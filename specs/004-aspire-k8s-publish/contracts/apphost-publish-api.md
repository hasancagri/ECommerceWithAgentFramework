# Contract: AppHost Kubernetes Publish API

Bu feature'ın dış yüzeyi bir HTTP endpoint değil, **AppHost kompozisyon API'si** +
**publish CLI davranışı** + **üretilen chart'ın deploy kontratı**dır.

## C1 — AppHost API kullanımı (kod sözleşmesi)

```csharp
using Aspire.Hosting.Kubernetes;            // AddKubernetesEnvironment
using Aspire.Hosting.Kubernetes.Resources;  // Deployment, workload tipleri

var k8s = builder.AddKubernetesEnvironment("k8s");   // compute environment

var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WithReference(rabbit).WithReference(redis)
    .WaitFor(catalogDb).WaitFor(rabbit).WaitFor(redis)
    .PublishAsKubernetesService(resource =>          // opsiyonel özelleştirme
    {
        if (resource.Workload is Deployment deployment)
            deployment.Spec.Replicas = 3;
    });
```

**Sözleşme kuralları:**
- `AddKubernetesEnvironment` publish'ten önce AppHost'a bir kez eklenir.
- `PublishAsKubernetesService` yalnızca özelleştirme gerektiren kaynaklarda çağrılır;
  çağrılmayan kaynak varsayılan (replicas=1) Deployment üretir.
- Mevcut `WithReference`/`WaitFor` zinciri korunur — publish onları bağlantı/sıralama
  bilgisi olarak kullanır.
- Preview API → csproj `NoWarn` = `ASPIRECOMPUTE003` (+ build'de çıkan diğer `ASPIRE*`).

## C2 — Publish CLI kontratı

```bash
aspire publish --project src/aspire/AppHost/AppHost.csproj -o ./artifacts/k8s
```

- **Girdi**: AppHost kaynak grafiği.
- **Çıktı**: `-o` dizinine bir **Helm chart** (Chart.yaml, values.yaml, templates/).
- **Determinizm**: aynı AppHost → aynı chart (SC-005). Çıktı dizini repoya commit
  edilmez (türetilmiş artefakt); `.gitignore`'a eklenir.

## C3 — Deploy kontratı (kind + helm)

Tüm `kubectl`/`helm` komutları `--context kind-ecommerce` ile hedeflenir (yanlış
cluster'a apply riskini sıfırlar; Docker Desktop K8s context'i ayrıdır).

```bash
# 0) (bir kez) kind kurulumu
brew install kind

# 1) cluster — 1 control-plane + 2 worker
kind create cluster --name ecommerce --config deploy/k8s/kind-cluster.yaml

# 2) ingress-nginx (identity.local için; kind provider manifesti)
kubectl --context kind-ecommerce apply -f <ingress-nginx kind manifest>

# 3) imajları build + node'lara yükle
kind load docker-image --name ecommerce <her-servis-imajı>

# 4) chart'ı deploy
helm --kube-context kind-ecommerce install ecommerce ./artifacts/k8s \
     -f deploy/k8s/values.local.yaml

# 5) doğrula
kubectl --context kind-ecommerce get pods           # tümü Running
kubectl --context kind-ecommerce get pods -o wide    # replicas 2 worker'a dağılmış
```

**Ön koşullar (dev makinesi):** docker (çalışıyor ✓), kubectl ✓, helm ✓, aspire ✓;
kind (kurulacak); `/etc/hosts` → `127.0.0.1 identity.local`.

## C4 — Görev dağılımı (implement fazı)

| Adım | Kim | Not |
|---|---|---|
| kind kur, cluster oluştur | Claude (onayla) | `brew install kind` + `kind create` |
| ingress-nginx apply | Claude | manifest apply |
| imaj build + `kind load` | Claude | Aspire publish imajları |
| `helm install` + `kubectl` doğrulama | Claude | headless |
| token/endpoint smoke (curl) | Claude | API tarafı headless |
| `/etc/hosts` düzenleme | **Kullanıcı** | sudo gerektirir |
| self-signed cert'e güven | **Kullanıcı** | tarayıcı/OS keychain |
| tarayıcıda login akışı gözlemi | **Kullanıcı** | OIDC redirect + cookie |

## C5 — Kabul kriterleri (spec ile izlenebilirlik)

| Kontrat | Spec | Doğrulama |
|---|---|---|
| Her proje kaynağı → workload | FR-001, SC-001 | `helm template` çıktısında sayım |
| `WithReference` → bağlantı | FR-002 | env/ConfigMap referansları |
| replicas kod-ile | FR-003, SC-002 | manifest `spec.replicas == 3` |
| Varsayılan replica=1 | FR-004 | özelleştirilmemiş servis |
| Deterministik | FR-005, SC-005 | iki publish diff'i boş |
| Deploy edilebilir | FR-006, SC-006 | pod'lar Running |
| env→ConfigMap/Secret | FR-007 | Secret/ConfigMap kaynakları |
| Yerel run bozulmaz | FR-008, SC-004 | `aspire run` regresyonsuz |
| kind'a deploy | FR-011, SC-006 | uçtan uca smoke |
| imaj kind'a yüklenir | FR-012 | `kind load` + pull policy |
| replica 2 worker'a dağılır | FR-013, SC-007 | `get pods -o wide` |
| issuer eşleşmesi | (R8) | login + korumalı endpoint 200 |