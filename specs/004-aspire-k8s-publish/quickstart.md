# Quickstart: Aspire → kind Kubernetes Deploy Doğrulaması

Bu rehber feature'ın uçtan uca çalıştığını kanıtlar. Uygulama detayları `tasks.md` +
implement fazındadır; burası **çalıştır/doğrula** kılavuzudur. Kontrat: [contracts/apphost-publish-api.md](./contracts/apphost-publish-api.md).

## Ön koşullar

- docker (çalışır durumda), kubectl, helm, aspire CLI — **kurulu** ✓
- kind — `brew install kind` (implement'te yapılır)
- Docker Desktop yerleşik Kubernetes **kapalı** (context karışıklığını önlemek için)
- `/etc/hosts` → `127.0.0.1 identity.local` (kullanıcı, sudo)

## Adım 0 — AppHost hazır mı (kod tarafı)

`src/aspire/AppHost/AppHost.cs` içinde `AddKubernetesEnvironment("k8s")` var ve build
temiz (`dotnet build`). Ölçeklenecek servis(ler) `PublishAsKubernetesService` ile
replica alır.

**Beklenen:** build 0 hata (Experimental uyarıları `NoWarn` ile bastırılmış).

## Adım 1 — Publish (chart üret)

```bash
aspire publish --project src/aspire/AppHost/AppHost.csproj -o ./artifacts/k8s
```

**Beklenen:** `./artifacts/k8s` altında bir Helm chart (Chart.yaml + templates + values.yaml);
her proje/backing kaynağı için bir workload template'i. (FR-001, SC-001)

## Adım 2 — cluster + ingress

```bash
kind create cluster --name ecommerce --config deploy/k8s/kind-cluster.yaml
kubectl --context kind-ecommerce get nodes            # 1 control-plane + 2 worker
kubectl --context kind-ecommerce apply -f <ingress-nginx kind manifest>
```

**Beklenen:** 3 node `Ready`; ingress-nginx controller `Running`. (FR-013)

## Adım 3 — imajlar + deploy

```bash
# Aspire publish imajları build eder; her imajı node'lara yükle
kind load docker-image --name ecommerce <servis-imajları>

helm --kube-context kind-ecommerce install ecommerce ./artifacts/k8s \
     -f deploy/k8s/values.local.yaml
```

**Beklenen:** helm release kurulu; pod'lar çekiliyor (imagePullPolicy IfNotPresent). (FR-012)

## Adım 4 — doğrulama

```bash
kubectl --context kind-ecommerce get pods              # tümü Running
kubectl --context kind-ecommerce get pods -o wide      # replicas 2 worker'a dağılmış
```

**Beklenen:**
- Tüm ~15 pod `Running`. (SC-006)
- replicas=3 verilen servisin 3 pod'u iki worker'a dağılmış. (SC-002, SC-007)

## Adım 5 — Identity / issuer smoke (en kritik)

```bash
# discovery issuer https://identity.local mi?
curl -sk https://identity.local/.well-known/openid-configuration | grep -o '"issuer":"[^"]*"'
```

**Beklenen:** `"issuer":"https://identity.local"`. Ardından bir korumalı API'ye geçerli
token ile 200, token'sız 401. (R8 / INV-1)

**Kullanıcı (tarayıcı):** WebApp açılır → login → OIDC redirect `identity.local` üzerinden
döner, cookie set edilir, korumalı sayfa görülür (login döngüsü YOK).

## Adım 6 — regresyon (yerel run bozulmadı mı)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

**Beklenen:** sistem eskisi gibi yerelde ayağa kalkar — publish additive, F5 deneyimi
değişmez. (FR-008, SC-004)

## Adım 7 — determinizm

```bash
aspire publish --project src/aspire/AppHost/AppHost.csproj -o ./artifacts/k8s-2
diff -r ./artifacts/k8s ./artifacts/k8s-2
```

**Beklenen:** anlamlı fark yok. (FR-005, SC-005)

## Temizlik

```bash
kind delete cluster --name ecommerce
```

## Başarı özeti (spec → doğrulama)

| Kanıt | Adım |
|---|---|
| Chart üretimi | 1 |
| 3 node + ingress | 2 |
| Deploy + Running | 3–4 |
| replica dağılımı | 4 |
| issuer eşleşmesi + login | 5 |
| yerel regresyon yok | 6 |
| determinizm | 7 |