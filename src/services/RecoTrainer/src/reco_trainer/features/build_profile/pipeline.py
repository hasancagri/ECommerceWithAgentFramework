"""SAF profil türetimi (İLKE VI, test-first). Sinyal satırları → ağırlıklı öznitelikler → çoklu-küme profil.

I/O yok: endpoint/service satırları çeker, buraya verir. Formül (R4): w = Σ typeWeight × recencyDecay, sonra
sqrt (sublinear) + IDF. Kümeleme (R6): kategori = küme tohumu, yazarlar bağlanır (tek-ortalama/çorba DEĞİL).
Dağıtım (R5): calibrated share (oransal, Σ≈1) + azınlık taban kotası; keşif kuşağı eşik-altı kategoriden.
"""

from __future__ import annotations

import math
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime

from reco_trainer.domain.profile import AttributeWeight, InterestCluster, TasteProfile


@dataclass(frozen=True)
class SignalRow:
    """Pipeline girdisi (Signal tablosunun saf yansıması; SQLAlchemy'den decouple)."""

    event_type: str
    author: str | None
    category: str | None
    occurred_at: datetime


@dataclass
class _CategoryAggregate:
    """Bir kategori kümesinin ham birikimi: kategori ağırlığı + içindeki yazar ağırlıkları."""

    category_raw: float = 0.0
    author_raw: dict[str, float] = field(default_factory=dict[str, float])


def _recency_decay(occurred_at: datetime, now: datetime, half_life_days: float) -> float:
    """Üstel tazelik: 0.5 ** (yaş_gün / yarı_ömür). Gelecek/eşit zaman = 1.0."""
    age_days = max(0.0, (now - occurred_at).total_seconds() / 86_400.0)
    return 0.5 ** (age_days / half_life_days)


def _attribute_pairs(signal: SignalRow) -> list[tuple[str, str]]:
    """Bir sinyalin taşıdığı (type, value) öznitelikleri — author + category (dolu olanlar)."""
    pairs: list[tuple[str, str]] = []
    if signal.author:
        pairs.append(("author", signal.author))
    if signal.category:
        pairs.append(("category", signal.category))
    return pairs


def compute_attribute_weights(
    signals: list[SignalRow],
    now: datetime,
    type_weights: dict[str, float],
    half_life_days: float,
) -> dict[tuple[str, str], float]:
    """Öznitelik ağırlıkları: Σ (typeWeight × recencyDecay), sonra sqrt (sublinear) × IDF (kendi korpusu)."""
    raw: dict[tuple[str, str], float] = defaultdict(float)
    doc_freq: dict[str, int] = defaultdict(int)
    total = len(signals)

    for signal in signals:
        decay = _recency_decay(signal.occurred_at, now, half_life_days)
        weight = type_weights.get(signal.event_type, 0.0) * decay
        for pair in _attribute_pairs(signal):
            raw[pair] += weight
            doc_freq[pair[1]] += 1

    result: dict[tuple[str, str], float] = {}
    for pair, raw_weight in raw.items():
        idf = math.log((1 + total) / (1 + doc_freq[pair[1]])) + 1.0
        result[pair] = math.sqrt(raw_weight) * idf
    return result


def _idf(value: str, doc_freq: dict[str, int], total: int) -> float:
    return math.log((1 + total) / (1 + doc_freq[value])) + 1.0


def _aggregate_by_category(
    signals: list[SignalRow],
    now: datetime,
    type_weights: dict[str, float],
    half_life_days: float,
) -> tuple[dict[str, _CategoryAggregate], dict[str, int], int]:
    """Kategori tohumlu birikim: her sinyal kategorisine + o kategorideki yazarına katkı yazar (R6)."""
    by_category: dict[str, _CategoryAggregate] = defaultdict(_CategoryAggregate)
    doc_freq: dict[str, int] = defaultdict(int)

    for signal in signals:
        decay = _recency_decay(signal.occurred_at, now, half_life_days)
        contribution = type_weights.get(signal.event_type, 0.0) * decay
        if signal.category:
            doc_freq[signal.category] += 1
            agg = by_category[signal.category]
            agg.category_raw += contribution
            if signal.author:
                doc_freq[signal.author] += 1
                agg.author_raw[signal.author] = agg.author_raw.get(signal.author, 0.0) + contribution

    return by_category, doc_freq, len(signals)


