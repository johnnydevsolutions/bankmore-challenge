# Banco Digital da Ana — Implementação do Teste

Este repositório contém uma implementação funcional mínima dos requisitos principais do desafio, focada em rodar localmente via Docker Compose, sem custos.

- API Conta Corrente (ASP.NET Core 8)
  - Cadastrar conta, Login (JWT), Inativar, Movimentar (crédito/débito com idempotência), Saldo.
- API Transferência (ASP.NET Core 8)
  - Transferência entre contas (débito origem, crédito destino, estorno em falha) com idempotência.
- Persistência: SQLite + Dapper (schema criado automaticamente).
- Segurança: JWT (HS256) com chave forte, issuer/audience validados; senhas com PBKDF2 (Rfc2898, 100k iterações). CPF não é armazenado em claro (índice hash para lookup).
- Swagger com botão Authorize (Bearer), respostas/requests padronizados em JSON.
- Health endpoints: `/health` e `/ready` em ambas as APIs.

## Como rodar com Docker

Pré‑requisitos: Docker/Docker Compose

1. `docker compose up -d --build`
2. Acesse:
   - ContaCorrente API: `http://localhost:8081/swagger`
   - Transferência API: `http://localhost:8082/swagger`

Variáveis já definidas no `docker-compose.yml`:
- `Jwt__Key` (64 chars), `Jwt__Issuer=bankmore.local`, `Jwt__Audience=bankmore.clients`
- `CONTA_API_URL=http://conta:8080` (base usada pela Transferência API para chamar a Conta API)

O banco SQLite fica em um volume Docker (`conta_data`).

## Fluxo de testes (copiável)

1. Cadastrar Conta A — POST `/contas/cadastrar`
   {
     "Cpf": "123.456.789-09",
     "Nome": "Cliente A",
     "Senha": "senhaA@123"
   }
   → Guarde o "Numero" (ex.: 1000000001).

2. Cadastrar Conta B — POST `/contas/cadastrar`
   {
     "Cpf": "987.654.321-00",
     "Nome": "Cliente B",
     "Senha": "senhaB@123"
   }
   → Guarde o "Numero" (ex.: 1000000002).

3. Login (Conta A) — POST `/contas/login`
   {
     "DocumentoOuNumero": "1000000001",
     "Senha": "senhaA@123"
   }
   → Copie o Token e clique em "Authorize" no Swagger (cole apenas o token, sem escrever "Bearer ").

4. Crédito na A — POST `/contas/movimentar` (autenticado)
   {
     "Idempotencia": "credA-001",
     "NumeroConta": null,
     "ContaId": null,
     "Valor": 500.00,
     "Tipo": "C"
   }
   → Esperado: 204 No Content. Se reutilizar a mesma `Idempotencia`, também retorna 204, sem duplicar.

5. Saldo A — GET `/contas/saldo` (autenticado)

6. Transferência A→B — POST `http://localhost:8082/transferencias` (autenticado com token da A)
   {
     "Idempotencia": "transf-001",
     "NumeroContaDestino": 1000000002,
     "Valor": 125.50
   }
   → Esperado: 204. Internamente, a API de Transferência resolve `ContaId` e chama a Conta API sem trafegar número de conta.

7. Saldo B — Login na B e GET `/contas/saldo` (token da B).

Notas:
- Sempre use `Content-Type: application/json`. Após login, o botão Authorize envia o Bearer automaticamente em endpoints protegidos.
- Erros comuns: reutilizar `Idempotencia` (a operação é ignorada) ou usar token expirado (401/403).

## Desenvolvimento local (sem Docker)

Pré‑requisito: .NET 8 SDK
- Build: `dotnet build BancoAna.sln`
- Testes: `dotnet test Tests/Tests.csproj`
- Rodar APIs: `dotnet run --project ContaCorrente.Api` e `dotnet run --project Transferencia.Api`

## Próximos passos sugeridos
- Logs estruturados/métricas/tracing
- Testes adicionais (idempotência repetida; cenários de falha na transferência)
- Kafka (eventos de movimentação/transferência) e módulo de Tarifas (opcional no desafio)
