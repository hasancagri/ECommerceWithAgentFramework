"""T031 (İLKE VI, test-first): tam tür-ağırlık önceliği (FR-004) + tazelik çürümesi (FR-005)."""

from __future__ import annotations

from datetime import UTC, datetime, timedelta

from reco_trainer.config import settings
from reco_trainer.features.build_profile.pipeline import SignalRow, compute_attribute_weights

NOW = datetime(2026, 9, 1, tzinfo=UTC)
TW = settings.type_weights
HL = settings.recency_half_life_days


def _one(event_type: str, author: str) -> dict[tuple[str, str], float]:
    signals = [SignalRow(event_type=event_type, author=author, category=None, occurred_at=NOW)]
    return compute_attribute_weights(signals, NOW, TW, HL)


def test_type_weight_full_priority_order() -> None:
    """Purchased > BasketItemAdded > ProductViewed > SearchPerformed (aynı tazelik, tek sinyal)."""
    purchased = _one("Purchased", "P")[("author", "P")]
    basket = _one("BasketItemAdded", "B")[("author", "B")]
    viewed = _one("ProductViewed", "V")[("author", "V")]
    searched = _one("SearchPerformed", "S")[("author", "S")]

    assert purchased > basket > viewed > searched


def test_recency_halves_at_half_life() -> None:
    """Yarı-ömür kadar eski aynı-tür sinyalin ham katkısı yarıya iner (üstel çürüme, FR-005)."""
    fresh = SignalRow(event_type="ProductViewed", author="F", category=None, occurred_at=NOW)
    old_at = NOW - timedelta(days=HL)
    old = SignalRow(event_type="ProductViewed", author="O", category=None, occurred_at=old_at)
    weights = compute_attribute_weights([fresh, old], NOW, TW, HL)

    # Ham katkı yarıya iner; sqrt sonrası oran sqrt(0.5). IDF ikisinde eşit (df=1, N=2).
    ratio = weights[("author", "O")] / weights[("author", "F")]
    assert abs(ratio - (0.5**0.5)) < 1e-9


def test_search_is_lightest_signal() -> None:
    """Arama en hafif tür — satın-almanın çok altında (config type_weight)."""
    purchased = _one("Purchased", "X")[("author", "X")]
    searched = _one("SearchPerformed", "X")[("author", "X")]
    assert searched < purchased
