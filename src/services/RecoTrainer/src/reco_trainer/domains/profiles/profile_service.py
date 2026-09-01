"""profiles domain — application service. Sinyal YAZ (gezinme/arama + satın-alma) + profil OKU (precompute).

Repository YOK: AsyncSession doğrudan (conventions.md). Profil istek-anında HESAPLANMAZ — jobs/ precompute
eder, serving taste_profile satırını okur. Hesaplanmamış subject = boş profil (cold-start).
"""

from __future__ import annotations

import uuid

from sqlalchemy import select
from sqlalchemy.dialects.postgresql import insert as pg_insert
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.domains.profiles.schema import (
    PurchaseEnrichedIn,
    SignalIn,
    SubjectOut,
    TasteProfileOut,
)
from reco_trainer.domains.profiles.signal import Signal
from reco_trainer.domains.profiles.taste_profile import TasteProfileRecord


class ProfileService:
    """Sinyal yazma (ingest) + zevk profili okuma (precompute'tan). Serving hesap yapmaz."""

    def __init__(self, session: AsyncSession):
        self._session = session

    async def ingest_browsing(self, batch: list[SignalIn]) -> None:
        """Gezinme/arama sinyalleri — geçerli ögeleri yazar; geçersiz atlanır (kayıp-toleranslı)."""
        rows = [
            Signal(
                event_type=item.event_type,
                user_id=item.user_id,
                anonymous_id=item.anonymous_id,
                product_id=item.product_id,
                author=item.author,
                category=item.category,
                price=item.price,
                search_term=item.search_term,
                occurred_at=item.occurred_at,
            )
            for item in batch
            if item.is_valid()
        ]
        if rows:
            self._session.add_all(rows)
            await self._session.commit()

    async def ingest_purchase(self, event: PurchaseEnrichedIn) -> None:
        """Satın-alma kalemleri — her biri `Purchased`; `unique(dedup_key)` idempotent (çift no-op)."""
        for item in event.items:
            stmt = (
                pg_insert(Signal)
                .values(
                    id=uuid.uuid4(),
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
                .on_conflict_do_nothing(constraint="uq_signal_dedup_key")
            )
            await self._session.execute(stmt)
        await self._session.commit()

    async def get_profile(
        self, user_id: uuid.UUID | None, anonymous_id: uuid.UUID | None
    ) -> TasteProfileOut:
        """Precompute profili OKUR (dikiş: önce user_id, yoksa anonymous_id). Yoksa boş (cold-start)."""
        record = await self._find(user_id, anonymous_id)
        if record is None:
            return TasteProfileOut(
                subject=SubjectOut(user_id=user_id, anonymous_id=anonymous_id),
                clusters=[],
                discovery=None,
            )
        return TasteProfileOut.model_validate(record.payload)

    async def _find(
        self, user_id: uuid.UUID | None, anonymous_id: uuid.UUID | None
    ) -> TasteProfileRecord | None:
        """Subject satırı: önce user_id (login), yoksa anonymous_id. Tam dikiş-merge = faz-2."""
        if user_id is not None:
            row = (
                await self._session.execute(
                    select(TasteProfileRecord).where(TasteProfileRecord.user_id == user_id)
                )
            ).scalar_one_or_none()
            if row is not None:
                return row
        if anonymous_id is not None:
            return (
                await self._session.execute(
                    select(TasteProfileRecord).where(TasteProfileRecord.anonymous_id == anonymous_id)
                )
            ).scalar_one_or_none()
        return None