using System.Security.Claims;
using Conta = ContaCorrente.Domain.Entities.ContaCorrente;
using ContaCorrente.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Transferencia.Api.Controllers;

[ApiController]
[Route("transferencias")]
public class TransferenciasController : ControllerBase
{
    private readonly ContaRepository _contas;
    private readonly TransferenciaRepository _transfers;
    private readonly IdempotenciaRepository _idem;
    private readonly IHttpClientFactory _httpFactory;
    public TransferenciasController(ContaRepository contas, TransferenciaRepository transfers, IdempotenciaRepository idem, IHttpClientFactory httpFactory)
    { _contas = contas; _transfers = transfers; _idem = idem; _httpFactory = httpFactory; }

    public record TransferirRequest(string Idempotencia, long NumeroContaDestino, decimal Valor);

    /// <summary>Efetua uma transferência entre contas da mesma instituição</summary>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> Transferir([FromBody] TransferirRequest req)
    {
        var origem = await ContaAutenticadaAsync();
        if (origem is null) return StatusCode(403);
        if (!origem.Ativo) return BadRequest(new { type = "INACTIVE_ACCOUNT", message = "Conta origem inativa" });
        if (req.Valor <= 0) return BadRequest(new { type = "INVALID_VALUE", message = "Valor deve ser positivo" });

        var destino = await _contas.GetByNumeroAsync(req.NumeroContaDestino);
        if (destino is null) return BadRequest(new { type = "INVALID_ACCOUNT", message = "Conta destino inexistente" });
        if (!destino.Ativo) return BadRequest(new { type = "INACTIVE_ACCOUNT", message = "Conta destino inativa" });

        // Idempotência
        if (!await _idem.TryBeginAsync(req.Idempotencia, JsonSerializer.Serialize(req)))
            return NoContent();

        var http = _httpFactory.CreateClient("conta");
        // Repasse do token recebido
        var auth = Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrWhiteSpace(auth))
            http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(auth);

        // Débito origem (não envia NumeroConta)
        var debitoPayload = JsonSerializer.Serialize(new { Idempotencia = req.Idempotencia + ":D", NumeroConta = (long?)null, Valor = Math.Round(req.Valor, 2), Tipo = 'D' });
        var respDebito = await http.PostAsync("/contas/movimentar", new StringContent(debitoPayload, System.Text.Encoding.UTF8, "application/json"));
        if (!respDebito.IsSuccessStatusCode)
        {
            await _idem.CompleteAsync(req.Idempotencia, await respDebito.Content.ReadAsStringAsync());
            return BadRequest(new { type = "DEBIT_FAILED" });
        }

        // Crédito destino (envia NumeroConta)
        var creditoPayload = JsonSerializer.Serialize(new { Idempotencia = req.Idempotencia + ":C", NumeroConta = req.NumeroContaDestino, Valor = Math.Round(req.Valor, 2), Tipo = 'C' });
        var respCredito = await http.PostAsync("/contas/movimentar", new StringContent(creditoPayload, System.Text.Encoding.UTF8, "application/json"));
        if (!respCredito.IsSuccessStatusCode)
        {
            // Estorno origem
            var estornoPayload = JsonSerializer.Serialize(new { Idempotencia = req.Idempotencia + ":E", NumeroConta = (long?)null, Valor = Math.Round(req.Valor, 2), Tipo = 'C' });
            await http.PostAsync("/contas/movimentar", new StringContent(estornoPayload, System.Text.Encoding.UTF8, "application/json"));
            await _idem.CompleteAsync(req.Idempotencia, await respCredito.Content.ReadAsStringAsync());
            return BadRequest(new { type = "CREDIT_FAILED" });
        }

        // Persistir transferência
        await _transfers.InsertAsync(Guid.NewGuid().ToString(), origem.Id, destino.Id, DateTime.UtcNow, Math.Round(req.Valor, 2));
        await _idem.CompleteAsync(req.Idempotencia, null);
        return NoContent();
    }

    private async Task<Conta?> ContaAutenticadaAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(id)) return null;
        return await _contas.GetByIdAsync(id);
    }
}
