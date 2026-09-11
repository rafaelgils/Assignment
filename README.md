# Martech — Order Management

Backend de gestão de pedidos para um e-commerce simples, composto por uma API **Gateway** (autenticação, roteamento, resiliência) e um microserviço **Order** (regras de negócio simples, persistência), comunicando-se via RabbitMQ e HTTP.

## Stack

- .NET 10, ASP.NET Core (Controllers)
- Clean Architecture (Domain / Application / Infrastructure / API) no microserviço Order
- CQRS com MediatR (commands/queries separados) + FluentValidation (pipeline behavior de validação) + Serilog (pipeline behavior de logging)
- Entity Framework Core + SQLite, migrations aplicadas automaticamente no startup
- RabbitMQ (filas `orders.main` / `orders.retry` / `orders.deadletter`) para a criação assíncrona de pedidos
- Redis: cache do token JWT ativo (Gateway) e chaves de idempotência
- JWT (usuário fixo em memória) + Polly (retry + circuit breaker) nas chamadas do Gateway ao Order.API
- OpenTelemetry (tracing, exportado para o console)
- xUnit + Moq (testes unitários) e xUnit + `WebApplicationFactory` (teste de integração)
- Docker / Docker Compose

## Por que Controllers em vez de Minimal API

Cada serviço expõe múltiplos endpoints relacionados (login, criação, listagem paginada, busca por id, cancelamento) que se beneficiam de agrupamento por rota, model binding automático de DTOs de request/response e um pipeline de exceções centralizado (middleware) — Controllers deixam essa organização explícita por convenção, enquanto Minimal API exigiria repetir esse boilerplate em cada `MapX`. Como a API cresce em endpoints e regras (não é um serviço de 1-2 rotas), Controllers reduz a duplicação. A única exceção nos dois serviços é o endpoint `/health`, deixado como Minimal API (`app.MapGet(...)` direto no `Program.cs`) por ser uma rota trivial que não justifica um Controller próprio.

Vale notar que isso é uma escolha de estilo de endpoint, não de arquitetura interna — as duas APIs usam Controllers, mas só o **Order.API** segue Clean Architecture de fato (camadas `Domain`/`Application`/`Infrastructure`/`API` em projetos separados, com CQRS via MediatR), porque é ali que existe regra de negócio e invariantes de domínio genuínos (cálculo de `TotalAmount`, transições de status, etc.). O **Gateway.API** é um projeto único, organizado por pasta de responsabilidade (`Controllers/`, `Auth/`, `Clients/`, `Idempotency/`, `Messaging/`, `Resilience/`) em vez de por camada — ele autentica, valida idempotência, publica no RabbitMQ e repassa chamadas HTTP pro Order.API, sem lógica de domínio própria que justificasse a mesma separação em camadas.

## Como rodar localmente (sem Docker)

Pré-requisitos: .NET 10 SDK, uma instância de RabbitMQ e uma de Redis acessíveis (ex.: via `docker compose up rabbitmq redis`).

```bash
dotnet run --project src/Order/Order.API   # http://localhost:5284
dotnet run --project src/Gateway/Gateway.API
```

O `Order.API` aplica as migrations do EF Core automaticamente ao subir. Ajuste `appsettings.Development.json` de cada projeto se o RabbitMQ/Redis não estiverem em `localhost`.

## Como rodar via Docker

```bash
cp .env.example .env   # ajuste os valores, principalmente JWT_SECRET
docker compose up --build
```

Sobe `rabbitmq` (painel em `http://localhost:15672`, usuário/senha do `.env`), `redis`, `order-api` (interno) e `gateway-api` (exposto em `http://localhost:8080`, único ponto de entrada externo).

