"""İLKE VI (test-first): to_point ağırlığı — typeWeight önceliği (FR-004) + recency decay (FR-005)."""

from __future__ import annotations

from datetime import UTC, datetime, timedelta

import pytest

from reco_trainer.config import settings
from reco_trainer.jobs.pipeline import to_point

NOW = datetime(2026, 9, 1, tzinfo=UTC)
TW = settings.type_weights
HL = settings.recency_half_life_days


def _weight(event_type: str, days_ago: float = 0.0) -> float:
    occurred = NOW - timedelta(days=days_ago)
    return to_point(event_type, "A", "C", occurred, NOW, TW, HL).weight


def test_type_weight_full_priority_order() -> None:
    """Purchased > BasketItemAdded > ProductViewed > SearchPerformed (aynı tazelik)."""
    assert (
        _weight("Purchased")
        > _weight("BasketItemAdded")
        > _weight("ProductViewed")
        > _weight("SearchPerformed")
    )


def test_recency_halves_at_half_life() -> None:
    """Yarı-ömür kadar eski aynı-tür sinyalin ağırlığı yarıya iner (üstel decay)."""
    assert _weight("ProductViewed", HL) == pytest.approx(_weight("ProductViewed") * 0.5)


def test_two_years_old_is_negligible() -> None:
    """2 yıllık sinyal ≈ 0 (kullanıcı: 'zerre alakadar etmez'). Window değil, decay söndürür."""
    assert _weight("ProductViewed", 730) < 1e-6