"""profiles domain — `Signal` girdi entity'si (feature store). Gezinme+arama+satın-alma tek tabloda.

Rich aggregate DEĞİL: telemetri satırı (invariant yok — bilinçle anemik). Profil bu tablo üstünde `GROUP BY`
ile türetilir. `dedup_key` yalnız Purchased'ta dolu; `unique` → son-hat idempotency (çift teslim no-op).
"""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from sqlalchemy import DateTime, Index, Numeric, String, UniqueConstraint
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

from reco_trainer.shared.db import Base


class Signal(Base):
    __tablename__ = "signal"

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True, default=uuid.uuid4)
    dedup_key: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    event_type: Mapped[str] = mapped_column(String(32), nullable=False)
    user_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    anonymous_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    product_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True), nullable=True)
    author: Mapped[str | None] = mapped_column(String(256), nullable=True)
    category: Mapped[str | None] = mapped_column(String(256), nullable=True)
    price: Mapped[Decimal | None] = mapped_column(Numeric(12, 2), nullable=True)
    quantity: Mapped[int | None] = mapped_column(nullable=True)
    search_term: Mapped[str | None] = mapped_column(String(512), nullable=True)
    occurred_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    __table_args__ = (
        UniqueConstraint("dedup_key", name="uq_signal_dedup_key"),
        Index("ix_signal_user_id", "user_id"),
        Index("ix_signal_anonymous_id", "anonymous_id"),
        Index("ix_signal_author", "author"),
        Index("ix_signal_category", "category"),
        Index("ix_signal_occurred_at", "occurred_at"),
    )