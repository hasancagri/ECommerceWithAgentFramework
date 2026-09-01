"""SAF profil türetimi (İLKE VI). domains DIŞI (hesap = job katmanı). sklearn DOĞRUDAN — wrapper yok.

FIT: `fit_vectorizer` korpusta `Pipeline(DictVectorizer → TfidfTransformer)` fit eder = saklanan model
(vocabulary/encoding + IDF sklearn'in kendi nesnesinde). TRANSFORM: `build_profile` bir subject'in
dokunduğu item'ları vektörler, `KMeans(k=3)` ile ilgi segmentlerine böler. Domain politikası (sklearn'de YOK):
typeWeight×recency = KMeans `sample_weight`; calibrated share = küme kütle oranı. Sadece bu 3 knob düz kod.
"""

from __future__ import annotations

import math
from datetime import datetime

from sklearn.cluster import KMeans
from sklearn.feature_extraction.text import TfidfVectorizer

from reco_trainer.domains.profiles.profile import AttributeWeight, InterestCluster, TasteProfile

KMEANS_K = 3
_RANDOM_STATE = 42


def _identity(tokens: list[str]) -> list[str]:
    """TfidfVectorizer analyzer'ı — token'lar zaten yapısal ('type=value'); tokenize/lowercase YOK.

    Module-level (lambda DEĞİL) → joblib picklable. Yazar adındaki boşluk + case korunur (train/serve
    tutarlılığı: profil değeri .NET katalog author adıyla birebir eşleşsin).
    """
    return tokens


def _doc(author: str | None, category: str | None) -> list[str]:
    """Bir item → token listesi ('author=<ad>', 'category=<ad>'). Değerler ham (boşluk+case korunur)."""
    tokens: list[str] = []
    if author:
        tokens.append(f"author={author}")
    if category:
        tokens.append(f"category={category}")
    return tokens


class SignalPoint:
    """Bir sinyalin pipeline görünümü: öznitelikler + ağırlık (typeWeight×recency = KMeans sample_weight)."""

    __slots__ = ("author", "category", "weight")

    def __init__(self, author: str | None, category: str | None, weight: float):
        self.author = author
        self.category = category
        self.weight = weight


def _recency_decay(occurred_at: datetime, now: datetime, half_life_days: float) -> float:
    age_days = max(0.0, (now - occurred_at).total_seconds() / 86_400.0)
    return 0.5 ** (age_days / half_life_days)


def to_point(
    event_type: str,
    author: str | None,
    category: str | None,
    occurred_at: datetime,
    now: datetime,
    type_weights: dict[str, float],
    half_life_days: float,
) -> SignalPoint:
    """Sinyal → ağırlıklı nokta. weight = typeWeight(event) × recencyDecay (FR-004/005)."""
    weight = type_weights.get(event_type, 0.0) * _recency_decay(occurred_at, now, half_life_days)
    return SignalPoint(author, category, weight)


def fit_vectorizer(points: list[SignalPoint]) -> TfidfVectorizer:
    """FIT: korpus üstünde tek TfidfVectorizer (vocab/encoding + IDF sklearn nesnesinde). Saklanan model."""
    # sklearn stub'u analyzer'ı `str` sanıyor; runtime callable kabul eder (stub eksik).
    vectorizer = TfidfVectorizer(
        analyzer=_identity,  # pyright: ignore[reportArgumentType]
        lowercase=False,
    )
    vectorizer.fit([_doc(p.author, p.category) for p in points])
    return vectorizer


def _idf_map(vectorizer: TfidfVectorizer) -> dict[str, float]:
    """sklearn'in fitted IDF'i: feature('type=value') → idf. Yorumlanabilir ağırlık için kullanılır."""
    return {feat: float(vectorizer.idf_[idx]) for feat, idx in vectorizer.vocabulary_.items()}


def _cluster_attributes(members: list[SignalPoint], idf: dict[str, float]) -> list[AttributeWeight]:
    """Küme öznitelikleri: üye ağırlıklarını (type,value) bazında topla × sklearn idf; ağırlık azalan."""
    raw: dict[tuple[str, str], float] = {}
    for p in members:
        for kind, value in (("author", p.author), ("category", p.category)):
            if value:
                raw[(kind, value)] = raw.get((kind, value), 0.0) + p.weight

    attributes = [
        AttributeWeight(kind, value, math.sqrt(w) * idf.get(f"{kind}={value}", 1.0))
        for (kind, value), w in raw.items()
    ]
    attributes.sort(key=lambda a: a.weight, reverse=True)
    return attributes


def _calibrated_shares(masses: list[float], base_quota: float) -> list[float]:
    """Oransal (calibrated) pay + azınlık taban kotası; normalize (Σ=1). argmax DEĞİL (FR-025)."""
    total = sum(masses)
    if total <= 0:
        return [1.0 / len(masses)] * len(masses)
    floored = [max(m / total, base_quota) for m in masses]
    renorm = sum(floored)
    return [f / renorm for f in floored]


def build_profile(
    points: list[SignalPoint],
    vectorizer: TfidfVectorizer,
    base_share_quota: float,
    k: int = KMEANS_K,
) -> TasteProfile:
    """TRANSFORM: subject noktalarını KMeans(k) ile segmentlere böler → çoklu-küme profil. Boşsa boş."""
    if not points:
        return TasteProfile(clusters=[], discovery=None)

    matrix = vectorizer.transform([_doc(p.author, p.category) for p in points])
    sample_weight = [p.weight for p in points]
    # k'yı DISTINCT nokta sayısıyla sınırla (tekrarlar KMeans'i "distinct < k" uyarısına sokmasın).
    distinct = len({(p.author, p.category) for p in points})
    effective_k = min(k, distinct)

    if effective_k <= 1:
        labels = [0] * len(points)
    else:
        # sklearn stub'u n_init'i `str` sanıyor; int geçerli (stub eksik).
        model = KMeans(n_clusters=effective_k, n_init=10, random_state=_RANDOM_STATE)  # pyright: ignore[reportArgumentType]
        labels = model.fit_predict(matrix, sample_weight=sample_weight).tolist()

    idf = _idf_map(vectorizer)
    grouped: dict[int, list[SignalPoint]] = {}
    for label, point in zip(labels, points, strict=True):
        grouped.setdefault(label, []).append(point)

    ordered = sorted(grouped.values(), key=lambda ms: sum(p.weight for p in ms), reverse=True)
    shares = _calibrated_shares([sum(p.weight for p in ms) for ms in ordered], base_share_quota)

    clusters: list[InterestCluster] = []
    for members, share in zip(ordered, shares, strict=True):
        attributes = _cluster_attributes(members, idf)
        if not attributes:
            continue
        top_category = next((a.value for a in attributes if a.type == "category"), attributes[0].value)
        top_author = next((a.value for a in attributes if a.type == "author"), top_category)
        clusters.append(
            InterestCluster(
                label=f"{top_category} için",
                reason=f"{top_author} ile ilgilendiğin için",
                share=share,
                attributes=attributes,
            )
        )

    return TasteProfile(clusters=clusters, discovery=None)