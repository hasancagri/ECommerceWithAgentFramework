"""SQLAlchemy tablo modelleri (feature store). Signal = TEK birleşik sinyal tablosu (telemetri)."""

from __future__ import annotations

import uuid
from datetime import datetime
from decimal import Decimal

from sqlalchemy import DateTime, Index, Numeric, String, UniqueConstraint
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column


class Base(DeclarativeBase):
    pass


class Signal(Base):
    """Tüm sinyaller (gezinme + arama + satın-alma) tek tabloda. Profil = bu tablo üstünde GROUP BY.

    data-model.md 053: satın-alma ayrı aggregate DEĞİL — yüksek öncelikli satır. `dedup_key` yalnız
    Purchased'ta dolu (Storefront `Guid.NewGuid()`); `unique` → son-hat idempotency (çift teslimde no-op).
    """

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