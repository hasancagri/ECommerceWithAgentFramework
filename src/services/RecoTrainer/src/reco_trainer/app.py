"""Composition root: FastAPI + FastStream broker + domain router'ları. Sistem hep Aspire'dan başlar.

Şema otomatik kurulur (lifespan `create_all`, Marten `ApplyAllDatabaseChangesOnStartup` emsali).
Alembic (`alembic/`) resmi migration kaydı; dev açılışı idempotent create_all kullanır.
"""

from __future__ import annotations

from collections.abc import AsyncGenerator
from contextlib import asynccontextmanager

from fastapi import FastAPI

# event_handlers import edilince (a) FastStream handler broker'a kayıt olur, (b) Signal tablosu
# transitif import zinciriyle Base.metadata'ya kaydolur (dekoratör + metadata yan etkisi).
from reco_trainer.domains.profiles import event_handlers  # noqa: F401  # pyright: ignore[reportUnusedImport]
from reco_trainer.domains.profiles.endpoints import router as profiles_router
from reco_trainer.jobs.scheduler import start_scheduler, stop_scheduler
from reco_trainer.shared.broker import broker
from reco_trainer.shared.db import Base, engine


@asynccontextmanager
async def lifespan(_: FastAPI) -> AsyncGenerator[None]:
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
    await broker.start()
    start_scheduler()  # precompute: periyodik + açılışta bir kez
    try:
        yield
    finally:
        stop_scheduler()
        await broker.stop()


app = FastAPI(title="RecoTrainer — 053 kişiselleştirme beyni", lifespan=lifespan)
app.include_router(profiles_router)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}
