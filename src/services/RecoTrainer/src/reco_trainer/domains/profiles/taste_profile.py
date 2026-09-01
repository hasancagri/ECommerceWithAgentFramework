"""profiles domain — `TasteProfileRecord` precompute edilmiş profil (serving buradan OKUR, hesap yok).

payload = TasteProfile çıktısı (clusters + discovery + subject), camelCase JSON (SABİT sözleşme FR-017).
Subject = tek kimlik satırı (user_id VEYA anonymous_id). model_version = hangi ModelArtifact ile üretildi.
"""

from __future__ import annotations

import uuid
from datetime import datetime
from typing import Any

from sqlalchemy import DateTime, Index, Integer
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from reco_trainer.shared.db import Base


class TasteProfileRecord(Base):
    __tablename__ = "taste_profile"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    user_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    anonymous_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    payload: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False)
    model_version: Mapped[int] = mapped_column(Integer, nullable=False)
    computed_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    __table_args__ = (
        Index("ix_taste_profile_user_id", "user_id"),
        Index("ix_taste_profile_anonymous_id", "anonymous_id"),
    )