## Endpoints (Gateway — `http://localhost:8080`)

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/auth/login` | — | Autentica o usuário fixo (`dev@martech.com` / `Senha@123`) e retorna um JWT |
| POST | `/api/order` | Bearer | Cria um pedido de forma assíncrona (publica no RabbitMQ). Requer o header `Idempotency-Key`; retorna `202 Accepted` |
| GET | `/api/orders?page=&pageSize=` | Bearer | Lista pedidos paginados |
| GET | `/api/orders/{id}` | Bearer | Busca um pedido por id |
| PATCH | `/api/orders/{id}/cancel` | Bearer | Cancela um pedido `Pending` (`409` se não estiver `Pending`) |

### Exemplos de requisição (curl)

Substitua `BASE_URL` por `http://localhost:8080` (via Docker Compose) ou `http://localhost:5069` (`dotnet run --project src/Gateway/Gateway.API` local). Os exemplos usam `jq` para extrair campos do JSON de resposta — sem ele, basta olhar o corpo retornado manualmente.

```bash
BASE_URL=http://localhost:8080
```

**1. Login** — autentica o usuário fixo e retorna o JWT:

```bash
curl -s -X POST "$BASE_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"dev@martech.com","password":"Senha@123"}'
```

Para encadear com os próximos exemplos, guarde o token numa variável:

```bash
TOKEN=$(curl -s -X POST "$BASE_URL/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"dev@martech.com","password":"Senha@123"}' | jq -r '.token')
```

**2. Criar pedido** — requer `Authorization` e o header `Idempotency-Key`; a criação é assíncrona (publica no RabbitMQ), por isso a resposta é `202 Accepted` com o `id` já gerado:

```bash
curl -i -X POST "$BASE_URL/api/order" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "customerId": "11111111-1111-1111-1111-111111111111",
    "items": [
      { "productName": "Widget", "quantity": 2, "unitPrice": 9.99 }
    ]
  }'
```

Guarde o `id` retornado (o pedido pode levar alguns milissegundos para ficar disponível no `GET`, até o consumer processar a mensagem):

```bash
ORDER_ID=$(curl -s -X POST "$BASE_URL/api/order" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","items":[{"productName":"Widget","quantity":2,"unitPrice":9.99}]}' \
  | jq -r '.id')
```

**3. Listar pedidos (paginado)**:

```bash
curl -s "$BASE_URL/api/orders?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN"
```

**4. Buscar pedido por id**:

```bash
curl -s "$BASE_URL/api/orders/$ORDER_ID" \
  -H "Authorization: Bearer $TOKEN"
```

**5. Cancelar pedido** — só é aceito com status `Pending` (`204 No Content`; `409 Conflict` se o pedido já não estiver `Pending`, `404` se o id não existir):

```bash
curl -i -X PATCH "$BASE_URL/api/orders/$ORDER_ID/cancel" \
  -H "Authorization: Bearer $TOKEN"
```

### Idempotência em `POST /api/order`

O cliente deve enviar um header `Idempotency-Key` (um id de chamada único, ex. um GUID). O Gateway grava essa chave no Redis associada ao `OrderId` gerado; reenviar a mesma chave retorna o mesmo `OrderId` sem publicar uma nova mensagem. O `Order.API` também é idempotente por `OrderId` (proteção adicional contra redelivery do RabbitMQ).

## Resiliência

O sistema tem duas estratégias de resiliência independentes, cada uma cobrindo um tipo de falha diferente — mais a idempotência, que não é resiliência a falha em si, mas evita que uma falha do lado do cliente (retry manual, timeout no meio de uma chamada) crie um pedido duplicado.

### RabbitMQ: fila de retry + dead-letter (falha ao *processar* um pedido)

Cobre falhas no **consumo assíncrono** — o `CreateOrderConsumer` do Order.API processando a mensagem de criação de pedido (ex.: erro transiente no banco, exceção de validação inesperada).

```
orders.main --(falha)--> orders.retry --(TTL 5s, dead-letter automático)--> orders.main --(3ª falha)--> orders.deadletter
```

- A mensagem entra em `orders.main`. Se o processamento falhar (exceção capturada no consumer), ela é republicada em `orders.retry` com o header `x-retry-count` incrementado.
- `orders.retry` não tem consumer algum — ela só existe pra segurar a mensagem por um TTL de 5s (`x-message-ttl`). Ao expirar, o próprio RabbitMQ a redireciona automaticamente pra `orders.main` via `x-dead-letter-exchange`/`x-dead-letter-routing-key` (mecanismo nativo de dead-lettering do broker — não é código nosso, é configuração da fila).
- Depois de 3 tentativas com erro (`OrdersTopology.MaxRetryAttempts`), a mensagem vai pra `orders.deadletter` em vez de voltar pra `orders.main` — fica lá disponível pra inspeção manual, sem reprocessar indefinidamente.

