"""T025 (İLKE VI, test-first): kümeleme (segment) + calibrated share + taban kota + keşif kuşağı."""

from __future__ import annotations

from datetime import UTC, datetime

from reco_trainer.config import settings
from reco_trainer.features.build_profile.pipeline import SignalRow, build_profile

NOW = datetime(2026, 9, 1, tzinfo=UTC)
TW = settings.type_weights
HL = settings.recency_half_life_days


def _profile(signals: list[SignalRow]):
    return build_profile(
        signals, NOW, TW, HL,
        cluster_seed_threshold=settings.cluster_seed_threshold,
        base_share_quota=settings.base_share_quota,
    )


def _views(category: str, author: str, n: int) -> list[SignalRow]:
    return [
        SignalRow(event_type="ProductViewed", author=author, category=category, occurred_at=NOW)
        for _ in range(n)
    ]


def test_segments_into_separate_clusters_not_single_average() -> None:
    """10 Tarih + 2 Rus sinyali → iki AYRI küme (çorba/tek-ortalama değil, US2)."""
    signals = _views("Tarih", "Tolstoy", 10) + _views("Rus", "Dostoyevski", 2)
    profile = _profile(signals)

    categories = {a.value for c in profile.clusters for a in c.attributes if a.type == "category"}
    assert "Tarih" in categories
    assert "Rus" in categories
    assert len(profile.clusters) >= 2


def test_shares_are_calibrated_sum_to_one() -> None:
    """share oransal (calibrated) — küme payları toplamı ≈ 1 (argmax değil)."""
    signals = _views("Tarih", "Tolstoy", 10) + _views("Rus", "Dostoyevski", 2)
    profile = _profile(signals)

    total = sum(c.share for c in profile.clusters)
    assert abs(total - 1.0) < 1e-6


def test_minority_cluster_protected_by_base_quota() -> None:
    """Azınlık küme (2 Rus, baskın 10 Tarih) taban kotanın altına düşmez (FR-008)."""
    signals = _views("Tarih", "Tolstoy", 10) + _views("Rus", "Dostoyevski", 2)
    profile = _profile(signals)

    rus = next(c for c in profile.clusters if any(a.value == "Rus" for a in c.attributes))
    assert rus.share >= settings.base_share_quota - 1e-9


def test_cluster_attributes_weight_descending() -> None:
    """Küme öznitelikleri ağırlık azalan sıralı."""
    signals = _views("Tarih", "Tolstoy", 6) + _views("Tarih", "Kemal", 2)
    profile = _profile(signals)

    tarih = next(c for c in profile.clusters if any(a.value == "Tarih" for a in c.attributes))
    weights = [a.weight for a in tarih.attributes]
    assert weights == sorted(weights, reverse=True)


def test_discovery_present_when_leftover_category_exists() -> None:
    """Eşik altı azınlık kategori kaldıkça keşif kuşağı üretilir (FR-009, komşu/farklı)."""
    # Baskın Tarih + eşik-altı tek Felsefe sinyali → Felsefe keşfe düşer.
    signals = _views("Tarih", "Tolstoy", 20) + _views("Felsefe", "Platon", 1)
    profile = _profile(signals)

    assert profile.discovery is not None
    disc_categories = {a.value for a in profile.discovery.attributes if a.type == "category"}
    assert "Felsefe" in disc_categories


def test_empty_signals_yields_empty_profile() -> None:
    profile = _profile([])
    assert profile.clusters == []
    assert profile.discovery is None
