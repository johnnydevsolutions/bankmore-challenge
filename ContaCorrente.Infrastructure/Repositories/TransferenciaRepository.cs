using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ContaCorrente.Infrastructure.Repositories;

public class TransferenciaRepository
{
    private readonly string _cs;
    public TransferenciaRepository(string connectionString) { _cs = connectionString; }
    private IDbConnection Open() => new SqliteConnection(_cs);

    public async Task InsertAsync(string id, string origemId, string destinoId, DateTime data, decimal valor)
    {
        using var conn = Open();
        await conn.ExecuteAsync(@"INSERT INTO transferencia (idtransferencia, idcontacorrente_origem, idcontacorrente_destino, datamovimento, valor)
                                  VALUES (@id, @origem, @destino, @data, @valor)",
            new { id, origem = origemId, destino = destinoId, data = data.ToString("O"), valor = (double)valor });
    }
}

