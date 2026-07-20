---
description: "Task list — Aspire Native Kubernetes Publish"
---

# Tasks: Aspire Native Kubernetes Publish

**Input**: Design docs from `specs/004-aspire-k8s-publish/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Otomatik test istenmedi (infra feature; domain unit testi uygulanamaz).
Doğrulama quickstart.md + `helm template`/`kubectl` ile manueldir.

**Organization**: Görevler user story'ye göre gruplu — her story bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1/US2/US3 — spec.md user story eşlemesi

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Paket + repo iskeleti; kod davranışı değişmez.

- [X] T001 `Directory.Packages.props`'a `Aspire.Hosting.Kubernetes` `13.3.5-preview.1.26270.6` PackageVersion ekle
- [X] T002 `src/aspire/AppHost/AppHost.csproj`'a sürümsüz `PackageReference` + `<NoWarn>ASPIRECOMPUTE003;$(NoWarn)</NoWarn>` ekle
- [X] T003 [P] `deploy/k8s/kind-cluster.yaml` oluştur: 1 control-plane + 2 worker, control-plane'de 80/443 extraPortMappings
- [X] T004 [P] `.gitignore`'a publish çıktı dizinini ekle (`artifacts/k8s*` — türetilmiş, commit edilmez)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Publish'i mümkün kılan çekirdek; tüm story'lerden önce gelir.

**⚠️ CRITICAL**: Bu tamamlanmadan hiçbir user story çalışamaz.

- [X] T005 `src/aspire/AppHost/AppHost.cs`'te `var k8s = builder.AddKubernetesEnvironment("k8s");` ekle (using'ler dahil)
- [X] T006 `dotnet build` ile AppHost'un 0 hata derlendiğini doğrula (Experimental uyarıları NoWarn ile bastırılmış)

**Checkpoint**: AppHost K8s'e publish edilebilir durumda.

---

## Phase 3: User Story 1 — Tüm sistemi tek publish ile manifest'e (Priority: P1) 🎯 MVP

**Goal**: `aspire publish` ile tüm kaynaklar (12 proje + 3 backing) Helm chart'a dönüşür.

**Independent Test**: `helm template` çıktısında her kaynağa karşılık bir workload
görülür; cluster gerekmez.

- [X] T007 [US1] `aspire publish --project src/aspire/AppHost/AppHost.csproj -o ./artifacts/k8s` çalıştır; Helm chart üretimini doğrula
- [X] T008 [US1] `helm lint ./artifacts/k8s` + `helm template ./artifacts/k8s`: şema hatasız geçtiğini ve 12 proje + 3 backing kaynağın her biri için workload üretildiğini doğrula (FR-001, SC-001, SC-003 client-side)
- [X] T009 [US1] `WithReference` bağımlılıklarının chart'ta env/ConfigMap bağlantısı olarak yansıdığını doğrula (FR-002)
- [X] T010 [US1] İkinci publish (`-o ./artifacts/k8s-2`) + `diff -r` ile determinizmi doğrula (FR-005, SC-005)

**Checkpoint**: Chart üretimi çalışıyor — MVP hazır (deploy henüz US3'te).

---

## Phase 4: User Story 2 — Servis başına replica (Priority: P2)

**Goal**: Seçili servis(ler) `PublishAsKubernetesService` ile replica alır; kalanı default 1.

**Independent Test**: Manifest'te ayarlanan servisin `spec.replicas` doğru; cluster gerekmez.

- [X] T011 [US2] `AppHost.cs`'te bir servise (ör. `catalog-api`) `PublishAsKubernetesService(r => { if (r.Workload is Deployment d) d.Spec.Replicas = 3; })` ekle
- [X] T012 [US2] Yeniden publish; `helm template` çıktısında ilgili servis `replicas: 3`, özelleştirilmemiş servis `replicas: 1` doğrula (FR-003, FR-004, SC-002)

**Checkpoint**: replica kod-ile ayarlanabiliyor; manifest yansıtıyor.

---

## Phase 5: User Story 3 — kind'a deploy + doğrulama (Priority: P3)

**Goal**: Chart, 3 node'lu kind cluster'a deploy edilir; sistem uçtan uca çalışır.
Bu story R8 (Identity), R4 (Postgres), R5/R6 (imaj/secret) feasibility'sini içerir.

**Independent Test**: Chart lint/dry-run şema-geçerli; tüm pod'lar Running; login akışı
çalışır; replica'lar 2 worker'a dağılır.

**⚠️ İlk kritik iş T015 (Identity issuer) — deploy'dan önce çözülmeli.**

- [ ] T013 [US3] `kind` kur (`brew install kind`); `kind create cluster --name ecommerce --config deploy/k8s/kind-cluster.yaml`; 3 node `Ready` doğrula
- [ ] T014 [US3] `ingress-nginx` (kind provider manifesti) apply; controller `Running` doğrula
- [ ] T015 [US3] R8: `src/others/Identity.Server`'da `IssuerUri=https://identity.local` sabitle; `Config.cs` Redirect/PostLogout URI'larını `identity.local`'e çevir; servis `IdentityOption.Address=http://identity-server` olacak şekilde ayarla
- [ ] T016 [US3] R4: Postgres kalıcılık (PVC) sağla + 9 mantıksal DB için idempotent init (Job/init-script); `AddPostgres(...).WithDataVolume()`'un PVC üretip üretmediğini publish çıktısında kontrol et, gerekirse `PublishAsKubernetesService` ile PVC ekle
- [ ] T017 [US3] Servis imajlarını build et; `kind load docker-image --name ecommerce <imaj>` ile node'lara yükle (FR-012)
- [ ] T018 [US3] `deploy/k8s/values.local.yaml` yaz: publish çıktısındaki imaj repo/tag, `imagePullPolicy: IfNotPresent`, Secret'lar (Postgres/RabbitMQ/OpenAI), issuer host (R5/R6/FR-007)
- [ ] T019 [US3] Önce `helm install --dry-run` ile server-side şema doğrula (SC-003); sonra `helm --kube-context kind-ecommerce install ecommerce ./artifacts/k8s -f deploy/k8s/values.local.yaml`; `kubectl get pods` tümü Running (FR-006, SC-006)
- [ ] T020 [US3] Identity smoke: `curl -sk https://identity.local/.well-known/openid-configuration` → issuer `https://identity.local`; token ile korumalı endpoint 200, token'sız 401 (R8/INV-1)
- [ ] T021 [US3] (Kullanıcı) `/etc/hosts` → `127.0.0.1 identity.local`; self-signed cert'e güven; tarayıcıda WebApp login akışını gözle (login döngüsü YOK)
- [ ] T022 [US3] `kubectl get pods -o wide` ile replicas>1 servisin pod'larının 2 worker'a dağıldığını doğrula (FR-013, SC-007)

