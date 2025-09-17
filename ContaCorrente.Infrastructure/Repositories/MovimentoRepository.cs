using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using ContaCorrente.Domain.Entities;

namespace ContaCorrente.Infrastructure.Repositories;

public class MovimentoRepository
{
    private readonly string _cs;
    public MovimentoRepository(string connectionString) { _cs = connectionString; }
    private IDbConnection Open() => new SqliteConnection(_cs);

    public async Task InsertAsync(Movimento mov)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"INSERT INTO movimento (idmovimento, idcontacorrente, datamovimento, tipomovimento, valor)
                                  VALUES (@Id, @ContaId, @DataStr, @Tipo, @Valor)",
            new { mov.Id, mov.ContaId, DataStr = mov.Data.ToString("O"), mov.Tipo, Valor = (double)mov.Valor });
    }

    public async Task<decimal> GetSaldoAsync(string contaId)
    {
        using var conn = Open();
        var credit = await conn.ExecuteScalarAsync<double?>("SELECT SUM(valor) FROM movimento WHERE idcontacorrente=@id AND tipomovimento='C'", new { id = contaId }) ?? 0;
        var debit = await conn.ExecuteScalarAsync<double?>("SELECT SUM(valor) FROM movimento WHERE idcontacorrente=@id AND tipomovimento='D'", new { id = contaId }) ?? 0;
        return (decimal)credit - (decimal)debit;
    }
}
