# Feature Specification: Aspire Native Kubernetes Publish

**Feature Branch**: `004-aspire-k8s-publish`

**Created**: 2026-07-20

**Status**: Draft

**Input**: User description: "Aspire AppHost'ta native Kubernetes publish desteği.
PublishAsKubernetesService kullanarak servisleri Kubernetes resource'larına
dönüştürmek istiyorum. Örnek: redis cache eklenip, api projesi
WithReference(redis).WaitFor(redis).PublishAsKubernetesService(...) ile Deployment
olarak publish edilecek; Deployment.Spec.Replicas gibi ayarlar yapılabilecek (ör. replicas=3)."

**Artefakt kademesi**: **Tam** — yeni deployment yüzeyi (K8s manifest üretimi), sistem
geneli etki ve gerçek belirsizlik (kapsam, backing-service yerleşimi) var. Domain
core principle'larını değiştirmez; AppHost (kompozisyon kökü) düzeyinde bir eklemedir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tüm sistemi tek komutla K8s manifest'lerine publish et (Priority: P1)

Operatör/geliştirici, AppHost'u tek bir publish komutuyla çalıştırıp mevcut dağıtık
sistemin (tüm API servisleri, gateway, web, agent, Identity ve backing servisler)
Kubernetes manifest'lerini üretir. Aspire kaynak grafiği (referanslar, bağımlılıklar,
bağlantı dizileri, ortam değişkenleri) manifest'lere birebir yansır.

**Why this priority**: Bu, feature'ın çekirdek değeridir — elle YAML yazmadan, tek
doğruluk kaynağı (AppHost) üzerinden dağıtılabilir çıktı elde etmek. Bu olmadan
diğer hikâyeler anlamsızdır.

**Independent Test**: Publish komutu çalıştırılır; çıktı dizininde her proje kaynağı
için bir workload manifest'i (+ servis/config) üretildiği ve mevcut AppHost'taki
referansların bağlantı bilgisi olarak yansıdığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** mevcut AppHost tüm servislerle, **When** publish komutu çalıştırılır,
   **Then** her proje kaynağı için bir workload manifest'i üretilir.
2. **Given** bir servis başka bir servise `WithReference` ile bağlı, **When** publish
   yapılır, **Then** hedef servisin adresi/bağlantısı kaynak servisin config'ine geçer.
3. **Given** publish tekrar çalıştırılır, **Then** çıktı deterministiktir (aynı girdi
   → aynı manifest; gereksiz fark oluşmaz).

---

### User Story 2 - Servis başına replica ve ölçek ayarı (Priority: P2)

Operatör, seçili servisler için manifest üretimini özelleştirir — özellikle replica
sayısını ayarlar (ör. bir API için 3 kopya). Ayar AppHost içinde, kod olarak (publish
özelleştirme kancası ile) ifade edilir; üretilen manifest bu değeri yansıtır.

**Why this priority**: Kullanıcının örneğinde açıkça istediği davranış; ölçeklenebilir
dağıtımın temel kontrolü. P1 üzerine kurulur.

**Independent Test**: Bir servise replica=3 verilir, publish çalıştırılır, üretilen
workload manifest'inde replica sayısının 3 olduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir servise replica=3 özelleştirmesi, **When** publish yapılır, **Then**
   o servisin workload'unda replica sayısı 3'tür.
2. **Given** hiçbir özelleştirme verilmemiş servis, **When** publish yapılır, **Then**
   makul bir varsayılan replica (1) uygulanır.

---

### User Story 3 - Üretilen chart'ın kind cluster'ına deploy edilmesi (Priority: P3)

Operatör, üretilen Helm chart'ı önce şema-geçerliliği için doğrular (lint/dry-run),
ardından **yerel bir `kind` cluster'ına (1 control-plane + 2 worker) fiilen deploy eder**;
tüm pod'lar Running duruma gelir ve sistem uçtan uca çalışır.

**Why this priority**: Publish çıktısının gerçekten dağıtılabilir olduğunu kanıtlar;
ancak P1/P2 çıktısı olmadan çalıştırılamaz, o yüzden en düşük öncelik (ama zorunlu).

**Independent Test**: Chart `helm lint`/`--dry-run`'dan şema hatasız geçer; kind'a
`helm install` sonrası tüm pod'lar Running olur ve login dahil uçtan uca akış çalışır.

**Acceptance Scenarios**:

1. **Given** üretilmiş chart, **When** `helm lint`/`--dry-run` çalıştırılır, **Then**
   şema hataları olmadan geçer.
2. **Given** kind cluster (1 cp + 2 worker), **When** chart `helm install` edilir,
   **Then** tüm pod'lar Running olur ve sistem uçtan uca çalışır.

---

### Edge Cases

- Postgres cluster içinde çalışırken kalıcı veri (data volume) K8s'te PVC ile nasıl
  karşılanır; pod restart'ta veri korunur mu?
- Servis imajları kind node'larına nasıl ulaşır (yerel build + `kind load docker-image`;
  harici registry olmadan `imagePullPolicy: Never`/`IfNotPresent` senaryosu)?
- 2 worker'a dağıtım: replica'lar worker node'lara yayılıyor mu; bir worker düşerse ne olur?
- Identity.Server HTTPS zorunluluğu ve issuer/`Authority` eşleşmesi cluster içi servis
  adları/DNS ile nasıl korunur (aksi halde login döngüsü riski)?
- Aspire tarafından enjekte edilen bağlantı dizileri/secret'lar manifest'lerde nasıl
  taşınır (config vs secret ayrımı)?
