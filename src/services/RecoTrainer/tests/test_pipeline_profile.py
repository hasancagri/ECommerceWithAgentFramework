"""İLKE VI (test-first): build_profile — KMeans segmentasyon + calibrated share + taban kota + k=3 cap."""

from __future__ import annotations

from datetime import UTC, datetime

from reco_trainer.config import settings
from reco_trainer.jobs.pipeline import SignalPoint, build_profile, fit_vectorizer, to_point

NOW = datetime(2026, 9, 1, tzinfo=UTC)
TW = settings.type_weights
HL = settings.recency_half_life_days
QUOTA = settings.base_share_quota


def _points(spec: list[tuple[str, str, str]]) -> list[SignalPoint]:
    return [to_point(e, a, c, NOW, NOW, TW, HL) for (e, a, c) in spec]


def _profile(points: list[SignalPoint], quota: float = QUOTA):
    return build_profile(points, fit_vectorizer(points), quota)


def test_segments_into_separate_clusters() -> None:
    """10 Tarih + 3 Rus + 1 Roman → ayrı kümeler (çorba değil); azınlık gömülmez."""
    points = (
        _points([("ProductViewed", "Tolstoy", "Tarih")] * 10)
        + _points([("ProductViewed", "Dostoyevski", "Rus")] * 3)
        + _points([("Purchased", "Kemal", "Roman")] * 1)
    )
    profile = _profile(points)

    categories = {a.value for c in profile.clusters for a in c.attributes if a.type == "category"}
    assert {"Tarih", "Rus", "Roman"} <= categories
    assert len(profile.clusters) == 3


def test_shares_calibrated_sum_to_one() -> None:
    points = _points([("ProductViewed", "Tolstoy", "Tarih")] * 8 + [("ProductViewed", "Kemal", "Roman")] * 2)
    profile = _profile(points)
    assert abs(sum(c.share for c in profile.clusters) - 1.0) < 1e-6


def test_quota_lifts_minority() -> None:
    """Taban kota azınlığı yükseltir: quota=0.3 ile azınlık payı, quota=0'a göre daha yüksek (FR-008)."""
    points = _points([("ProductViewed", "Tolstoy", "Tarih")] * 10 + [("ProductViewed", "Kemal", "Roman")] * 1)
    vec = fit_vectorizer(points)

    def roman_share(quota: float) -> float:
        profile = build_profile(points, vec, quota)
        cluster = next(c for c in profile.clusters if any(a.value == "Roman" for a in c.attributes))
        return cluster.share

    assert roman_share(0.30) > roman_share(0.0)


def test_k_caps_cluster_count() -> None:
    """5 farklı kategori ama k=3 → en fazla 3 küme (KMeans birleştirir)."""
    points = _points(
        [("ProductViewed", f"A{i}", f"Kat{i}") for i in range(5)]
    )
    profile = _profile(points)
    assert len(profile.clusters) <= 3


def test_sparse_single_point_one_cluster() -> None:
    profile = _profile(_points([("ProductViewed", "Tek", "Solo")]))
    assert len(profile.clusters) == 1


def test_empty_yields_empty_profile() -> None:
    profile = build_profile([], fit_vectorizer([SignalPoint("A", "C", 1.0)]), QUOTA)
    assert profile.clusters == []
    assert profile.discovery is None