"""APScheduler (in-process, BackgroundService emsali): recompute'u periyodik + açılışta bir kez tetikler.

Aynı uvicorn sürecinde; app.py lifespan başlatır/durdurur. Job request-dışı, kendi session'ını açar.
"""

from __future__ import annotations

from apscheduler.schedulers.asyncio import AsyncIOScheduler

from reco_trainer.config import settings
from reco_trainer.jobs.recompute_profiles import RecomputeProfilesJob
from reco_trainer.shared.db import session_factory

scheduler = AsyncIOScheduler()


async def run_recompute() -> None:
    async with session_factory() as session:
        await RecomputeProfilesJob(session).run()


def start_scheduler() -> None:
    """Periyodik job + açılışta bir kez (soğuk veri hızlı ısınsın)."""
    scheduler.add_job(run_recompute, "interval", minutes=settings.recompute_interval_minutes)
    scheduler.add_job(run_recompute)
    scheduler.start()


def stop_scheduler() -> None:
    if scheduler.running:
        scheduler.shutdown(wait=False)