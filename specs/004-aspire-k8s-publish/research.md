# Phase 0 Research: Aspire Native Kubernetes Publish

Kaynaklar: aspire.dev/integrations/compute/kubernetes, aspire.dev/deployment/kubernetes,
Aspire 9.3 release notes, NuGet Aspire.Hosting.Kubernetes. Doğrulama tarihi 2026-07-20.

## R1 — Publish mekanizması ve environment kaydı

- **Decision**: AppHost'ta `var k8s = builder.AddKubernetesEnvironment("k8s");` eklenir.
  Manifest üretimi `aspire publish -o <dir>` CLI komutuyla; çıktı bir **Helm chart**
  (templates + values.yaml).
- **Rationale**: Native, desteklenen yol; kaynak grafiği (referans/bağlantı/env) chart'a
  otomatik yansır. Tek doğruluk kaynağı AppHost kalır (drift önlenir).
- **Alternatives**: Aspir8/`aspirate` (3rd-party) — reddedildi (native değil, önceki
  denemede karışıklık). Elle YAML — reddedildi (drift, bakım maliyeti).

## R2 — Paket sürümü ve Experimental bastırma

- **Decision**: `Aspire.Hosting.Kubernetes` = **`13.3.5-preview.1.26270.6`**
  (`Directory.Packages.props`'a; AppHost.csproj sürümsüz `PackageReference`). AppHost.csproj
  `<NoWarn>ASPIRECOMPUTE003;$(NoWarn)</NoWarn>` — build'de çıkan diğer `ASPIRE*` Experimental
  ID'leri de eklenir.
- **Rationale**: Stable yok; tüm 13.3.x yalnız `-preview`. SDK 13.3.5 hizalaması
  ([[aspire-version-alignment-dcp]]) DCP/sürüm çakışmasını önler.
- **Alternatives**: 13.4.6-preview (en yeni) — reddedildi (SDK 13.3.5 ile hizalı değil).

## R3 — Servis özelleştirme (replicas)

- **Decision**: `PublishAsKubernetesService(resource => { if (resource.Workload is
  Deployment d) d.Spec.Replicas = 3; })`; `using Aspire.Hosting.Kubernetes.Resources;`.
  Özelleştirilmemiş servis replica=1.
- **Rationale**: Kullanıcının örnek API'siyle birebir; tek yerde, kod-olarak ayar.
- **Alternatives**: Helm values'ta elle replica — reddedildi (tek-kaynak ilkesini bozar).

## R4 — Backing servisler (Postgres/RabbitMQ/Redis) + kalıcılık

- **Decision**: Üçü de cluster-içi workload olarak publish edilir. Postgres için
  **kalıcılık (PVC)**; RabbitMQ/Redis efemeral kabul edilir (restart'ta durum kaybı tolere).
- **Rationale**: Spec kararı "cluster içinde"; yerel `kind` self-contained olur.
- **OPEN / implement-time**: `AddPostgres(...).WithDataVolume()`'un chart'ta PVC'ye
  çevrilip çevrilmediği doğrulanır; çevirmiyorsa `PublishAsKubernetesService` içinde
  StatefulSet/PVC eklenir. 9 mantıksal DB tek Postgres instance'ında → DB oluşturma
  idempotent init (Job/init-script) ile (Postgres'te "CREATE DB IF NOT EXISTS" yok).

## R5 — İmajlar: kind (registry yok)

- **Decision**: İmajlar yerelde build edilir; `kind load docker-image <img>` ile
  node'lara yüklenir. `values.local.yaml` ile `imagePullPolicy: IfNotPresent` (veya
  `Never`) ve imaj repo/tag override; harici registry yok.
- **Rationale**: Yerel, offline, ücretsiz; GHCR/registry gereksinimini kaldırır.
- **Alternatives**: Yerel registry container — reddedildi (fazladan kurulum); GHCR +
  imagePullSecret — reddedildi (yerel hedef için gereksiz).
- **OPEN**: Aspire'ın imaja verdiği ad/tag + chart'ın parametrelemesi implement'te
  görülüp `values.local.yaml`'a yansıtılır.

## R6 — Bağlantı dizileri / env → ConfigMap/Secret

- **Decision**: Aspire env/bağlantıları chart'ta ConfigMap/Secret olarak üretir.
  Sabit sırlar (OpenAI, Postgres/RabbitMQ parolası) Secret'a; `values.local.yaml` ile
  sağlanır, repoya düz-metin commit edilmez.
- **OPEN**: Üretilen Secret adı/anahtarı implement'te doğrulanıp `values` ile eşlenir.

## R7 — Servis discovery / DNS eşlemesi

- **Decision**: `WithReference` bağımlılıkları cluster-içi Service adlarına çözülür
  (Service adı = resource adı, ör. `catalog-api`). Gateway (YARP) + WebApp bu isimlerle konuşur.
- **Rationale**: Aspire service discovery publish'te K8s Service DNS'ine map'lenir.

## R8 — Identity.Server HTTPS + tek issuer (EN KRİTİK — ÇÖZÜLDÜ)

Sorun: servisler token'ı `Authority=IdentityOption.Address`, `RequireHttpsMetadata=false`,
**`ValidateIssuer=true`** ile doğrular (`AuthenticationExtension.cs:20-33`). Token'ın `iss`
claim'i her doğrulayıcıda birebir eşleşmeli. Tarayıcı Identity'ye *dışarıdan* (HTTPS,
Secure cookie zorunlu — [[identity-server-https]]), servisler *cluster-içinden* (Service DNS)
ulaşır → farklı hostname → issuer uyuşmazsa 401 / login döngüsü.

- **Decision**:
  1. IdentityServer'da **`IssuerUri = https://identity.local`** sabitlenir → token `iss`
     erişim yolundan bağımsız sabit kalır.
  2. Tarayıcı-yüzü: **`ingress-nginx` + self-signed TLS**, host `identity.local`.
     `kind-cluster.yaml`'a control-plane 80/443 `extraPortMappings`; dev makinesinde
     `/etc/hosts` → `127.0.0.1 identity.local`. `RedirectUris`/`PostLogoutRedirectUris`
     `https://identity.local/...` olur.
  3. Servisler: `IdentityOption.Address = http://identity-server` (cluster-içi HTTP).
     `RequireHttpsMetadata=false` zaten açık → discovery doc HTTP'den çekilir; doc'un
     `issuer` alanı `https://identity.local` olduğu için token `iss` eşleşir. **Servisler
     self-signed cert'e güvenmek zorunda değildir** (içeride HTTP metadata).
- **Rationale**: Tek doğrulama noktası olarak `iss` sabitlenir; TLS/self-signed sürtünmesi
  yalnız tarayıcıda kalır (dev bir kez güvenir). Ingress portsuz temiz issuer verir —
  NodePort'un port'lu/kırılgan issuer'ına tercih edildi (kullanıcı onayı).
- **OPEN / implement-time**: `IssuerUri` pinlemesinin discovery `issuer`'ını gerçekten
  `https://identity.local` yaptığı, WebApp login akışının (authorization code + cookie)
  uçtan uca çalıştığı **ilk feasibility adımı** olarak doğrulanır.

## R9 — kind cluster (1 control-plane + 2 worker)

- **Decision**: `deploy/k8s/kind-cluster.yaml` — `kind: Cluster`, 1 `control-plane` +
  2 `worker`; control-plane'de 80/443 `extraPortMappings` (ingress için). Kurulum
  `kind create cluster --config kind-cluster.yaml`.
- **Rationale**: Spec hedefi; replica'lar 2 worker'a dağılır (varsayılan scheduler;
  gerekirse topologySpreadConstraints).
- **Alternatives**: Docker Desktop K8s (tek node) — elendi; minikube --nodes 3 — eşdeğer,
  kind seçildi (`kind load` imaj akışı + ingress-nginx yaygın kombinasyon).

## Kalan açık maddeler (implement feasibility sırası)

1. **R8** — `IssuerUri` pin + ingress + login akışı uçtan uca (ilk, en kritik).
2. **R4** — Postgres PVC + 9-DB idempotent init.
3. **R5/R6** — üretilen imaj adı + Secret adı → `values.local.yaml` eşlemesi.

Bunlar kod değil, publish çıktısı/deploy görülünce netleşen doğrulama adımlarıdır;
tasks.md'de "publish çıktısını incele → values/manifest ayarla" olarak sıralanır.