def _cluster_attributes(
    category: str, agg: _CategoryAggregate, doc_freq: dict[str, int], total: int
) -> list[AttributeWeight]:
    """Küme öznitelikleri: kategori + içindeki yazarlar (sqrt×IDF), ağırlık azalan."""
    attributes = [
        AttributeWeight("category", category, math.sqrt(agg.category_raw) * _idf(category, doc_freq, total))
    ]
    attributes.extend(
        AttributeWeight("author", author, math.sqrt(raw) * _idf(author, doc_freq, total))
        for author, raw in agg.author_raw.items()
    )
    attributes.sort(key=lambda a: a.weight, reverse=True)
    return attributes


def _calibrated_shares(raw_by_category: dict[str, float], base_quota: float) -> dict[str, float]:
    """Oransal (calibrated) pay + azınlık taban kotası; normalize (Σ=1). argmax DEĞİL (R5)."""
    total = sum(raw_by_category.values())
    if total <= 0:
        return {k: 1.0 / len(raw_by_category) for k in raw_by_category}

    shares = {k: max(v / total, base_quota) for k, v in raw_by_category.items()}
    renorm = sum(shares.values())
    return {k: v / renorm for k, v in shares.items()}


def build_profile(
    signals: list[SignalRow],
    now: datetime,
    type_weights: dict[str, float],
    half_life_days: float,
    cluster_seed_threshold: float,
    base_share_quota: float,
) -> TasteProfile:
    """Çoklu-küme profil: kategori tohumlu segmentler + calibrated share + keşif. Sinyalsiz = boş."""
    by_category, doc_freq, total = _aggregate_by_category(signals, now, type_weights, half_life_days)
    if not by_category:
        return TasteProfile(clusters=[], discovery=None)

    # Tohum seçimi: normalize kategori ağırlığı eşiği geçen = birincil küme; kalanlar keşif adayı.
    total_cat = sum(a.category_raw for a in by_category.values())
    primary: dict[str, _CategoryAggregate] = {}
    leftover: dict[str, _CategoryAggregate] = {}
    for category, agg in by_category.items():
        norm = agg.category_raw / total_cat if total_cat > 0 else 0.0
        (primary if norm >= cluster_seed_threshold else leftover)[category] = agg

    # En az bir küme garantisi: hiçbiri eşiği geçmezse en ağır kategori tek küme olur.
    if not primary:
        top = max(by_category.items(), key=lambda kv: kv[1].category_raw)
        primary = {top[0]: top[1]}
        leftover.pop(top[0], None)

    shares = _calibrated_shares({c: a.category_raw for c, a in primary.items()}, base_share_quota)

    clusters: list[InterestCluster] = []
    for category, agg in sorted(primary.items(), key=lambda kv: kv[1].category_raw, reverse=True):
        attributes = _cluster_attributes(category, agg, doc_freq, total)
        top_author = next((a.value for a in attributes if a.type == "author"), category)
        clusters.append(
            InterestCluster(
                label=f"{category} için",
                reason=f"{top_author} ile ilgilendiğin için",
                share=shares[category],
                attributes=attributes,
            )
        )

    discovery = _build_discovery(leftover, doc_freq, total)
    return TasteProfile(clusters=clusters, discovery=discovery)


def _build_discovery(
    leftover: dict[str, _CategoryAggregate], doc_freq: dict[str, int], total: int
) -> InterestCluster | None:
    """Keşif kuşağı (FR-009): eşik-altı en ağır kategori (komşu/farklı). Kalan yoksa None (faz-1)."""
    if not leftover:
        return None
    category, agg = max(leftover.items(), key=lambda kv: kv[1].category_raw)
    attributes = _cluster_attributes(category, agg, doc_freq, total)
    return InterestCluster(
        label="Keşfet",
        reason="Sevdiklerine komşu türler",
        share=0.0,  # keşif payı serving'de (WebApp) sabit orandan gelir; profil sıralamayı taşır
        attributes=attributes,
    )
