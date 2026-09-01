"""FastAPI profil ucu: GET /api/v1/taste-profile?userId=&anonymousId= → SABİT TasteProfile sözleşmesi."""

from __future__ import annotations

import uuid

from fastapi import APIRouter, Depends, HTTPException, Query, status
from sqlalchemy.ext.asyncio import AsyncSession

from reco_trainer.adapters.db import get_session
from reco_trainer.features.build_profile.schema import TasteProfileOut
from reco_trainer.features.build_profile.service import get_taste_profile

router = APIRouter(prefix="/api/v1", tags=["profile"])


@router.get("/taste-profile", response_model=TasteProfileOut, response_model_by_alias=True)
async def taste_profile(
    user_id: uuid.UUID | None = Query(default=None, alias="userId"),
    anonymous_id: uuid.UUID | None = Query(default=None, alias="anonymousId"),
    session: AsyncSession = Depends(get_session),
) -> TasteProfileOut:
    """En az bir kimlik zorunlu; ikisi de verilirse birleşik profil (dikiş, FR-013)."""
    if user_id is None and anonymous_id is None:
        raise HTTPException(status.HTTP_400_BAD_REQUEST, "En az bir kimlik (userId/anonymousId) gerekli.")
    return await get_taste_profile(session, user_id, anonymous_id)
