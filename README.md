# ECommerce with Agent Framework

> An event-driven, Domain-Driven microservices e-commerce platform with a built-in AI shopping agent — orchestrated end-to-end with .NET Aspire.

<p>
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white">
  <img alt="Aspire" src="https://img.shields.io/badge/.NET_Aspire-orchestration-512BD4">
  <img alt="Marten" src="https://img.shields.io/badge/Marten-document%2Fevent_store-16a34a">
  <img alt="Wolverine" src="https://img.shields.io/badge/Wolverine-CQRS%20%2B%20messaging-0ea5e9">
  <img alt="PostgreSQL" src="https://img.shields.io/badge/PostgreSQL-per--service-4169E1?logo=postgresql&logoColor=white">
  <img alt="RabbitMQ" src="https://img.shields.io/badge/RabbitMQ-integration_events-FF6600?logo=rabbitmq&logoColor=white">
  <img alt="OpenIddict" src="https://img.shields.io/badge/OpenIddict-OIDC%2FOAuth-512BD4">
  <img alt="YARP" src="https://img.shields.io/badge/YARP-gateway-blueviolet">
  <img alt="MCP" src="https://img.shields.io/badge/MCP-tool_server%2Fclient-9333ea">
  <img alt="A2A" src="https://img.shields.io/badge/A2A-agent--to--agent-e11d48">
  <img alt="Microsoft Agent Framework" src="https://img.shields.io/badge/Microsoft_Agent_Framework-AI_agent-2563eb">
</p>

## Overview

This is a full **microservices e-commerce backend** where every service is an isolated **bounded context** with its own database, and cross-context communication happens only through **integration events**, the **Model Context Protocol (MCP)**, and — for decisions that need an immediate yes/no — a single sanctioned **typed gRPC channel** (stock reservation). On top of it sit two agent applications: **ChatAgent**, an AI assistant built on the **Microsoft Agent Framework** that acts as an MCP client — it can browse the catalog, manage a basket, and review a user's orders and payments by calling each service's MCP tools with that user's token, and — via the **Agent2Agent (A2A)** protocol — delegate installment quotes to a **remote payment agent living in a separate solution**, passing only the cart total and the default card's non-sensitive **BIN** (never the PAN, CVV, or token) — and **IngestionAgent**, a **stateless** supplier-ingestion consumer built on **Agent Framework Workflows**: the **Supplier.Gateway** boundary service pulls the supplier feed and publishes only new/changed records as canonical events, and the agent processes each message with four **LLM-driven writer agents** (brand → category → catalog → stock), each scoped to its own service's MCP tools and fenced in by deterministic guardrails.

It's a portfolio / learning project built to demonstrate how far you can push **DDD, CQRS, and event-driven design** in real .NET code — and how a modern LLM agent plugs into that architecture cleanly, without leaking business logic into the agent layer.

## What this project demonstrates

