"""profiles domain — broker giriş (Storefront PurchaseEnriched). ProfileService'e delege eder.

Binding'i TÜKETİCİ kurar (007): fanout exchange + kendi durable kuyruğu. İnce giriş: iş ProfileService'te.
Son-hat idempotency `unique(dedup_key)` (çift teslim no-op).
"""

from __future__ import annotations

from faststream.rabbit import ExchangeType, RabbitExchange, RabbitQueue

from reco_trainer.domains.profiles.profile_service import ProfileService
from reco_trainer.domains.profiles.schema import PurchaseEnrichedIn
from reco_trainer.shared.broker import broker
from reco_trainer.shared.db import session_factory

_exchange = RabbitExchange("purchase.enriched", type=ExchangeType.FANOUT, durable=True)
_queue = RabbitQueue("reco-trainer.purchase-enriched", durable=True)


@broker.subscriber(_queue, _exchange)
async def handle_purchase_enriched(event: PurchaseEnrichedIn) -> None:
    """Satın-alma event'i → ProfileService.ingest_purchase (her kalem bir Purchased satırı)."""
    async with session_factory() as session:
        await ProfileService(session).ingest_purchase(event)
