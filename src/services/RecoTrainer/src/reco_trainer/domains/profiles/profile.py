"""profiles domain — zevk profili değer nesneleri (saf, I/O yok). TasteProfile = beyin çıktısı (FR-017)."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class AttributeWeight:
    """Bir öznitelik + türetilmiş ağırlık. type ∈ {author, category, period}."""

    type: str
    value: str
    weight: float


@dataclass(frozen=True)
class InterestCluster:
    """Bir ilgi kümesi: etiket + gerekçe + oransal pay + ağırlık-azalan öznitelikler."""

    label: str
    reason: str
    share: float
    attributes: list[AttributeWeight]


@dataclass(frozen=True)
class TasteProfile:
    """Beyin sonucu. bookId ÜRETMEZ (FR-023) — yalnız öznitelik + ağırlık + oran + gerekçe."""

    clusters: list[InterestCluster]
    discovery: InterestCluster | None = None