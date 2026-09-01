"""taste_profile tablosu (053 precompute edilmiş profil; serving buradan okur)

Revision ID: 0002_taste_profile
Revises: 0001_signal
Create Date: 2026-09-01
"""
from __future__ import annotations

from collections.abc import Sequence

import sqlalchemy as sa
from alembic import op
from sqlalchemy.dialects import postgresql

revision: str = "0002_taste_profile"
down_revision: str | None = "0001_signal"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def upgrade() -> None:
    op.create_table(
        "taste_profile",
        sa.Column("id", postgresql.UUID(as_uuid=True), primary_key=True),
        sa.Column("user_id", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("anonymous_id", postgresql.UUID(as_uuid=True), nullable=True),
        sa.Column("payload", postgresql.JSONB(), nullable=False),
        sa.Column("model_version", sa.Integer(), nullable=False),
        sa.Column("computed_at", sa.DateTime(timezone=True), nullable=False),
    )
    op.create_index("ix_taste_profile_user_id", "taste_profile", ["user_id"])
    op.create_index("ix_taste_profile_anonymous_id", "taste_profile", ["anonymous_id"])


def downgrade() -> None:
    op.drop_table("taste_profile")