"""Composition root: FastAPI + FastStream broker + feature router'ları. Sistem hep Aspire'dan başlar.

Şema otomatik kurulur (lifespan `create_all`, projedeki Marten `ApplyAllDatabaseChangesOnStartup` emsali).
Alembic dosyaları (`alembic/`) resmi migration kaydı; dev açılışı idempotent create_all kullanır.
"""

from __future__ import annotations

from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager

from fastapi import FastAPI

from reco_trainer.adapters.broker import broker
from reco_trainer.adapters.db import engine
from reco_trainer.adapters.models import Base
from reco_trainer.features.build_profile.endpoint import router as profile_router

# Consumer modülü import edilince FastStream handler'ları broker'a kayıt olur (dekoratör yan etkisi).
from reco_trainer.features.ingest_signals import (
    purchase_consumer,  # noqa: F401  # pyright: ignore[reportUnusedImport]
)
from reco_trainer.features.ingest_signals.http_ingest import router as ingest_router


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncGenerator[None]:
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
    await broker.start()
    try:
        yield
    finally:
        await broker.stop()


app = FastAPI(title="RecoTrainer — 053 kişiselleştirme beyni", lifespan=lifespan)
app.include_router(ingest_router)
app.include_router(profile_router)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
