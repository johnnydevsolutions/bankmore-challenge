using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ContaCorrente.Infrastructure.Repositories;

public class IdempotenciaRepository
{
    private readonly string _cs;
    public IdempotenciaRepository(string connectionString) { _cs = connectionString; }
    private IDbConnection Open() => new SqliteConnection(_cs);

    public async Task<string?> GetResultadoAsync(string chave)
    {
        using var conn = Open();
        return await conn.ExecuteScalarAsync<string?>("SELECT resultado FROM idempotencia WHERE chave_idempotencia=@chave", new { chave });
    }

    public async Task<bool> TryBeginAsync(string chave, string requisicao)
    {
        using var conn = Open();
        try
        {
            await conn.ExecuteAsync("INSERT INTO idempotencia (chave_idempotencia, requisicao, resultado) VALUES (@chave, @requisicao, NULL)", new { chave, requisicao });
            return true;
        }
        catch
        {
            return false; // já existe
        }
    }

    public async Task CompleteAsync(string chave, string? resultado)
    {
        using var conn = Open();
        await conn.ExecuteAsync("UPDATE idempotencia SET resultado=@resultado WHERE chave_idempotencia=@chave", new { chave, resultado });
    }
}