**Checkpoint**: Sistem kind üzerinde uçtan uca çalışıyor.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T023 [P] `deploy/k8s/README.md` + opsiyonel `deploy/k8s/deploy.sh` (publish→kind load→helm install akışı) yaz
- [ ] T024 Regresyon: `dotnet run --project src/aspire/AppHost/AppHost.csproj` ile yerel run'ın bozulmadığını doğrula (FR-008, SC-004)
- [ ] T025 `quickstart.md`'yi baştan sona çalıştırıp tüm adımların geçtiğini doğrula
- [ ] T026 [P] Değişiklikleri özetle; gerekirse CLAUDE.md'ye kısa "K8s publish" notu ekle

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen başlar.
- **Foundational (P2)**: Setup'a bağlı; tüm story'leri BLOKLAR.
- **US1 (P3)**: Foundational sonrası. MVP.
- **US2 (P4)**: Foundational sonrası; US1'den bağımsız test edilir (manifest inceleme).
- **US3 (P5)**: US1'e bağlı (chart gerekir). replica-dağılım alt-kontrolü (T022) US2'ye bağlı.
- **Polish (P6)**: istenen story'ler bitince.

### User Story Dependencies

- **US1**: Foundational sonrası; başka story'ye bağlı değil.
- **US2**: Foundational sonrası; bağımsız (yalnız manifest üretimi).
- **US3**: US1 (chart) gerekir; T022 için US2 replica özelleştirmesi gerekir.

### Within US3 (sıra kritik)

- T015 (Identity issuer) deploy'dan (T019) ÖNCE — en riskli, önce çözülür.
- T013→T014 (cluster→ingress) T017→T018→T019'dan önce.
- T016 (Postgres PVC) T019'dan önce.

### Parallel Opportunities

- Setup: T003, T004 paralel.
- US1: T007→T008/T009/T010 sıralı (aynı publish çıktısı).
- US3 çoğu görev sıralı (aynı cluster durumu); T023/T026 polish'te paralel.

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1.
4. **DUR ve DOĞRULA**: `helm template` ile tüm kaynakların workload'a döndüğünü gör.
5. Bu, cluster olmadan gösterilebilir bir MVP'dir.

### Incremental Delivery

1. Setup + Foundational → publish altyapısı hazır.
2. US1 → chart üretimi (MVP).
3. US2 → replica kontrolü.
4. US3 → gerçek kind deploy + uçtan uca (en ağır; R8 önce).

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- US3 gerçek altyapı işidir; cluster/ingress/imaj/helm = Claude (onayla), sudo/cert/tarayıcı = kullanıcı (bkz contracts C4).
- İlk feasibility = T015 (IssuerUri pin + login). Sonra T016 (PVC/DB init), T017-T018 (imaj/secret eşleme).
- Publish çıktısı (`artifacts/`) commit edilmez; repoda yalnız `deploy/k8s/` girdileri.