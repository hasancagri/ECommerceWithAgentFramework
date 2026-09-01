"""signal tablosu (053 tek birleşik sinyal feature store)

Revision ID: 0001_signal
Revises:
Create Date: 2026-09-01
"""
from __future__ import annotations

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "0001_signal"
down_revision: str | None = None
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.create_table(
        "signal",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("dedup_key", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("event_type", sa.String(length=32), nullable=False),
        sa.Column("user_id", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("anonymous_id", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("product_id", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("author", sa.String(length=256), nullable=True),
        sa.Column("category", sa.String(length=256), nullable=True),
        sa.Column("price", sa.Numeric(precision=12, scale=2), nullable=True),
        sa.Column("quantity", sa.Integer(), nullable=True),
        sa.Column("search_term", sa.String(length=512), nullable=True),
        sa.Column("occurred_at", sa.DateTime(timezone=True), nullable=False),
        sa.UniqueConstraint("dedup_key", name="uq_signal_dedup_key"),
    )
    op.create_index("ix_signal_user_id", "signal", ["user_id"])
    op.create_index("ix_signal_anonymous_id", "signal", ["anonymous_id"])
    op.create_index("ix_signal_author", "signal", ["author"])
    op.create_index("ix_signal_category", "signal", ["category"])
    op.create_index("ix_signal_occurred_at", "signal", ["occurred_at"])


def downgrade() -> None:
    op.drop_table("signal")