Implementado em [CreateOrderConsumer.cs](src/Order/Order.Infrastructure/Messaging/CreateOrderConsumer.cs) (método `RouteToRetryOrDeadLetterAsync`) e na topologia declarada em [OrdersTopologyDeclarer.cs](src/Shared/Shared.Contracts/Messaging/OrdersTopologyDeclarer.cs).

### Polly: retry + circuit breaker (chamadas HTTP do Gateway ao Order.API)

Cobre falhas de **rede/disponibilidade** nas chamadas síncronas que o Gateway faz ao Order.API — listar, buscar por id, cancelar. Não se aplica à criação de pedido, que é assíncrona via RabbitMQ e não passa por essas políticas.

Definido em [ResiliencePolicies.cs](src/Gateway/Gateway.API/Resilience/ResiliencePolicies.cs):

```csharp
public static IAsyncPolicy<HttpResponseMessage> RetryPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)));

public static IAsyncPolicy<HttpResponseMessage> CircuitBreakerPolicy() =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
```

- **Retry**: até 3 tentativas em erro transiente (timeout, erro de conexão, `5xx`), com backoff exponencial (~200ms, 400ms, 800ms entre tentativas).
- **Circuit breaker**: depois de 5 falhas transientes seguidas, o circuito abre por 30s — nesse período as chamadas falham imediatamente, sem nem tentar a rede, evitando martelar um Order.API que já está com problema.

Registrado no `HttpClient` do `IOrderApiClient`, em [Program.cs](src/Gateway/Gateway.API/Program.cs):

```csharp
builder.Services.AddHttpClient<IOrderApiClient, OrderApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:OrderApi"]!);
})
    .AddPolicyHandler(ResiliencePolicies.RetryPolicy())
    .AddPolicyHandler(ResiliencePolicies.CircuitBreakerPolicy());
```

A ordem dos `.AddPolicyHandler(...)` importa: o retry fica "por fora" do circuit breaker, então cada nova tentativa do retry é que alimenta a contagem de falhas do circuit breaker — se as 3 tentativas de retry falharem, conta como 1 falha pro circuito abrir depois de 5 dessas.

## Variáveis de ambiente

Ver `.env.example`. Principais: `RABBITMQ_USER`/`RABBITMQ_PASSWORD`, `JWT_SECRET`/`JWT_ISSUER`/`JWT_AUDIENCE`.

## Testes

```bash
dotnet test
```
- `tests/E2E.Tests` — Testes ponta a ponta reais
- `tests/Gateway.API.Tests` — Testes específicos do Gateway, atualmente possuindo apenas um teste de RateLimit
- `tests/Order.Application.Tests` — testes unitários dos handlers CQRS (com Moq), incluindo idempotência e as regras de cancelamento.
- `tests/Order.API.IntegrationTests` — testes de integração com `WebApplicationFactory` contra um SQLite em memória.

### Cobertura de testes

```bash
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

O `coverlet.runsettings` exclui da métrica o código gerado automaticamente (source generator do OpenAPI em `obj/`), que não é código autoral e não deve contar como "não testado". O relatório `coverage.cobertura.xml` é gerado em `TestResults/<guid>/`; para um resumo legível, use o [ReportGenerator](https://github.com/danielpalme/ReportGenerator):

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/report -reporttypes:TextSummary
```

## SonarQube

```bash
docker compose --profile tools up -d sonarqube   # http://localhost:9000 (login padrão: admin/admin — troque na primeira vez)
dotnet tool install --global dotnet-sonarscanner  # se ainda não instalado
```

Gere um token em `http://localhost:9000` → **My Account → Security → Generate Token**, depois:

```bash
dotnet-sonarscanner begin /k:"martech" /d:sonar.host.url="http://localhost:9000" /d:sonar.token="<seu-token>"
dotnet build
dotnet-sonarscanner end /d:sonar.token="<seu-token>"
```

