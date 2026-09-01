"""Signal outbound port (Postgres). Insert (gezinme/arama) + idempotent upsert (Purchased, dedup_key)."""

from __future__ import annotations

from sqlalchemy.dialects.postgresql import insert as pg_insert
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.adapters.models import Signal


async def add_signals(session: AsyncSession, signals: list[Signal]) -> None:
    """Gezinme/arama sinyallerini yazar (dedup_key yok). Çağıran commit eder."""
    if not signals:
        return
    session.add_all(signals)
    await session.flush()


async def upsert_purchased(session: AsyncSession, signal: Signal) -> None:
    """Satın-alma sinyalini `unique(dedup_key)` ile idempotent yazar — çift teslimde no-op (son hat).

    `PurchaseEnriched` yeniden teslim edilirse ON CONFLICT (dedup_key) DO NOTHING → satır artmaz.
    """
    stmt = (
        pg_insert(Signal)
        .values(
            id=signal.id,
            dedup_key=signal.dedup_key,
            event_type=signal.event_type,
            user_id=signal.user_id,
            anonymous_id=signal.anonymous_id,
            product_id=signal.product_id,
            author=signal.author,
            category=signal.category,
            price=signal.price,
            quantity=signal.quantity,
            occurred_at=signal.occurred_at,
        )
        .on_conflict_do_nothing(constraint="uq_signal_dedup_key")
    )
    await session.execute(stmt)