- Servisler-arası service discovery isimleri K8s Service DNS adlarıyla nasıl hizalanır?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, AppHost publish çalıştırıldığında bir **Helm chart** üretmeli ve
  bu chart her proje kaynağı için bir Kubernetes workload manifest'i (template) içermelidir.
- **FR-002**: Sistem, AppHost'taki `WithReference` bağımlılıklarını üretilen
  manifest'lerde bağlantı/adres bilgisi olarak yansıtmalıdır.
- **FR-003**: Sistem, servis başına replica sayısının AppHost içinde kod olarak
  ayarlanmasına izin vermeli ve bunu manifest'e yansıtmalıdır.
- **FR-004**: Özelleştirme verilmeyen servisler için makul varsayılanlar (replica=1)
  uygulanmalıdır.
- **FR-005**: Publish çıktısı deterministik olmalıdır (aynı AppHost → aynı manifest).
- **FR-006**: Üretilen manifest'ler, geçerli bir Kubernetes hedefine uygulanabilir
  (şema-geçerli) olmalıdır.
- **FR-007**: Aspire'ın enjekte ettiği ortam değişkenleri ve bağlantı dizileri
  manifest'lerde config/secret olarak temsil edilmelidir.
- **FR-008**: Publish akışı, sistemin mevcut yerel (F5/AppHost run) çalıştırma deneyimini
  bozmamalıdır — publish yalnızca ek bir çıktı modudur.
- **FR-009**: Publish kapsamı **tüm sistemdir** — mevcut AppHost'taki 12 proje kaynağı
  (Identity, 8 API, gateway, web, agent) ve backing servisler dahil publish edilir.
- **FR-010**: Backing servisler (Postgres, RabbitMQ, Redis) **cluster içinde workload
  olarak** üretilmelidir (self-contained). Postgres için kalıcı veri (PVC/kalıcılık)
  ele alınmalıdır; RabbitMQ ve Redis'in bağlantıları servis DNS adlarıyla çözülmelidir.
- **FR-011**: Üretilen manifest'ler **yerel bir `kind` cluster'ına (1 control-plane +
  2 worker node) fiilen deploy edilebilmelidir**; pod'lar worker node'lara dağılarak
  ayağa kalkar ve sistem uçtan uca çalışır. Bulut sağlayıcı hedeflenmez.
- **FR-012**: Servis imajları, kind'ın çekebileceği şekilde hazır olmalıdır (yerel imaj
  build + `kind load` ile node'lara yükleme); publish akışı imaj üretimini kapsamalıdır.
- **FR-013**: replicas>1 olan servisler için kopyalar 2 worker node'a dağıtılabilmelidir;
  cluster 1 control-plane + 2 worker ile ayağa kaldırılabilir olmalıdır.

### Key Entities

- **Publish Çıktısı (Helm Chart)**: AppHost kaynak grafiğinden türetilen, dağıtılabilir
  Helm chart (Chart.yaml + values.yaml + workload/servis/config/secret template'leri).
- **Servis Publish Özelleştirmesi**: Bir kaynağın manifest üretimini etkileyen ayarlar
  (ör. replica sayısı); AppHost içinde kod olarak ifade edilir.
- **Backing Servis Temsili**: Postgres/RabbitMQ/Redis'in publish çıktısındaki karşılığı
  (cluster içi workload + Postgres için kalıcılık/PVC).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tek bir publish komutuyla, mevcut sistemdeki her proje kaynağı için bir
  workload manifest'i elle YAML yazılmadan üretilir.
- **SC-002**: Bir servise replica=3 verildiğinde üretilen manifest'te replica sayısı
  3 olarak görülür (elle düzenleme olmadan).
- **SC-003**: Üretilen manifest seti bir doğrulama/dry-run adımından şema hatası
  olmadan geçer.
- **SC-004**: Publish eklendikten sonra da sistem yerelde eskisi gibi (AppHost run ile)
  sorunsuz çalışmaya devam eder — hiçbir regresyon yoktur.
- **SC-005**: Aynı AppHost'tan iki ardışık publish, anlamlı fark üretmez (deterministik).
- **SC-006**: Üretilen manifest'ler kind cluster'a (1 control-plane + 2 worker) apply
  edildiğinde tüm pod'lar Running duruma gelir ve sistem uçtan uca çalışır (ör. web
  açılır, bir ürün akışı çalışır).
- **SC-007**: replicas=3 verilen bir servisin 3 pod'u çalışır ve 2 worker node'a
  dağılır (hepsi tek worker'a yığılmaz).

## Assumptions

- Hedef, mevcut Aspire AppHost'u tek doğruluk kaynağı olarak korumak; K8s tanımları
  elle değil publish ile türetilir (drift'i önlemek için).
- Publish özelliği preview/erken olgunlukta olabilir; feature bunu kabul eder ve
  gerektiğinde sınırlamaları belgeler (tüm sistem kapsanır).
- Identity.Server'ın HTTPS ve issuer eşleşme kısıtı korunacak; cluster içi adresleme
  bu kısıtı bozmayacak şekilde ele alınır (login döngüsü riski önlenir).
- Hedef Kubernetes, yerel bir `kind` cluster'ıdır: 1 control-plane + 2 worker node
  (Docker üzerinde çalışır); bulut sağlayıcı veya yönetilen cluster varsayılmaz.
- Bu feature domain servislerinin kodunu/aggregate'lerini değiştirmez; değişiklik
  AppHost (kompozisyon) düzeyindedir.