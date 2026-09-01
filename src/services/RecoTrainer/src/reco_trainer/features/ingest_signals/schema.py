"""Ingest sözleşme şemaları (Pydantic). WebApp BehaviorEvent şeklinin Python karşılığı (camelCase alias)."""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

# Gezinme/arama ingest'te kabul edilen tipler (Purchased broker'dan gelir, HTTP'den DEĞİL).
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
