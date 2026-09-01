"""FastAPI ingest ucu: POST /api/v1/signals (batch, kayıp-toleranslı, 202). Gezinme + arama sinyalleri."""

from __future__ import annotations

from fastapi import APIRouter, Depends, status
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.adapters.db import get_session
from reco_trainer.adapters.models import Signal
from reco_trainer.adapters.signal_repository import add_signals
from reco_trainer.features.ingest_signals.schema import SignalIn

router = APIRouter(prefix="/api/v1", tags=["signals"])


@router.post("/signals", status_code=status.HTTP_202_ACCEPTED)
async def ingest_signals(
    batch: list[SignalIn],
    session: AsyncSession = Depends(get_session),
) -> None:
    """Geçerli ögeleri `Signal` tablosuna yazar; geçersiz öge atlanır (kısmi geçersizlik hata değil)."""
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
    await add_signals(session, rows)
    await session.commit()
