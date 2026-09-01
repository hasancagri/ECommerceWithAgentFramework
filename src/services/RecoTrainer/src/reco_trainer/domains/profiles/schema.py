"""profiles domain — Pydantic DTO'lar (request/response + broker event şeması). Domain VO'dan ayrı (sınır)."""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel, to_pascal

from reco_trainer.domains.profiles.profile import InterestCluster, TasteProfile

# --- Gezinme/arama ingest (WebApp → HTTP; camelCase) ---
INGEST_EVENT_TYPES = frozenset({"ProductViewed", "BasketItemAdded", "SearchPerformed"})


class SignalIn(BaseModel):
    """POST /api/v1/signals batch ögesi. Puan gövdede YOK — eventType'tan config ile türetilir."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True, extra="ignore")

    event_type: str
    user_id: uuid.UUID | None = None
    anonymous_id: uuid.UUID | None = None
    product_id: uuid.UUID | None = None
    author: str | None = None
    category: str | None = None
    price: Decimal | None = None
    search_term: str | None = None
    occurred_at: datetime

    def is_valid(self) -> bool:
        """eventType bilinen kümede + en az bir kimlik dolu (kayıp-toleranslı: geçersiz atlanır)."""
        return self.event_type in INGEST_EVENT_TYPES and (
            self.user_id is not None or self.anonymous_id is not None
        )


# --- Satın-alma (Storefront → broker; .NET STJ default = PascalCase) ---
class PurchaseEnrichedItemIn(BaseModel):
    model_config = ConfigDict(alias_generator=to_pascal, populate_by_name=True, extra="ignore")

    product_id: uuid.UUID
    quantity: int
    unit_price: Decimal
    author: str | None = None
    category: str | None = None
    dedup_key: uuid.UUID


class PurchaseEnrichedIn(BaseModel):
    model_config = ConfigDict(alias_generator=to_pascal, populate_by_name=True, extra="ignore")

    order_id: uuid.UUID
    user_id: uuid.UUID
    anonymous_id: uuid.UUID | None = None
    occurred_at: datetime
    items: list[PurchaseEnrichedItemIn]


# --- TasteProfile çıktı (Python → serving; SABİT sözleşme FR-017, camelCase) ---
class _Camel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class SubjectOut(_Camel):
    user_id: uuid.UUID | None = None
    anonymous_id: uuid.UUID | None = None


class AttributeOut(_Camel):
    type: str
    value: str
    weight: float


class ClusterOut(_Camel):
    label: str
    reason: str
    share: float
    attributes: list[AttributeOut]


class TasteProfileOut(_Camel):
    subject: SubjectOut
    clusters: list[ClusterOut]
    discovery: ClusterOut | None = None

    @staticmethod
    def _cluster(c: InterestCluster) -> ClusterOut:
        return ClusterOut(
            label=c.label,
            reason=c.reason,
            share=c.share,
            attributes=[AttributeOut(type=a.type, value=a.value, weight=a.weight) for a in c.attributes],
        )

    @classmethod
    def of(
        cls, profile: TasteProfile, user_id: uuid.UUID | None, anonymous_id: uuid.UUID | None
    ) -> TasteProfileOut:
        return cls(
            subject=SubjectOut(user_id=user_id, anonymous_id=anonymous_id),
            clusters=[cls._cluster(c) for c in profile.clusters],
            discovery=cls._cluster(profile.discovery) if profile.discovery else None,
        )
