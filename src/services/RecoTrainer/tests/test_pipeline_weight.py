"""T013 (İLKE VI, test-first): build_profile ağırlık formülü — typeWeight × recencyDecay, sqrt, IDF."""

from __future__ import annotations

import math
from datetime import UTC, datetime, timedelta

import pytest

from reco_trainer.config import settings
from reco_trainer.features.build_profile.pipeline import SignalRow, compute_attribute_weights

NOW = datetime(2026, 9, 1, tzinfo=UTC)
TW = settings.type_weights
HL = settings.recency_half_life_days


def _weights(signals: list[SignalRow]) -> dict[tuple[str, str], float]:
    return compute_attribute_weights(signals, NOW, TW, HL)


def test_type_weight_priority_purchased_over_view() -> None:
    """Aynı tazelik: satın-alma yazarı, tıklama yazarından ağır (FR-004)."""
    signals = [
        SignalRow(event_type="Purchased", author="Tolstoy", category="Tarih", occurred_at=NOW),
        SignalRow(event_type="ProductViewed", author="Dostoyevski", category="Rus", occurred_at=NOW),
    ]
    w = _weights(signals)
    assert w[("author", "Tolstoy")] > w[("author", "Dostoyevski")]


def test_recency_decay_fresher_heavier() -> None:
    """Aynı tür: taze sinyal, yarı-ömür kadar eski sinyalden ağır (FR-005)."""
    signals = [
        SignalRow(event_type="ProductViewed", author="Fresh", category="A", occurred_at=NOW),
        SignalRow(
            event_type="ProductViewed",
            author="Old",
            category="B",
            occurred_at=NOW - timedelta(days=HL),
        ),
    ]
    w = _weights(signals)
    # Yarı-ömürde çürüme 0.5 → taze ağır, eski hafif ama pozitif (IDF ikisinde eşit, df=1).
    assert w[("author", "Fresh")] > w[("author", "Old")] > 0


def test_sqrt_sublinear_repeated_attribute() -> None:
    """Aynı özniteliğin 4 sinyali, tekinin 4 katından AZ ağırlık üretir (sqrt sublinear)."""
    one = _weights([SignalRow(event_type="ProductViewed", author="X", category="C", occurred_at=NOW)])
    four = _weights(
        [SignalRow(event_type="ProductViewed", author="X", category="C", occurred_at=NOW) for _ in range(4)]
    )
    assert four[("author", "X")] < 4 * one[("author", "X")]


def test_idf_downweights_common_attribute() -> None:
    """Her sinyalde geçen yazar (df yüksek), nadir yazardan düşük IDF alır."""
    signals = [
        SignalRow(event_type="ProductViewed", author="Common", category="Rare", occurred_at=NOW)
        for _ in range(5)
    ]
    signals.append(SignalRow(event_type="ProductViewed", author="Common", category="Solo", occurred_at=NOW))
    w = _weights(signals)
    # "Solo" kategori tek sinyalde (df=1, N=6) → final = sqrt(rawViewWeight) × idf_solo (IDF uygulandı).
    idf_solo = math.log((1 + 6) / (1 + 1)) + 1
    expected_solo = math.sqrt(TW["ProductViewed"]) * idf_solo
    assert w[("category", "Solo")] == pytest.approx(expected_solo)
    # Nadir öznitelik (df=1) yüksek IDF; her belgede geçen yazar (df=6) düşük IDF alır.
    assert idf_solo > math.log((1 + 6) / (1 + 6)) + 1
