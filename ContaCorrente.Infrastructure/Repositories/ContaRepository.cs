using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Conta = ContaCorrente.Domain.Entities.ContaCorrente;

namespace ContaCorrente.Infrastructure.Repositories;

public class ContaRepository
{
    private readonly string _cs;
    public ContaRepository(string connectionString)
    {
        _cs = connectionString;
    }

    private IDbConnection Open() => new SqliteConnection(_cs);

    public async Task<Conta?> GetByNumeroAsync(long numero)
    {
        using var conn = Open();
        return await conn.QueryFirstOrDefaultAsync<Conta>(
            "SELECT idcontacorrente as Id, numero, nome, (ativo=1) as Ativo, senha as SenhaHash, salt as SenhaSalt, cpfhash as CpfHash, cpfsalt as CpfSalt FROM contacorrente WHERE numero=@numero",
            new { numero });
    }

    public async Task<Conta?> GetByIdAsync(string id)
    {
        using var conn = Open();
        return await conn.QueryFirstOrDefaultAsync<Conta>(
            "SELECT idcontacorrente as Id, numero, nome, (ativo=1) as Ativo, senha as SenhaHash, salt as SenhaSalt, cpfhash as CpfHash, cpfsalt as CpfSalt FROM contacorrente WHERE idcontacorrente=@id",
            new { id });
    }

    public async Task<string> InsertAsync(Conta conta)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"INSERT INTO contacorrente (idcontacorrente, numero, nome, ativo, senha, salt, cpfhash, cpfsalt, cpfindex)
                                  VALUES (@Id, @Numero, @Nome, @AtivoInt, @SenhaHash, @SenhaSalt, @CpfHash, @CpfSalt, @CpfIndex)",
            new { conta.Id, conta.Numero, conta.Nome, AtivoInt = conta.Ativo ? 1 : 0, conta.SenhaHash, conta.SenhaSalt, conta.CpfHash, conta.CpfSalt, conta.CpfIndex });
        return conta.Id;
    }

    public async Task SetAtivoAsync(string id, bool ativo)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE contacorrente SET ativo=@ativo WHERE idcontacorrente=@id", new { id, ativo = ativo ? 1 : 0 });
    }

    public async Task<bool> NumeroExistsAsync(long numero)
    {
        using var conn = Open();
        var n = await conn.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM contacorrente WHERE numero=@numero", new { numero });
        return n > 0;
    }

    public async Task<Conta?> GetByCpfIndexAsync(string cpfIndex)
    {
        using var conn = Open();
        return await conn.QueryFirstOrDefaultAsync<Conta>(
            "SELECT idcontacorrente as Id, numero, nome, (ativo=1) as Ativo, senha as SenhaHash, salt as SenhaSalt, cpfhash as CpfHash, cpfsalt as CpfSalt FROM contacorrente WHERE cpfindex=@cpfIndex",
            new { cpfIndex });
    }
}
