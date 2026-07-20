# Data Model: Aspire → Kubernetes Kaynak Eşlemesi

Bu feature veri şeması getirmez; "data model" burada **AppHost kaynak grafiğinin K8s
workload topolojisine eşlemesi** ve publish özelleştirme parametreleridir.

## Kaynak → Workload eşlemesi

| AppHost kaynağı | Tür | K8s workload | Service | Not |
|---|---|---|---|---|
| `identity-server` | proje | Deployment | ClusterIP + **Ingress** (identity.local) | IssuerUri=https://identity.local |
| `catalog-api` | proje | Deployment | ClusterIP | replicas özelleştirilebilir |
| `stock-api` | proje | Deployment | ClusterIP | |
| `basket-api` | proje | Deployment | ClusterIP | |
| `order-api` | proje | Deployment | ClusterIP | |
| `discount-api` | proje | Deployment | ClusterIP | |
| `file-api` | proje | Deployment | ClusterIP | dahili (görsel serve) |
| `storefront-api` | proje | Deployment | ClusterIP | |
| `payment-api` | proje | Deployment | ClusterIP | |
| `gateway` | proje | Deployment | ClusterIP (+ ileride ingress) | YARP |
| `ecommerce-web` | proje | Deployment | ClusterIP (+ ileride ingress) | WebApp |
| `chat-agent` | proje | Deployment | ClusterIP | Singleton agent |
| `postgres` | container | Deployment/StatefulSet | ClusterIP | **PVC (kalıcı)**, 9 DB |
| `rabbitmq` | container | Deployment | ClusterIP | efemeral |
| `redis` | container | Deployment | ClusterIP | efemeral (L2 cache) |

Toplam ~15 workload. Service adları = resource adları (cluster-içi DNS bunlarla çözülür).

## Publish özelleştirme parametreleri (AppHost'ta kod)

| Parametre | Nerede | Değer | Etki |
|---|---|---|---|
| Compute environment | `builder.AddKubernetesEnvironment("k8s")` | — | publish hedefini K8s yapar |
| `Deployment.Spec.Replicas` | `PublishAsKubernetesService` callback | int (varsayılan 1) | pod kopya sayısı |
| (opsiyonel) resource limits | aynı callback | cpu/mem | ileride, kapsam dışı v1 |

## Değer/override kontratı (`values.local.yaml`)

Helm chart parametreleri deploy anında override edilir (repoya sır commit edilmez):

| Değer | Amaç |
|---|---|
| imaj repo/tag | kind'a yüklenen yerel imajları işaret eder |
| `imagePullPolicy` | `IfNotPresent`/`Never` (registry yok) |
| Postgres/RabbitMQ parolası | Secret'a enjekte |
| OpenAI API anahtarı | chat-agent Secret'ı |
| `IssuerUri` / Identity host | `https://identity.local` |

## İlişkiler / bağımlılıklar (chart'a yansıyan)

- Her API → `postgres` (kendi DB'si) + çoğu → `rabbitmq`; catalog + `redis`.
- `gateway` → tüm API'ler + `identity-server`.
- `ecommerce-web` → tüm API'ler + `identity-server` + `chat-agent`.
- `chat-agent` → `gateway`.
- Tüm doğrulayıcı servisler → issuer `https://identity.local` (token `iss` eşleşmesi).

## Invariant'lar / doğrulama kuralları (deploy-düzeyi)

- **INV-1**: Tüm servislerin issuer'ı = `https://identity.local` (uyuşmazlık = 401).
- **INV-2**: Postgres pod restart'ında veri korunur (PVC bağlı).
- **INV-3**: replicas>1 verilen servisin pod'ları 2 worker'a dağılır (tek worker'a yığılmaz).
- **INV-4**: Publish additive — yerel `aspire run` (F5) davranışı değişmez.