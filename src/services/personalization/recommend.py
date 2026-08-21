"""042 US3 — oneri zinciri: personal -> session -> popular; asla bos donmez (FR-012, FR-013).

Model swap: ModelStore tek referansi kilitle degistirir — egitim sirasinda sorgu kesintisi yok (R5).
"""

import logging
import threading
import uuid
from dataclasses import dataclass, field

import numpy as np

import db

logger = logging.getLogger("personalization.recommend")


@dataclass
class AlsModel:
    user_factors: np.ndarray
    item_factors: np.ndarray
    user_index: dict[str, int]
    items: list[uuid.UUID]
    trained_at: str
    matrix: object = None  # egitim-ici cagrida gezilenleri dislamak icin; npz'den yuklemede None
    item_index: dict[uuid.UUID, int] = field(init=False)

    def __post_init__(self):
        self.item_index = {item: i for i, item in enumerate(self.items)}


def load_npz(path: str) -> AlsModel:
    data = np.load(path, allow_pickle=True)
    users = [str(u) for u in data["users"]]
    return AlsModel(
        user_factors=data["user_factors"],
        item_factors=data["item_factors"],
        user_index={u: i for i, u in enumerate(users)},
        items=[uuid.UUID(str(x)) for x in data["items"]],
        trained_at=str(data["trained_at"]),
    )


def _top_ids(scores: np.ndarray, items: list[uuid.UUID], exclude: set[int], count: int) -> list[uuid.UUID]:
    order = np.argsort(scores)[::-1]
    result = []
    for col in order:
        if int(col) in exclude:
            continue
        result.append(items[int(col)])
        if len(result) >= count:
            break
    return result


def recommend(model: AlsModel | None, popular: list[uuid.UUID], identity: str,
              session_product_ids: list[uuid.UUID], count: int) -> tuple[list[uuid.UUID], str]:
    """Oneri zinciri. Donus: (urun listesi, kaynak: personal|session|popular)."""
    if model is not None:
        row = model.user_index.get(identity)
        if row is not None:
            scores = model.user_factors[row] @ model.item_factors.T
            seen: set[int] = set()
            if model.matrix is not None:
                seen = set(model.matrix[row].indices)
            ids = _top_ids(scores, model.items, seen, count)
            if ids:
                return ids, "personal"

        session_cols = [model.item_index[p] for p in session_product_ids if p in model.item_index]
        if session_cols:
            profile = model.item_factors[session_cols].mean(axis=0)
            scores = profile @ model.item_factors.T
            ids = _top_ids(scores, model.items, set(session_cols), count)
            if ids:
                return ids, "session"

    return popular[:count], "popular"


def fetch_popular(conninfo: str) -> list[uuid.UUID]:
    with db.connect(conninfo) as conn:
        rows = conn.execute("SELECT product_id FROM popular_products ORDER BY rank").fetchall()
    return [r[0] for r in rows]


class ModelStore:
    """Sureç-ici tek model referansi; egitim sonrasi kilitli swap (FR-011)."""

    def __init__(self):
        self._lock = threading.Lock()
        self._model: AlsModel | None = None
        self.popular: list[uuid.UUID] = []

    @property
    def model(self) -> AlsModel | None:
        with self._lock:
            return self._model

    def set(self, model: AlsModel | None):
        with self._lock:
            self._model = model

    def reload(self, conninfo: str):
        """En son basarili modeli + popular listesini DB'den yukler; model yoksa fallback'te kalir."""
        self.popular = fetch_popular(conninfo)
        with db.connect(conninfo) as conn:
            row = conn.execute(
                "SELECT model_path FROM model_runs WHERE status = 'Succeeded' "
                "ORDER BY trained_at DESC LIMIT 1").fetchone()
        if row is None:
            return
        try:
            self.set(load_npz(row[0]))
            logger.info("Model yuklendi: %s", row[0])
        except (OSError, KeyError, ValueError) as ex:
            logger.warning("Model dosyasi yuklenemedi (%s): %s", row[0], ex)