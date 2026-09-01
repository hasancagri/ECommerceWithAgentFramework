"""profiles domain — HTTP giriş (Controller). ProfileService'e delege eder (DI = FastAPI Depends)."""

from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.domains.profiles.profile_service import ProfileService
from reco_trainer.domains.profiles.schema import SignalIn, TasteProfileOut
from reco_trainer.shared.db import get_session

router = APIRouter(prefix="/api/v1", tags=["profiles"])


def get_service(session: AsyncSession = Depends(get_session)) -> ProfileService:
    """DI sağlayıcı (.NET constructor-injection karşılığı): session → ProfileService."""
    return ProfileService(session)


@router.post("/signals", status_code=status.HTTP_202_ACCEPTED)
async def ingest_signals(
    batch: list[SignalIn],
    service: ProfileService = Depends(get_service),
) -> None:
    """Gezinme/arama sinyali batch (kayıp-toleranslı, 202). Geçersiz öge atlanır."""
    await service.ingest_browsing(batch)


@router.get("/taste-profile", response_model=TasteProfileOut, response_model_by_alias=True)
async def taste_profile(
    user_id: uuid.UUID | None = Query(default=None, alias="userId"),
    anonymous_id: uuid.UUID | None = Query(default=None, alias="anonymousId"),
    service: ProfileService = Depends(get_service),
) -> TasteProfileOut:
    """En az bir kimlik zorunlu; ikisi de verilirse birleşik profil (dikiş, FR-013)."""
    if user_id is None and anonymous_id is None:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "En az bir kimlik (userId/anonymousId) gerekli.")
    return await service.get_profile(user_id, anonymous_id)
