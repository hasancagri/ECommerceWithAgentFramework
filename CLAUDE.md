# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build ECommerceWithAgentFramework.slnx   # build everything (.NET 10, slnx solution)
dotnet run --project src/AppHost                # run the whole system via Aspire (needs Docker)
```

- The AppHost starts Postgres (+pgAdmin), Redis (+RedisInsight), and RabbitMQ (+management UI) as persistent containers, then all services. There is no way to meaningfully run a single service standalone — they depend on Aspire service discovery and the containers.
- No test projects exist yet.
- NuGet versions are centralized in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); never put a `Version=` on a `PackageReference` in a csproj.
- The ChatAgent requires `OpenAI:ApiKey` from its own user-secrets: `dotnet user-secrets set OpenAI:ApiKey <key> --project src/ChatAgent`.
- Code comments are written in Turkish — follow that convention when adding comments.

## Architecture

.NET Aspire distributed app (`src/AppHost/AppHost.cs` is the topology map). Projects:

- `src/services/*` — microservices (catalog, basket, order, discount, payment, stock, file), each a minimal API with its own Postgres database (Marten schema per service).
- `src/services/gateway` — YARP reverse proxy. Routes REST and MCP traffic to services using Aspire service discovery names (`http://catalog-api`). Routes declare `AuthorizationPolicy` of either `ClientCredential` (app token) or `Password` (user token); currently both just require an authenticated token.
- `src/Identity.Server` — Duende IdentityServer + ASP.NET Identity. **Must run over HTTPS** (`https://localhost:5001`): login cookies are SameSite=None and the issuer address must match in every service's auth config, or login loops.
- `src/ChatAgent` — Microsoft Agent Framework host exposing OpenAI-compatible endpoints (`/public/v1/*` anonymous agent with catalog tools, `/assistant/v1/*` logged-in agent with catalog+basket tools). Tools come from the services' MCP servers, reached through the gateway (`/mcp/<service>/...`). Agents are registered as **Singleton** — the hosting library resolves them once at startup, so tools are collected once and per-user tokens cannot flow into tool calls yet (known deferred debt; see `docs/superpowers/`).
- `src/ui/WebApp` — Razor Pages storefront; its chat widget proxies to the ChatAgent.
- `src/Common` — cross-cutting library; `src/Shared` — integration-event payloads, enums, constants shared between services; `src/ServiceDefaults` — Aspire defaults (OTel, service discovery, resilience).

### Repo layout

```
src/
  AppHost/            Aspire topology — start here
  ServiceDefaults/    Aspire defaults (OTel, discovery, resilience)
  Identity.Server/    Duende IdentityServer + ASP.NET Identity (HTTPS-only)
  ChatAgent/          Microsoft Agent Framework host (OpenAI-compatible endpoints)
  Shared/             integration-event payloads, enums, constants
  ui/WebApp/          Razor Pages storefront + chat widget
  services/
    gateway/          YARP reverse proxy (REST + MCP routing)
    catalog basket order discount payment stock file   microservices (minimal API + Marten/Postgres)
  Common/             cross-cutting library:
    Auths/            ICurrentUser / CurrentUser
    Dependencies/     ITransient/IScoped/ISingletonDependency markers (Scrutor auto-registration)
    Domains/          BaseModel, AggregateRoot, Enumeration, IModel
    Exceptions/       GlobalExceptionHandler + validation exceptions
    Inputs/ Results/  request base models / result models (Feature*, ResultDomain, MessageItem)
    Utils/            Constants, Extensions, Helpers, Authorization (RequiredScope + middleware)
    Options/          IdentityOption (JWT auth binding)
    PubSubs/          Redis pub/sub — DEAD now, kept as caching-rework scaffolding (don't remove)
```

### Service-internal pattern (vertical slices)

Every service follows the same layout — mirror it when adding features:

- `Domains/<Entity>/Features/{Commands,Queries,Agent}/<FeatureName>.cs`: one static class per feature containing the message record, the response type, and the Wolverine handler. Handlers take `IDocumentSession` (Marten) and return `FeatureObjectResultModel<T>` / `FeatureResultModel` from Common.
- Endpoints are extension methods on `RouteGroupBuilder` in the same file or `<Entity>EndpointExtension.cs`, dispatching via Wolverine's `IMessageBus.InvokeAsync`.
- MCP tools are `[McpServerToolType]` static classes (`<Entity>McpTools.cs`) that dispatch the same messages via `IMessageBus` — REST and MCP share the handlers. Tool descriptions state *intent* (e.g. `get_product` is for adding to cart, `search_products` for showing links).
- Authorization is handler-level: put `[RequiredScope(AuthorizationScopes.X)]` on the message record; `ScopeAuthorizationMiddleware` is woven by Wolverine policy only into handlers whose message carries the attribute. This is the single auth point for both REST and MCP.
- Messaging: Wolverine + RabbitMQ fanout exchanges for integration events (payload records in `Shared`); constants in `RabbitMqConstants`. Publishers also declare/bind the consumer queues so messages published before the consumer starts are not dropped.
- DI: services implement `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` marker interfaces (Common.Dependencies) and are auto-registered by Scrutor scanning in each service's `Dependencies/DependencyExtensions.cs`.

### Key decisions & design docs

`docs/superpowers/specs/` holds the approved design specs (the *why*) for past features — read the relevant one before reworking that area:

- **Payment — client-orchestrated (A1):** the client calls Payment.Api directly with its user token, gets a `paymentId`, then creates the Order referencing it. Payment knows only amount+card; Order knows only the `paymentId`. No synchronous Order→Payment call. Note: the payment `amount` is client-supplied and not validated (learning-project tradeoff); idempotency is enforced Order-side via `paymentId` uniqueness.
- **Chat widget:** the WebApp customer-service chat agent takes real actions via MCP tools (e.g. add to cart) with streaming responses.
- **Orchestrator MCP auth (rev3):** the WebApp BFF (`ChatEndpoints.cs`) always supplies the token (anonymous → `ecommerce.bff` client_credentials); the orchestrator has no m2m provider of its own; authorization happens once, at the Wolverine handler middleware shared by REST and MCP.
- **Common refactor (2026-07-06):** net10.0 alignment, file-scoped + folder-aligned namespaces, nullable fixes, dead-code removal (SmsSenders, IAggregateRoot), `ResultDomain` → `Results/`. Behavior-neutral.

Known deferred debt: no caching implemented yet (`Common/PubSubs` is scaffolding for it); orchestrator agents are Singleton so per-user tokens can't flow into tool calls yet; chat history is in-memory.