- **Bounded-context isolation** — 9 services, each with its own PostgreSQL database and Marten schema. No shared domain model; the same concept (e.g. *Product*) is modeled differently in each context — a rich aggregate in Catalog, a plain basket-item entity in Basket, a read-model row in Storefront.
- **Rich aggregates & enforced invariants** — business rules live inside aggregates (private collections, behavior methods), not in handlers. Illegal states are unrepresentable.
- **Vertical Slice + CQRS** — code is organized by feature, not by technical layer. Writes and reads are separate slices; no repositories — handlers use Marten's `IDocumentSession` directly.
- **Result pattern over exceptions** — expected failures (not-found, validation, rule violations) flow through typed `Result` objects; exceptions are reserved for the truly unexpected.
- **Scope-based authorization** — identity issued by OpenIddict + ASP.NET Identity; services authorize on OAuth **scopes** (no roles), enforced on HTTP endpoints *and* on Wolverine message handlers.
- **Push-only read model** — the `storefront` service maintains a product-centric composite view (catalog + stock) fed purely by integration events — no outbound calls, no backfill. The web home page is served entirely from this view (fat `ProductChangedEvent` carries name, description, brand & category ids+names, price) — one anonymous read call renders every card with stock badges. The product list filters by dynamic **category & brand** (AND-combinable; facet options derive from the same view, so empty categories never appear — 016).
- **Hybrid product search (filters + semantic, via chat)** — the storefront exposes a single `search_storefront_products` MCP tool (plus an anonymous REST twin): optional brand-OR / price-range / min-stock filters combined with a natural-language `searchText`. Embeddings (`text-embedding-3-small`) are produced on `ProductChangedEvent` only when the search text's hash actually changed, stored as a side document in **pgvector**-enabled `storefrontDb`, and queried with a raw cosine-distance SQL join — filters stay hard, ranking is semantic, and an embedding outage never blocks the view write or filter-only search (019). The slice splits into two execution paths behind one query: no `searchText` → Marten LINQ over sellable rows plus a pure, unit-testable filter core with deterministic `Name ASC` ordering; with `searchText` → hand-written SQL, because top-K + similarity-threshold must run *in the database* (limit-then-filter would silently drop results). Notable craft in the SQL path: the query vector travels as a **text parameter** with a server-side `::vector(1536)` cast (immune to Npgsql's pg_type cache race), every user value is a bound parameter (SQL string only ever concatenates constants), an `INNER JOIN` on the embedding side-table makes "no embedding → not ranked" structural rather than conditional, and the command borrows the Marten session's own connection instead of opening a new one. The similarity threshold (cosine distance ≤ 0.7) acts as a floor filter under top-K — it reliably drops unrelated matches, not a precision dial.
- **Saved cards & addresses with PCI-safe tokenization** — a `customer` bounded context holds each user's **Wallet** (saved cards) and **AddressBook**, two aggregates keyed by user id with a "≤1 default" invariant. Raw PAN/CVV are **never persisted, logged, evented, or exposed** — `AddCard` passes them straight to a tokenizer (a simulated gateway now, swappable for a real PSP) and stores only a token + brand + last4 + expiry. MCP exposes **read-only** tools (`list_cards` / `list_addresses`) — there is deliberately no add-card tool. Checkout then lets the user **select** a saved address and card instead of retyping them, defaulting to the marked-default of each.
- **Cross-agent delegation over A2A (installment quotes)** — ChatAgent is also an **Agent2Agent client**: when a user asks for installments on their basket, the assistant resolves the cart total (`get_basket`) and the default card's **BIN** (a new read-only `get_default_card_bin` MCP tool over the customer context), then delegates to a **remote payment agent** published by a *separate* solution. It discovers that agent from its `/.well-known/agent-card.json`, verifies the advertised `installment_quote` skill, and wraps it as a single callable tool (`AsAIFunction`). The boundary is deliberately **PCI-clean**: only the amount and the non-sensitive BIN cross the wire — full PAN/CVV/token never touch the A2A channel or the LLM context. It is **fail-open** by construction: if the remote agent is unconfigured or unreachable, the tool simply isn't added and every other capability keeps working (the whole system boots and runs without the remote side existing). The BIN itself is captured at card-add time (first 6 digits, non-sensitive) — the tokenizer still discards the PAN.
- **Declarative, cross-cutting caching** — read queries are cached with a single `[Cached(...)]` attribute via an `IMessageBus` decorator over HybridCache (L1 in-memory + optional Redis L2). Handlers stay untouched.
- **AI agent as a first-class client** — each service exposes its Wolverine commands/queries as MCP tools; ChatAgent consumes them per-user. MCP tools are thin wrappers — zero business logic duplication.
- **LLM-driven writers with deterministic guardrails** — supplier ingestion is split at the boundary: `supplier-gateway` pulls the feed, normalizes it to a canonical `SupplierProductSnapshotReceived` event, and publishes **only new/changed records** (change gate via record value equality, transactional outbox). The stateless `ingestion-agent` consumes each message with a per-message **Agent Framework Workflow**: four writer agents (brand → category → catalog → stock), each a `ChatClientAgent` **scoped by allowlist** to its own service's MCP tools (`upsert_brand` / `upsert_category` / `upsert_product` / `set_stock`), temperature 0, returning **typed structured-output results** — no hand-written envelope parsing. Steps hand off via typed results over **conditional workflow edges**: a failed step routes straight to the terminal, so later LLMs are never even invoked. `BrandId`/`CategoryId`/`ProductId` are minted by Catalog and carried by *code*, never by the model; a "success" without a tool call is treated as failure. Each step runs under its **own 60s budget** beneath a 6-minute bus execution timeout, so a hung call (e.g. a downed service behind a proxy that queues instead of refusing) surfaces as a visible failure that triggers backoff retries (10/30/60s) and a DLQ with full record content — never a **silent false ack**. Recovery is operational by design: requeue the DLQ message from the RabbitMQ management UI and the idempotent writes converge.
- **Durable orchestration & scheduling (no cron, no lost work)** — checkout is a **Wolverine durable saga** (state in Marten, keyed by order id): the order is created `Pending`, HTTP returns immediately, and the saga drives the rest — per-item gRPC stock commit → confirm → gRPC basket clear. A **pivot rule** splits the timeline: steps *before* confirm are compensatable (`RevertCommit` + cancel on a business failure), everything *after* is forward-only (retryable) — so a post-pivot hiccup like a failed basket-clear never cancels an already-paid order. A scheduled `CheckoutTimedOut` **watchdog** compensates any run that stalls (config'd timeout). The same durable-scheduling primitive retires cron elsewhere: basket-reservation expiry is a Wolverine **`ScheduleAsync`** message (a ~10-minute safety net) that fires `ReservationExpired` to purge the cart line — not a polling sweep. Background saga steps run under a **machine identity** (`order-saga` client-credentials, no user token), and commit/revert carry the order id as an **idempotency key** so at-least-once delivery converges instead of double-committing. Decision logic lives in pure `On*` methods — unit-tested with no mocks (026/028).
- **One-command orchestration** — .NET Aspire spins up every service, gateway, Postgres, RabbitMQ, and Redis with service discovery and connection-string injection.
- **Spec-driven development** — non-trivial features go through a GitHub spec-kit flow (spec → plan → tasks → implement) governed by a project constitution.

## Architecture

```mermaid
flowchart TB
    subgraph Client
        Web["WebApp (Blazor UI + customer-service chat page)"]
    end

    Web -->|HTTP| GW["Gateway (YARP)"]
    Web -->|chat proxy| Agent["ChatAgent<br/>(Microsoft Agent Framework)"]

    Agent -->|MCP tools, per-user token| GW

    GW --> Catalog["catalog-api"]
    GW --> Basket["basket-api"]
    GW --> Order["order-api"]
    GW --> Stock["stock-api"]
    GW --> Payment["payment-api"]
    GW --> File["file-api"]
    GW --> Storefront["storefront-api"]
    GW --> Customer["customer-api"]

    IdP["Identity.Server (OpenIddict OIDC/OAuth + ASP.NET Identity)"]
    GW -.->|JWT bearer / scopes| IdP
    Agent -.->|user token| IdP

    RemotePay["Remote PaymentAgent<br/>(separate solution,<br/>A2A server)"]
    Agent -->|A2A: installment_quote<br/>amount + card BIN only| RemotePay

    Supplier["supplier-api<br/>(feed simulator)"]
    SupplierGW["supplier-gateway<br/>(boundary: pull, normalize,<br/>change gate)"]
    Ingestion["ingestion-agent<br/>(stateless consumer,<br/>4 LLM writer agents via MCP)"]
    SupplierGW -->|GET /v1/feeds| Supplier
    SupplierGW --> DB10[("supplierGatewayDb<br/>(last published snapshots)")]
    SupplierGW -->|SupplierProductSnapshotReceived| MQ
    MQ -->|queue + retry + DLQ| Ingestion
    Ingestion -->|MCP tools: upsert_brand/category/product,<br/>set_stock| Catalog & Stock

    Catalog & Basket & Order & Stock & Payment & File & Storefront -->|integration events| MQ["RabbitMQ (fanout exchanges)"]
    MQ -->|single sequential queue| Storefront

    Catalog --> DB1[("catalogDb")]
    Basket --> DB2[("basketDb")]
    Order --> DB3[("orderDb")]
    Stock --> DB5[("stockDb")]
    Payment --> DB6[("paymentDb")]
    File --> DB7[("fileDb")]
    Storefront --> DB8[("storefrontDb")]
    Customer --> DB9[("customerDb")]

    Catalog -.->|L2 cache| Redis[("Redis")]
```

Each service is a self-contained bounded context. Synchronous read/write traffic goes **client → YARP gateway → service**, secured by JWT bearer tokens with OAuth scopes issued by Identity.Server. State changes are published as **integration events** over RabbitMQ fanout exchanges; the `storefront` read model is built entirely by consuming those events on a **single sequential queue** (structurally eliminating concurrent writes to the same composite row). The **ChatAgent** reaches the services' MCP endpoints through the gateway, injecting the calling user's token at invocation time so the agent acts *as that user*. The **Supplier.Gateway** periodically pulls the supplier feed (persistent **Hangfire** `feed-pull` recurring job — cron from config, storage in a separate `hangfire` schema of `supplierGatewayDb`, dev-only dashboard at `/hangfire`, failed pulls retried at most twice — or `POST /v1/feeds/pull`), compares each record with the last published snapshot, and publishes only new/changed records as canonical events; the stateless **IngestionAgent** consumes them one message at a time and writes to Catalog/Stock through four **LLM-driven writer agents** calling their MCP tools — typed structured-output results, bounded retries, and a dead-letter queue. For instant-consistency decisions, **stock reservation** runs over a typed **gRPC** contract (`Shared/Protos`): basket/order call Stock synchronously (fail-closed — no reservation, no add-to-cart), TTL holds are swept by Hangfire, and the supplier feed is the **sole authority** for on-hand stock, which only order commits decrement.

## Tech Stack

| Area | Technology |
|------|-----------|
| Runtime | .NET 10, C# (nullable + implicit usings) |
| Orchestration | .NET Aspire (AppHost + ServiceDefaults) |
| Persistence | Marten (PostgreSQL as document / event store) |
| In-process bus & messaging | Wolverine (CQRS bus + RabbitMQ integration messaging) |
| Messaging transport | RabbitMQ (fanout exchanges) |
| Caching | HybridCache (L1 in-memory + optional Redis L2), AOP decorator |
| Identity & AuthZ | OpenIddict + ASP.NET Identity (OIDC/OAuth, scope-based) |
| API Gateway | YARP (with Aspire service discovery) |
| Sync RPC | gRPC (stock reservation — shared proto contract) |
| AI Agents | Microsoft Agent Framework + Microsoft.Extensions.AI (OpenAI), MCP |
| Cross-agent protocol | Agent2Agent (A2A) — `Microsoft.Agents.AI.A2A` client to a remote payment agent |
| UI | Blazor WebApp |
| DI | Scrutor (convention-based auto-registration) |
| Testing | xUnit + Shouldly (pure domain unit tests) |

## Services

| Project | Responsibility |
|---------|----------------|
| `catalog-api` | Products and their details (Catalog bounded context) |
| `basket-api` | User baskets and items |
| `order-api` | Order placement and lifecycle |
| `stock-api` | Product stock levels |
| `payment-api` | Payment processing |
| `file-api` | Product image storage/serving (internal) |
| `storefront-api` | Push-only composite read model (catalog + stock) |
| `customer-api` | Wallet (saved cards, tokenized — no PAN/CVV; stores the non-sensitive BIN for installment quotes) + AddressBook (Customer bounded context) |
| `reviews-api` | Verified-purchase product reviews (1-5 stars) — purchase proof via sync gRPC to Order, async AI moderation (auto-hide), rating summary event to Storefront |
| `supplier-api` | Supplier feed simulator — one typed JSON endpoint, no DB, no bus |
| `supplier-gateway` | Supplier boundary — Hangfire-scheduled feed pull, normalizes to the canonical event, publishes only new/changed records (snapshots in `supplierGatewayDb`) |
| `gateway` | YARP reverse proxy / single entry point |
| `identity-server` | OpenIddict + ASP.NET Identity — OIDC/OAuth authority |
| `chat-agent` | AI shopping assistant — MCP client over the gateway + A2A client to the remote payment agent (installment quotes) |
| `ingestion-agent` | Stateless supplier-ingestion consumer (per-message Agent Framework Workflow, four LLM writer agents over MCP, no database) |
| `ecommerce-web` | Blazor storefront UI with a full-page customer-service chat (`/musteri-hizmetleri`) |

Shared foundations live under `src/others`: `Common` (domain building blocks, results, caching), `Shared` (integration-event contracts), and `Identity.Server`.

## Key Design Decisions

- **A microservice = a bounded context.** The boundary is physical and hard: separate database, separate schema, separate domain model. Services never share a database or leak one context's model into another.
- **Aggregates own their invariants.** New rules go on the aggregate method, never in a handler. Collections are private and exposed read-only; mutation only flows through behavior methods.
- **Result over exceptions.** All handlers, aggregate methods, and endpoints return a `Result`; endpoints translate `IsSuccess` into `Ok`/`BadRequest`.
- **Caching is a decorator, not middleware.** Wolverine's `Before/After` hooks can't return a value on short-circuit, so caching is implemented as a transparent `IMessageBus` decorator (Scrutor `Decorate`) — endpoints and handlers stay unaware.
- **The agent adds no business logic.** MCP tools re-invoke the same Wolverine command/query via `IMessageBus`; they only add an LLM-friendly name and description.
- **A2A is a consumed contract, not a merge.** The remote payment agent lives in its own solution with its own database; this system *consumes* it over A2A exactly the way it consumes integration events, MCP, and gRPC — a deliberate contract, never a shared model or DB. The quote/charge boundary is drawn on purpose: installment **quote** needs only the bank (BIN + amount), so that is all that is sent; **charge** (which needs card identity / a token) is a separate future feature — keeping the token off the A2A/LLM path entirely for this one.
- **LLM writers, deterministic guardrails.** The ingestion write path is deliberately LLM-driven — otherwise MCP is an empty ritual (a plain HTTP client with extra steps); here a model actually reads the tool schemas and calls them. The non-determinism is fenced in: per-writer tool allowlists, temperature 0, typed structured-output results, `ProductId` minted by Catalog and carried by code, success-without-a-tool-call treated as failure, and per-step time budgets.
- **Sync RPC only where a yes/no must be immediate.** Stock reservation (basket/order → stock) is the one sanctioned synchronous channel: a typed gRPC contract, scope-protected, fail-closed. DB isolation still holds — callers hit Stock's API, never its database.
- **Idempotency over transactions across services.** Cross-context writes can't share a transaction. The ingestion flow converges instead: SKU-keyed upsert, absolute `set_stock`, a transactional outbox at the gateway (Wolverine + Marten — event and snapshot commit atomically), at-least-once delivery with bounded retries + DLQ.
- **No roles — scopes only.** Role-based authorization was intentionally removed; authorization is purely scope-based. Reads (stock, storefront) are anonymous; tokens matter on the shopping write path.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Docker (Aspire provisions PostgreSQL, RabbitMQ, and Redis as containers)

### Run the whole system

Always start the distributed system through the **Aspire AppHost** — services discover each other, their databases, and RabbitMQ via Aspire service discovery. Running a single API standalone will fail to resolve its dependencies.

```bash
# From the repo root
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

This brings up every service, the YARP gateway, Identity.Server, the Blazor UI, the ChatAgent, plus PostgreSQL (with pgAdmin), RabbitMQ (with the management plugin), and Redis. The **Aspire dashboard** opens with a live view of every resource, its logs, and its endpoints.

> Identity.Server must run over **HTTPS** (its `SameSite=None; Secure` cookies loop forever on plain HTTP).

Both agents need OpenAI credentials in user-secrets — **IngestionAgent fails fast at startup without them**:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "<key>"   --project src/agents/ChatAgent/ChatAgent.csproj
dotnet user-secrets set "OpenAI:ApiKey" "<key>"   --project src/agents/IngestionAgent/IngestionAgent.csproj
dotnet user-secrets set "OpenAI:Model"  "<model>" --project src/agents/IngestionAgent/IngestionAgent.csproj
```

### Build & test

```bash
# Build the whole solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```

## Project Structure

```
src/
  aspire/        AppHost (orchestration) + ServiceDefaults
  services/      basket, catalog, customer, file, gateway, order,
                 payment, procurement, reviews, stock, storefront, supplier
  others/        Common, Shared, Identity.Server
  agents/        ChatAgent (MCP client, LLM) + IngestionAgent (Workflows, LLM writers)
  ui/            WebApp (Blazor)
tests/           Per-service domain unit tests (xUnit + Shouldly)
.specify/        Spec-driven development setup (spec-kit)
specs/           Feature specs / plans / tasks
```

A single service follows a **Vertical Slice** layout — code grouped by domain feature, not technical layer:

```
Domains/<Aggregate>/
  <Aggregate>.cs                  # rich aggregate root (factory + behavior methods)
  <Aggregate>EndpointExtension.cs # Minimal API endpoint mapping
  <Aggregate>McpTools.cs          # MCP tool wrappers for this aggregate
  Features/
    Commands/                     # write slices  (IDocumentSession, [Transactional])
    Queries/                      # read slices   (read-only)
    Agent/                        # agent-facing slices (exposed via MCP)
```

## DropShop payment-gateway integration (032/033 + vault)

This storefront registers itself as a merchant with the external **DropShop** payment gateway
(a separate solution, consumed as a contract — never a shared DB/model) and then uses the
gateway's **card vault** for PCI-safe card storage. The earlier `GatewayOnboarding/` module
(descriptor + HTTP-01 challenge + in-memory stores) was **removed** — the gateway switched to
push-inline registration and human admin review, so the flow is now chat-driven:

- **Admin onboarding via chat (032)** — an admin-only Razor page (`Pages/Admin/Onboarding`) talks
  to the ChatAgent **`admin` persona** through the BFF SSE proxy (`/chat/admin/stream`, role-gated).
  The persona's tool set is a WebApp-hosted onboarding MCP surface that wraps the gateway calls:
  `submit_registration` (pushes domain/legalName/taxId/contactEmail/webhookUrl straight to the
  gateway's Merchant.Api `/mcp`) and `registration_status`. Gateway calls run with the machine
  identity (`ecommerce-onboarding` client_credentials) — the admin's user token never leaves this
  system.
- **MerchantKey handling (033)** — after gateway-side approval, the gateway's activation page shows
  the **MerchantKey** once; the admin pastes `{merchantId, merchantKey}` into the same Onboarding
  page, which persists it in **Customer.Api** (`MerchantInformation` aggregate — no more in-memory
  store). The key is the gateway OAuth `client_secret` (`client_id = merchantId`) and only ever
  goes to the gateway's `connect/token`.
- **Card vault client (017 consumer)** — Customer.Api's Wallet tokenizer now calls the gateway's
  vault (`merchants/{merchantId}/vault/cards`, scope `cards.write`, merchant-scoped token acquired
  with the stored MerchantKey): raw PAN goes straight to the gateway, only `card_…` token + brand +
  last4 + BIN are kept locally. Config via `DropShopVaultOption` (Options pattern).
- **Commission negotiation counterpart** — merchant-side commission decisions stay on the gateway's
  anonymous decision links (mailed accept/reject pages); this system does not call the gateway's
  commission API.

## Chat payment + order completion (038/039)

A shopper can pay and place an order **end-to-end from chat**, never touching a screen — but payment
trust is never handed to the LLM. Two complementary paths:

- **Installment quote (038, A2A)** — "list my installments" goes ChatAgent → **A2A** → the gateway's
  remote `Payment.Agent` → gateway `/mcp`. Read-only; the LLM shows returned options verbatim, never
  invents amounts. The buyer/vault-token come from Customer.Api `get_payment_context` and are passed
  **verbatim** into the A2A request (the cart line is synthesized gateway-side).
- **Order completion (039, server orchestration)** — on user confirmation the LLM only picks the tool
  and passes `cardId?` + `installment`; **everything else is server-side**. The `place_order` MCP tool
  (Order.Api) reads the cart (Basket `GetBasketItems` gRPC, server-authoritative), the payment context
  (Customer `/internal/payment-context` REST), derives a **correlation-key** (HMAC of
  userId+cart+installment — ownership + idempotency), then charges PaymentGateway over **structural
  REST** (Principle I: non-agent code cannot drive A2A/MCP) and starts the Order + CheckoutSaga.

Why the charge moved off the LLM-A2A step: `paymentId` is not proof of success, and a hallucinated
"success" would mean a free order. So the charge + verify are **structural server-to-server**; the LLM
only triggers. The flow is a durable `PaymentAttempt` (Marten doc, sync charge outcome +
`ScheduleAsync` bounded reconcile) so a lost response is recovered by re-deriving the same
correlation-key and retrieving from the gateway — never a double charge. Card add/remove is **refused
in chat** (FR-013, security); only card *selection* is allowed and the PAN never reaches the LLM.

The gateway side of this (idempotent structural charge + retrieve, X-Api-Key auth) lives in the
separate **PaymentGateway** repo (feature `039-structural-charge-retrieve`) and is consumed here as a
contract via `PaymentGatewayClient` (`PaymentGatewayOption`: base url + merchant API key).

## Notes

- **Central Package Management** is enabled — package versions live in `Directory.Packages.props`, not individual `.csproj` files.
- Non-trivial features are built with a **spec-kit** flow (`/speckit-*`), governed by a project constitution under `.specify/memory/`. Artifact depth scales with feature size.

---

*Built as a hands-on exploration of Domain-Driven microservices and AI agents on the .NET stack.*