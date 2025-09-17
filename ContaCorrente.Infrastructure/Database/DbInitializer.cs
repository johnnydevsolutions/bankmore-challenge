using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ContaCorrente.Infrastructure.Database;

public static class DbInitializer
{
    public static void EnsureDatabase(string connectionString)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();

        conn.Execute(@"CREATE TABLE IF NOT EXISTS contacorrente (
            idcontacorrente TEXT(37) PRIMARY KEY,
            numero INTEGER(10) NOT NULL UNIQUE,
            nome TEXT(100) NOT NULL,
            ativo INTEGER(1) NOT NULL default 0,
            senha TEXT(200) NOT NULL,
            salt TEXT(200) NOT NULL,
            cpfhash TEXT(200) NOT NULL,
            cpfsalt TEXT(200) NOT NULL,
            cpfindex TEXT(200),
            CHECK (ativo in (0,1))
        );", transaction: tx);

        conn.Execute(@"CREATE TABLE IF NOT EXISTS movimento (
            idmovimento TEXT(37) PRIMARY KEY,
            idcontacorrente TEXT(37) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            tipomovimento TEXT(1) NOT NULL,
            valor REAL NOT NULL,
            CHECK (tipomovimento in ('C','D')),
            FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
        );", transaction: tx);

        conn.Execute(@"CREATE TABLE IF NOT EXISTS idempotencia (
            chave_idempotencia TEXT(100) PRIMARY KEY,
            requisicao TEXT(2000),
            resultado TEXT(2000)
        );", transaction: tx);

        // Campos adicionais para CPF indexável e tabela de transferência
        try { conn.Execute("ALTER TABLE contacorrente ADD COLUMN cpfindex TEXT(200)", transaction: tx); } catch { }

        conn.Execute(@"CREATE TABLE IF NOT EXISTS transferencia (
            idtransferencia TEXT(37) PRIMARY KEY,
            idcontacorrente_origem TEXT(37) NOT NULL,
            idcontacorrente_destino TEXT(37) NOT NULL,
            datamovimento TEXT(25) NOT NULL,
            valor REAL NOT NULL
        );", transaction: tx);

        tx.Commit();
    }
}
