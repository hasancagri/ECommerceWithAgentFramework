"""FastStream tüketici: Storefront `PurchaseEnriched` → her item bir `Purchased` Signal (idempotent).

Binding'i TÜKETİCİ kurar (007): fanout exchange + kendi durable kuyruğu. Son-hat idempotency =
`unique(dedup_key)` (çift teslimde no-op). .NET yayıncı STJ default = PascalCase alan adları (alias).
"""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from faststream.rabbit import ExchangeType, RabbitExchange, RabbitQueue
from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_pascal

from reco_trainer.adapters.broker import broker
from reco_trainer.adapters.db import session_factory
from reco_trainer.adapters.models import Signal
from reco_trainer.adapters.signal_repository import upsert_purchased

_exchange = RabbitExchange("purchase.enriched", type=ExchangeType.FANOUT, durable=True)
_queue = RabbitQueue("reco-trainer.purchase-enriched", durable=True)


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


@broker.subscriber(_queue, _exchange)
async def handle_purchase_enriched(event: PurchaseEnrichedIn) -> None:
    """Her satın-alma kalemi → `Purchased` satırı; `unique(dedup_key)` ile çift teslim no-op."""
    async with session_factory() as session:
        for item in event.items:
            signal = Signal(
                dedup_key=item.dedup_key,
                event_type="Purchased",
                user_id=event.user_id,
                anonymous_id=event.anonymous_id,
                product_id=item.product_id,
                author=item.author,
                category=item.category,
                price=item.unit_price,
                quantity=item.quantity,
                occurred_at=event.occurred_at,
            )
            await upsert_purchased(session, signal)
        await session.commit()
