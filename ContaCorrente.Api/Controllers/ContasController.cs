using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Conta = ContaCorrente.Domain.Entities.ContaCorrente;
using ContaCorrente.Domain.Entities;
using ContaCorrente.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ContaCorrente.Api.Controllers;

[ApiController]
[Route("contas")]
[Produces("application/json")]
[Consumes("application/json")]
public class ContasController : ControllerBase
{
    private readonly ContaRepository _contas;
    private readonly MovimentoRepository _movs;
    private readonly IConfiguration _cfg;
    private readonly IdempotenciaRepository _idem;
    public ContasController(ContaRepository contas, MovimentoRepository movs, IConfiguration cfg, IdempotenciaRepository idem)
    {
        _contas = contas; _movs = movs; _cfg = cfg; _idem = idem;
    }

    public record CadastroRequest(string Cpf, string Nome, string Senha);
    public record CadastroResponse(long Numero);
    /// <summary>Cadastro de conta corrente</summary>
    [HttpPost("cadastrar")]
    [ProducesResponseType(typeof(CadastroResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Cadastrar([FromBody] CadastroRequest req)
    {
        if (!CpfValido(req.Cpf))
            return BadRequest(new { type = "INVALID_DOCUMENT", message = "CPF inválido" });

        var numero = await GerarNumeroUnicoAsync();
        var (senhaHash, senhaSalt) = HashSenha(req.Senha);
        var (cpfHash, cpfSalt) = HashSenha(req.Cpf);
        var cpfIndex = Sha256Base64(new string(req.Cpf.Where(char.IsDigit).ToArray()));

        var conta = new Conta
        {
            Numero = numero,
            Nome = req.Nome,
            Ativo = true,
            SenhaHash = senhaHash,
            SenhaSalt = senhaSalt,
            CpfHash = cpfHash,
            CpfSalt = cpfSalt,
            CpfIndex = cpfIndex
        };
        await _contas.InsertAsync(conta);
        return Ok(new CadastroResponse(numero));
    }

    public record LoginRequest(string DocumentoOuNumero, string Senha);
    public record LoginResponse(string Token);
    /// <summary>Login por número da conta ou CPF</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        Conta?  conta = null;
        if (long.TryParse(req.DocumentoOuNumero, out var numero))
        {
            conta = await _contas.GetByNumeroAsync(numero);
        }
        else
        {
            var cpfIndex = Sha256Base64(new string(req.DocumentoOuNumero.Where(char.IsDigit).ToArray()));
            conta = await _contas.GetByCpfIndexAsync(cpfIndex);
        }
        // Simplificação: caso receba CPF, não temos busca por CPF hash
        if (conta is null)
            return Unauthorized(new { type = "USER_UNAUTHORIZED", message = "Usuário ou senha inválidos" });

        if (!VerificaSenha(req.Senha, conta.SenhaSalt, conta.SenhaHash))
            return Unauthorized(new { type = "USER_UNAUTHORIZED", message = "Usuário ou senha inválidos" });

        var token = EmitirJwt(conta);
        return Ok(new LoginResponse(token));
    }

    public record InativarRequest(string Senha);
    [Authorize]
    /// <summary>Inativa a conta corrente autenticada</summary>
    [HttpPost("inativar")]
    [ProducesResponseType(204)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Inativar([FromBody] InativarRequest req)
    {
        var conta = await ContaAutenticadaAsync();
        if (conta is null)
            return StatusCode(403);
        if (!VerificaSenha(req.Senha, conta.SenhaSalt, conta.SenhaHash))
            return StatusCode(403);
        await _contas.SetAtivoAsync(conta.Id, false);
        return NoContent();
    }

    public record MovimentarRequest(string Idempotencia, long? NumeroConta, string? ContaId, decimal Valor, char Tipo);
    [Authorize]
    /// <summary>Crédito/Débito na conta</summary>
    [HttpPost("movimentar")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Movimentar([FromBody] MovimentarRequest req)
    {
        var contaLogada = await ContaAutenticadaAsync();
        if (contaLogada is null) return StatusCode(403);

        if (req.Valor <= 0) return BadRequest(new { type = "INVALID_VALUE", message = "Valor deve ser positivo" });
        if (req.Tipo != 'C' && req.Tipo != 'D') return BadRequest(new { type = "INVALID_TYPE", message = "Tipo inválido" });

        Conta contaMov = contaLogada;
        // Preferir direcionamento por ContaId para evitar trânsito de número entre serviços
        if (!string.IsNullOrWhiteSpace(req.ContaId))
        {
            if (req.Tipo != 'C') return BadRequest(new { type = "INVALID_TYPE", message = "Apenas crǸdito permitido para terceiros" });
            var outraById = await _contas.GetByIdAsync(req.ContaId);
            if (outraById is null) return BadRequest(new { type = "INVALID_ACCOUNT", message = "Conta inexistente" });
            contaMov = outraById;
        }
        if (req.NumeroConta.HasValue && req.NumeroConta.Value != contaLogada.Numero)
        {
            if (req.Tipo != 'C') return BadRequest(new { type = "INVALID_TYPE", message = "Apenas crédito permitido para terceiros" });
            var outra = await _contas.GetByNumeroAsync(req.NumeroConta.Value);
            if (outra is null) return BadRequest(new { type = "INVALID_ACCOUNT", message = "Conta inexistente" });
            contaMov = outra;
        }

        if (!contaMov.Ativo) return BadRequest(new { type = "INACTIVE_ACCOUNT", message = "Conta inativa" });

        // Idempotência persistida
        if (!await _idem.TryBeginAsync(req.Idempotencia, System.Text.Json.JsonSerializer.Serialize(req)))
        {
            return NoContent();
        }

        var mov = new Movimento
        {
            ContaId = contaMov.Id,
            Data = DateTime.UtcNow,
            Tipo = req.Tipo,
            Valor = Math.Round(req.Valor, 2)
        };
        await _movs.InsertAsync(mov);
        await _idem.CompleteAsync(req.Idempotencia, null);
        return NoContent();
    }

    [Authorize]
    /// <summary>Consulta saldo</summary>
    [HttpGet("saldo")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Saldo()
    {
        var conta = await ContaAutenticadaAsync();
        if (conta is null) return StatusCode(403);
        if (!conta.Ativo) return BadRequest(new { type = "INACTIVE_ACCOUNT", message = "Conta inativa" });
        var saldo = await _movs.GetSaldoAsync(conta.Id);
        return Ok(new
        {
            Numero = conta.Numero,
            Nome = conta.Nome,
            Data = DateTime.UtcNow,
            Saldo = saldo
        });
    }

    private async Task<long> GerarNumeroUnicoAsync()
    {
        var rnd = RandomNumberGenerator.Create();
        while (true)
        {
            var bytes = new byte[8];
            rnd.GetBytes(bytes);
            var numero = Math.Abs(BitConverter.ToInt64(bytes, 0)) % 1_000_000_0000L; // 10 dígitos
            if (numero < 1_000_000_000L) numero += 1_000_000_000L; // garantir 10 dígitos
            if (!await _contas.NumeroExistsAsync(numero)) return numero;
        }
    }

    private static bool CpfValido(string cpf)
    {
        cpf = new string(cpf.Where(char.IsDigit).ToArray());
        if (cpf.Length != 11) return false;
        if (new string(cpf[0], 11) == cpf) return false;
        int[] mult1 = {10,9,8,7,6,5,4,3,2};
        int[] mult2 = {11,10,9,8,7,6,5,4,3,2};
        string temp = cpf[..9];
        int sum = 0;
        for (int i=0;i<9;i++) sum += (temp[i]-'0')*mult1[i];
        int rem = sum%11; int d1 = rem<2?0:11-rem;
        temp += d1.ToString(); sum=0;
        for (int i=0;i<10;i++) sum += (temp[i]-'0')*mult2[i];
        rem = sum%11; int d2 = rem<2?0:11-rem;
        return cpf.EndsWith(d1.ToString()+d2.ToString());
    }

    private static (string hash, string salt) HashSenha(string value)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(value, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerificaSenha(string input, string salt, string hash)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(input, saltBytes, 100_000, HashAlgorithmName.SHA256);
        var computed = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(computed) == hash;
    }

    private static string Sha256Base64(string value)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async Task<Conta? > ContaAutenticadaAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id)) return null;
        return await _contas.GetByIdAsync(id);
    }

    private string EmitirJwt(Conta conta)
    {
        var key = _cfg["Jwt:Key"] ?? "dev-secret-key-change";
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, conta.Id),
            new Claim("numero", conta.Numero.ToString())
        };
        var issuer = _cfg["Jwt:Issuer"] ?? "bankmore.local";
        var audience = _cfg["Jwt:Audience"] ?? "bankmore.clients";
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
