"""Async SQLAlchemy engine + session fabrikası. Composition root (app.py) tüketir; FastAPI Depends sağlar."""

from __future__ import annotations

from collections.abc import AsyncIterator

from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession, async_sessionmaker, create_async_engine

from reco_trainer.config import settings

engine: AsyncEngine = create_async_engine(settings.db_url, pool_pre_ping=True)
session_factory: async_sessionmaker[AsyncSession] = async_sessionmaker(engine, expire_on_commit=False)


async def get_session() -> AsyncIterator[AsyncSession]:
    """FastAPI Depends sağlayıcısı — istek başına session (commit çağıran feature'da)."""
    async with session_factory() as session:
        yield session