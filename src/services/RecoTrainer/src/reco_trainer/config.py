"""pydantic-settings yapılandırma (Options deseni karşılığı). Ortam değişkeninden DOĞRUDAN okuma yok."""

from __future__ import annotations

import re

from pydantic import AliasChoices, Field
from pydantic_settings import BaseSettings, SettingsConfigDict


def _to_asyncpg_url(aspire_conn: str) -> str:
    """Aspire Npgsql conn-string'ini SQLAlchemy asyncpg URL'ine çevirir.

    'Host=h;Port=p;Username=u;Password=w;Database=d' -> 'postgresql+asyncpg://u:w@h:p/d'.
    Zaten URL biçimindeyse (postgres://...) asyncpg sürücüsüne yükseltir.
    """
    if aspire_conn.startswith(("postgres://", "postgresql://")):
        return re.sub(r"^postgres(ql)?://", "postgresql+asyncpg://", aspire_conn, count=1)

    parts = dict(
        kv.split("=", 1) for kv in (p.strip() for p in aspire_conn.split(";")) if "=" in kv
    )
    host = parts.get("Host", "localhost")
    port = parts.get("Port", "5432")
    user = parts.get("Username", "postgres")
    password = parts.get("Password", "")
    database = parts.get("Database", "recoTrainerDb")
    return f"postgresql+asyncpg://{user}:{password}@{host}:{port}/{database}"


class Settings(BaseSettings):
    """053 beyin yapılandırması. Aspire conn-string'leri env (`ConnectionStrings__*`) ile enjekte edilir."""

    model_config = SettingsConfigDict(extra="ignore", populate_by_name=True)

    # --- Bağlantılar (Aspire `ConnectionStrings__<name>` env ile enjekte eder) ---
    db_conn: str = Field(
        default="Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=recoTrainerDb",
        validation_alias=AliasChoices("ConnectionStrings__recoTrainerDb", "db_conn"),
    )
    rabbitmq_conn: str = Field(
        default="amqp://guest:guest@localhost:5672",
        validation_alias=AliasChoices("ConnectionStrings__rabbitmq", "rabbitmq_conn"),
    )

    # --- Profil politikası (tunable; canlı gözlemle ayarlanır) ---
    # event_type -> öncelik ağırlığı (FR-004). GENİŞ makas: satın-alma domine, tıklama ≈ gürültü.
    # NOT: sqrt (sublinear) makası ezer → final oran ~sqrt(w). 50 tıklama ≈ 1 satın-alma ağırlığı.
    type_weight_purchased: float = 50.0
    type_weight_basket_item_added: float = 15.0
    type_weight_product_viewed: float = 1.0
    type_weight_search_performed: float = 0.5

    # Tazelik (FR-005): üstel yarı-ömür (gün). recencyDecay = 0.5 ** (yaş_gün / half_life).
    recency_half_life_days: float = 30.0

    # Oransal dağıtım (FR-008/FR-025): azınlık kümesi taban kotayla korunur.
    base_share_quota: float = 0.10          # her kümenin minimum payı
    minority_weight_threshold: float = 0.05  # bu ağırlığın altındaki öznitelik kümeye tohum olmaz

    # Keşif kuşağı (FR-009): komşu/farklı öznitelik seçimi payı.
    discovery_share: float = 0.15
    discovery_epsilon: float = 0.30

    # Kümeleme: bir kategorinin küme tohumu olması için minimum normalize ağırlık.
    cluster_seed_threshold: float = 0.15

    # Precompute (jobs/): recompute job aralığı (dk). BackgroundService periyodu. Bayatlık = bu pencere.
    recompute_interval_minutes: int = 15

    # Fitted model dosya dizini (saf dosya registry; versiyonlu joblib; gitignore'lı).
    model_dir: str = "models"

    @property
    def db_url(self) -> str:
        return _to_asyncpg_url(self.db_conn)

    @property
    def rabbitmq_url(self) -> str:
        return self.rabbitmq_conn

    @property
    def type_weights(self) -> dict[str, float]:
        return {
            "Purchased": self.type_weight_purchased,
            "BasketItemAdded": self.type_weight_basket_item_added,
            "ProductViewed": self.type_weight_product_viewed,
            "SearchPerformed": self.type_weight_search_performed,
        }


settings = Settings()