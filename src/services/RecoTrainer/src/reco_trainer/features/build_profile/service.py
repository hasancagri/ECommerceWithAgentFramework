"""Profil servisi: Signal satırlarını çeker (dikiş userId OR anonymousId) → saf pipeline → çıktı.

I/O burada; saf hesap `pipeline.py`'de. Faz-1 anlık hesap (precompute yok).
"""

from __future__ import annotations

import uuid
from datetime import UTC, datetime

from sqlalchemy import ColumnElement, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.adapters.models import Signal
from reco_trainer.config import settings
from reco_trainer.features.build_profile.pipeline import SignalRow, build_profile
from reco_trainer.features.build_profile.schema import TasteProfileOut


async def _load_signals(
    session: AsyncSession,
    user_id: uuid.UUID | None,
    anonymous_id: uuid.UUID | None,
) -> list[SignalRow]:
    """Dikiş (FR-013): iki kimlikten en az biri eşleşen sinyaller (userId OR anonymousId)."""
    conditions: list[ColumnElement[bool]] = []
    if user_id is not None:
        conditions.append(Signal.user_id == user_id)
    if anonymous_id is not None:
        conditions.append(Signal.anonymous_id == anonymous_id)
    if not conditions:
        return []

    stmt = select(
        Signal.event_type, Signal.author, Signal.category, Signal.occurred_at
    ).where(or_(*conditions))
    result = await session.execute(stmt)
    return [
        SignalRow(event_type=r.event_type, author=r.author, category=r.category, occurred_at=r.occurred_at)
        for r in result
    ]


async def get_taste_profile(
    session: AsyncSession,
    user_id: uuid.UUID | None,
    anonymous_id: uuid.UUID | None,
) -> TasteProfileOut:
    """Zevk profilini anlık türetir. Sinyalsiz = boş clusters (serving cold-start fallback'e düşer)."""
    signals = await _load_signals(session, user_id, anonymous_id)
    profile = build_profile(
        signals,
        datetime.now(UTC),
        settings.type_weights,
        settings.recency_half_life_days,
        cluster_seed_threshold=settings.cluster_seed_threshold,
        base_share_quota=settings.base_share_quota,
    )
    return TasteProfileOut.of(profile, user_id, anonymous_id)
