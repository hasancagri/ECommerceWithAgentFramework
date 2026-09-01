"""Precompute use-case (BackgroundService işi): tüm sinyaller → fit vectorizer → subject başına profil → DB.

Tümünü yeniden hesaplar (faz-1: dirty-tracking yok). Tüm kayıt gelir (recency decay eskiyi söndürür,
window YOK). Serving bu satırları okur (hesap yok). Model dosyaya (saf-dosya), profiller taste_profile'a.
"""

from __future__ import annotations

import uuid
from datetime import UTC, datetime

from sqlalchemy import delete, select
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.config import settings
from reco_trainer.domains.profiles.schema import TasteProfileOut
from reco_trainer.domains.profiles.signal import Signal
from reco_trainer.domains.profiles.taste_profile import TasteProfileRecord
from reco_trainer.jobs.model_store import save_vectorizer
from reco_trainer.jobs.pipeline import SignalPoint, build_profile, fit_vectorizer, to_point


class _Subject:
    """Bir subject (tek kimlik) + noktaları. Anahtar = user_id varsa o, yoksa anonymous_id."""

    __slots__ = ("user_id", "anonymous_id", "points")

    def __init__(self, user_id: uuid.UUID | None, anonymous_id: uuid.UUID | None):
        self.user_id = user_id
        self.anonymous_id = anonymous_id
        self.points: list[SignalPoint] = []


class RecomputeProfilesJob:
    """Tüm subject'lerin profilini yeniden hesaplar; model dosyaya, profiller DB'ye yazılır."""

    def __init__(self, session: AsyncSession):
        self._session = session

    async def run(self) -> int:
        """Döner: yazılan profil sayısı. Sinyalsizse 0 (hiçbir şey yazmaz)."""
        now = datetime.now(UTC)
        rows = (
            await self._session.execute(
                select(
                    Signal.user_id,
                    Signal.anonymous_id,
                    Signal.event_type,
                    Signal.author,
                    Signal.category,
                    Signal.occurred_at,
                )
            )
        ).all()
        if not rows:
            return 0

        all_points: list[SignalPoint] = []
        subjects: dict[uuid.UUID, _Subject] = {}
        for r in rows:
            point = to_point(
                r.event_type, r.author, r.category, r.occurred_at, now,
                settings.type_weights, settings.recency_half_life_days,
            )
            all_points.append(point)
            key = r.user_id or r.anonymous_id
            if key is None:
                continue
            subject = subjects.get(key)
            if subject is None:
                is_user = r.user_id is not None
                subject = _Subject(key if is_user else None, None if is_user else key)
                subjects[key] = subject
            subject.points.append(point)

        # FIT: korpus üstünde tek vectorizer → dosyaya (versiyonlu). Return = model versiyonu.
        vectorizer = fit_vectorizer(all_points)
        version = save_vectorizer(vectorizer)

        # Tam yeniden hesap: eski profilleri sil, taze yaz.
        await self._session.execute(delete(TasteProfileRecord))

        written = 0
        for subject in subjects.values():
            profile = build_profile(subject.points, vectorizer, settings.base_share_quota)
            if not profile.clusters:
                continue
            payload = TasteProfileOut.of(profile, subject.user_id, subject.anonymous_id).model_dump(
                by_alias=True, mode="json"
            )
            self._session.add(
                TasteProfileRecord(
                    user_id=subject.user_id,
                    anonymous_id=subject.anonymous_id,
                    payload=payload,
                    model_version=version,
                    computed_at=now,
                )
            )
            written += 1

        await self._session.commit()
        return written