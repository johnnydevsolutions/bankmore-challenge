# Banco Digital da Ana – Implementação do Teste

Este repositório contém uma implementação funcional mínima dos requisitos principais:

- API Conta Corrente (ASP.NET Core 8)
  - Cadastrar conta: valida CPF, cria conta ativa e retorna número
  - Login: emite JWT (usa número da conta)
  - Inativar: exige token e senha
  - Movimentar: crédito/débito com validações e idempotência simplificada
  - Saldo: retorna saldo calculado (créditos – débitos)

- API Transferência (ASP.NET Core 8)
  - Transferir entre contas da mesma instituição (débito origem, crédito destino, estorno em falha)

- Persistência: SQLite + Dapper. Schema criado automaticamente ao subir.

- Segurança: JWT; senha e CPF são armazenados como hash + salt (CPF apenas para cumprir autenticação por documento no cadastro; login atual usa número da conta).

- Padrões: DDD (camada Domain/Infrastructure), CQRS (via MediatR – estrutura pronta), Swagger habilitado em Development.

Observações de escopo/teste:
- Idempotência: placeholder simplificado (tabela criada, uso mínimo nos endpoints).
- Transferência chama diretamente a mesma base (para evitar custos/infra); em produção, a orquestração deveria usar chamadas HTTP para a API de Conta.
- Kafka: serviços no docker-compose como placeholder; integração de tarifas é opcional e não implementada para manter custos zero.

## Como rodar com Docker

Requisitos: Docker/Docker Compose

1. `docker compose build`
2. `docker compose up -d`

Serviços:
- ContaCorrente API: `http://localhost:8081/swagger`
- Transferência API: `http://localhost:8082/swagger`

O banco SQLite fica em um volume Docker (`conta_data`).

## Fluxo básico

1. Cadastrar conta: POST `/contas/cadastrar` body `{ cpf, nome, senha }`
2. Login: POST `/contas/login` body `{ documentoOuNumero, senha }` (usar o número gerado no cadastro)
3. Usar o token Bearer nos demais endpoints (`Authorization: Bearer <token>`)
4. Movimentar: POST `/contas/movimentar` body `{ idempotencia, numeroConta?, valor, tipo }`
5. Saldo: GET `/contas/saldo`
6. Transferir: POST `/transferencias` body `{ idempotencia, numeroContaDestino, valor }`

## Desenvolvimento local

Requisitos: .NET 8 SDK

- Build: `dotnet build BancoAna.sln`
- Testes: `dotnet test Tests/Tests.csproj`
- Rodar APIs: `dotnet run --project ContaCorrente.Api` e `dotnet run --project Transferencia.Api`

## Próximos passos sugeridos

- Completar idempotência persistida em todos os comandos
- Login por CPF (usando hash + salt, sem armazenar o CPF puro)
- Documentar esquemas/erros detalhados no Swagger (exemplos)
- Separar bancos por serviço e comunicação por HTTP entre as APIs
- Implementar Kafka (KafkaFlow) para tarifa opcional

