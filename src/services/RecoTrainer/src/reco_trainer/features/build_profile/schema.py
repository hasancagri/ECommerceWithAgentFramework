"""TasteProfile çıktı şeması (Pydantic, camelCase). SABİT sözleşme (FR-017) — taste-profile.md."""

from __future__ import annotations

import uuid

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel

from reco_trainer.domain.profile import InterestCluster, TasteProfile


class _Camel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class SubjectOut(_Camel):
    user_id: uuid.UUID | None = None
    anonymous_id: uuid.UUID | None = None


class AttributeOut(_Camel):
    type: str
    value: str
    weight: float


class ClusterOut(_Camel):
    label: str
    reason: str
    share: float
    attributes: list[AttributeOut]


class TasteProfileOut(_Camel):
    subject: SubjectOut
    clusters: list[ClusterOut]
    discovery: ClusterOut | None = None

    @staticmethod
    def _cluster(c: InterestCluster) -> ClusterOut:
        return ClusterOut(
            label=c.label,
            reason=c.reason,
            share=c.share,
            attributes=[AttributeOut(type=a.type, value=a.value, weight=a.weight) for a in c.attributes],
        )

    @classmethod
    def of(
        cls,
        profile: TasteProfile,
        user_id: uuid.UUID | None,
        anonymous_id: uuid.UUID | None,
    ) -> TasteProfileOut:
        return cls(
            subject=SubjectOut(user_id=user_id, anonymous_id=anonymous_id),
            clusters=[cls._cluster(c) for c in profile.clusters],
            discovery=cls._cluster(profile.discovery) if profile.discovery else None,
        )
