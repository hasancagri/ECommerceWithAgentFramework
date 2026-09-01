"""Ortak altyapı (shared_kernel): async engine + session fabrikası + declarative Base.

Base tüm domain'lerin tablo metadata'sını paylaşır. Session = Unit of Work (identity map + transaction);
repository YOK — service `AsyncSession`'ı doğrudan kullanır (.NET IDocumentSession deseni, conventions.md).
"""

from __future__ import annotations

from collections.abc import AsyncIterator

from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase

from reco_trainer.config import settings


class Base(DeclarativeBase):
    """Tüm SQLAlchemy tablolarının paylaşılan metadata kökü."""


engine: AsyncEngine = create_async_engine(settings.db_url, pool_pre_ping=True)
session_factory: async_sessionmaker[AsyncSession] = async_sessionmaker(engine, expire_on_commit=False)


async def get_session() -> AsyncIterator[AsyncSession]:
    """FastAPI Depends sağlayıcısı — istek başına session (commit çağıran service'te)."""
    async with session_factory() as session:
        yield session