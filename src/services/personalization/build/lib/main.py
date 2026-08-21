"""042 Personalization — FastAPI: ingest + egitim zamanlayicisi + oneri API.

Kontrat: specs/042-behavior-personalization/contracts/recommendations-api.md
Zamanlama: APScheduler (ingest 30 sn, egitim 10 dk; env ile ayarlanir). Dev tetikleri:
POST /v1/admin/ingest + /v1/admin/train (Procurement feeds/pull emsali, anonim).
"""

import logging
import uuid
from contextlib import asynccontextmanager
from datetime import datetime, timezone

from apscheduler.schedulers.background import BackgroundScheduler
from fastapi import FastAPI, HTTPException, Query

import db
import ingest
import train
from config import load_settings
from recommend import ModelStore, recommend

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("personalization")

settings = None
store = ModelStore()
last_ingest_at: datetime | None = None


def run_ingest():
    global last_ingest_at
    result = ingest.ingest_once(settings.conninfo, settings.behavior_log_dir)
    last_ingest_at = datetime.now(timezone.utc)
    return result


def run_train():
    result = train.train_once(settings.conninfo, settings.model_dir)
    store.reload(settings.conninfo)
    return result


@asynccontextmanager
async def lifespan(app: FastAPI):
    global settings
    settings = load_settings()
    db.init_schema(settings.conninfo)
    store.reload(settings.conninfo)

    scheduler = BackgroundScheduler()
    scheduler.add_job(run_ingest, "interval", seconds=settings.ingest_interval_seconds,
                      max_instances=1, coalesce=True)
    scheduler.add_job(run_train, "interval", seconds=settings.train_interval_seconds,
                      max_instances=1, coalesce=True)
    scheduler.start()
    logger.info("Personalization hazir (ingest=%ds, train=%ds).",
                settings.ingest_interval_seconds, settings.train_interval_seconds)
    yield
    scheduler.shutdown(wait=False)


app = FastAPI(title="Personalization", lifespan=lifespan)


@app.get("/health")
def health():
    return {
        "status": "ok",
        "modelLoaded": store.model is not None,
        "lastIngestAt": last_ingest_at.isoformat() if last_ingest_at else None,
    }


@app.get("/v1/recommendations")
def recommendations(
    userId: uuid.UUID | None = None,
    anonymousId: uuid.UUID | None = None,
    sessionProductIds: str | None = None,
    count: int = Query(default=10, ge=1, le=50),
):
    if userId is None and anonymousId is None:
        raise HTTPException(status_code=400, detail="userId veya anonymousId zorunlu")

    session_products: list[uuid.UUID] = []
    if sessionProductIds:
        for chunk in sessionProductIds.split(","):
            try:
                session_products.append(uuid.UUID(chunk.strip()))
            except ValueError:
                continue  # bozuk id yok sayilir — sorgu yine yanitlanir

    # Kimlik oncelik sirasi egitimle ayni: user varsa user, yoksa anonim (R5).
    identity = str(userId) if userId else str(anonymousId)
    model = store.model
    ids, source = recommend(model, store.popular, identity, session_products, count)

    return {
        "productIds": [str(x) for x in ids],
        "source": source,
        "modelTrainedAt": model.trained_at if model and source != "popular" else None,
    }


@app.post("/v1/admin/ingest")
def admin_ingest():
    return run_ingest()


@app.post("/v1/admin/train")
def admin_train():
    return run_train()


if __name__ == "__main__":
    import os

    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=int(os.environ.get("PORT", "8000")))
