# Implementation Plan: Aspire Native Kubernetes Publish

**Branch**: `004-aspire-k8s-publish` | **Date**: 2026-07-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-aspire-k8s-publish/spec.md`

## Summary

AppHost'a native Kubernetes publish yeteneği eklenir. `AddKubernetesEnvironment("k8s")`
ile bir compute environment tanımlanır; her proje/backing kaynağı `aspire publish` ile
bir Helm chart'a dönüştürülür. Seçili servisler `PublishAsKubernetesService(...)` ile
özelleştirilir (ör. `Deployment.Spec.Replicas`). Üretilen chart, yerel bir `kind`
cluster'ına (1 control-plane + 2 worker) `helm install` ile deploy edilir; imajlar
yerel build + `kind load` ile node'lara yüklenir. Domain kodu değişmez — değişiklik
yalnızca kompozisyon kökü (AppHost) + repo-düzeyi deploy artefaktlarındadır.

## Technical Context

**Language/Version**: C# / .NET 10 (AppHost yalnızca; domain servisleri değişmez)

**Primary Dependencies**:
- `Aspire.AppHost.Sdk/13.3.5` (mevcut)
- `Aspire.Hosting.Kubernetes` **`13.3.5-preview.1.26270.6`** (preview-only; SDK ile hizalı)
- Mevcut: `Aspire.Hosting.PostgreSQL/13.3.5`, `Aspire.Hosting.Redis/13.3.5`,
  `Aspire.Hosting.RabbitMQ/9.3.0`
- Araçlar (repo dışı, geliştirici makinesinde): `aspire` CLI, `docker`, `kind`,
  `kubectl`, `helm` (v3.14+/v4)

**Storage**: Postgres cluster-içi workload + kalıcılık (PVC); RabbitMQ/Redis cluster-içi
(varsayılan efemeral kabul edilebilir, karar research.md'de).

**Testing**: Otomatik domain testi gerekmez (infra). Doğrulama: `aspire publish` →
`helm lint`/`helm template` → `kind` apply → pod'lar Running + uçtan uca smoke.

**Target Platform**: Yerel `kind` cluster — 1 control-plane + 2 worker (Docker üzerinde).

**Project Type**: Deployment/infra (AppHost kompozisyonu + repo `deploy/` artefaktları).

**Performance Goals**: N/A (dağıtılabilirlik hedefi; performans kapsam dışı).

**Constraints**:
- Publish API `[Experimental]` → `ASPIRECOMPUTE003` (ve gerekirse ilgili ASPIRE* ID'leri)
  csproj `NoWarn` ile bastırılır.
- Identity.Server HTTPS + tek issuer: cluster-içi Service DNS ile issuer/`Authority`
  eşleşmesi korunmalı (login döngüsü riski — en kritik).
- Central Package Management: yeni paket sürümü `Directory.Packages.props`'a eklenir.
- Sistem yerelde hâlâ AppHost run (F5) ile çalışmaya devam etmeli (publish additive).

**Scale/Scope**: 12 proje kaynağı (Identity, 8 API, gateway, web, agent) + 3 backing
(Postgres/RabbitMQ/Redis) + 9 mantıksal DB → ~15 workload.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bakılır.*

Anayasanın **Core Principles**'ı (I–V) domain servislerine dairdir (Bounded Context,
zengin aggregate, Vertical Slice+CQRS, Result pattern, scope-auth). Bu feature domain
kodu/aggregate/endpoint/handler'lara **dokunmaz**; değişiklik AppHost kompozisyonu ve
repo `deploy/` artefaktlarındadır. Dolayısıyla I–V için **ihlal yok / uygulanamaz**.

**Teknoloji ve Mimari Kısıtları** gate'leri (geçerli olanlar):

- ✅ **Sistem her zaman Aspire AppHost üzerinden**: publish additive; yerel run bozulmaz.
- ✅ **Central Package Management**: `Aspire.Hosting.Kubernetes` sürümü
  `Directory.Packages.props`'a eklenir, csproj sürümsüz `PackageReference` alır.
- ✅ **.NET 10 / Nullable / ImplicitUsings**: AppHost mevcut ayarları korur.
- ✅ **Artefakt Ölçekleme**: "Tam" kademe — spec + plan + research + data-model +
  contracts + quickstart üretilir (belirsizlik + yeni yüzey nedeniyle doğru kademe).

**Sonuç: GATE PASS** — Complexity Tracking gerektiren bir ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/004-aspire-k8s-publish/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 — kaynak→workload eşlemesi + özelleştirmeler
├── quickstart.md        # Phase 1 — publish + kind deploy doğrulama rehberi
├── contracts/
│   └── apphost-publish-api.md   # AppHost publish API sözleşmesi + değer/param kontratı
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/aspire/AppHost/
├── AppHost.cs                 # DEĞİŞİR: AddKubernetesEnvironment + PublishAsKubernetesService
└── AppHost.csproj             # DEĞİŞİR: Aspire.Hosting.Kubernetes PackageReference + NoWarn

Directory.Packages.props       # DEĞİŞİR: Aspire.Hosting.Kubernetes sürümü

deploy/k8s/                    # YENİ: yerel cluster + deploy yardımcıları
├── kind-cluster.yaml          # 1 control-plane + 2 worker kind config
├── values.local.yaml          # helm install için yerel değer override'ları (imaj, pull policy)
├── deploy.sh                  # publish → kind load → helm install akışı (yardımcı script)
└── README.md                  # kısa kullanım (quickstart'a link)
```

**Structure Decision**: Yeni bir .NET projesi eklenmez. Kod değişikliği yalnızca
AppHost + CPM dosyalarında; deploy yardımcıları repo kökünde `deploy/k8s/` altında
toplanır (fiziksel klasör = mantıksal amaç). `aspire publish` çıktısı (`-o` ile) repoya
commit edilmez — türetilmiş artefakttır; `deploy/k8s/` yalnızca kaynak girdileri tutar.

## Complexity Tracking

> Constitution Check ihlali yok — bu tablo boş bırakılır.