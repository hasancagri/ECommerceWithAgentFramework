"""042 US3 — ALS egitimi: behavior_events -> CSR matris -> implicit ALS -> .npz + popular (R5).

Agirliklar: ProductViewed=1, BasketItemAdded=3; ListShown matrise girmez (impression yalniz depolanir).
Kimlik = user_id varsa o, yoksa anonymous_id (str). Model dosyasi atomik yazilir (tmp + rename).
"""

import logging
import os
import uuid
from datetime import datetime, timezone

import numpy as np
from implicit.als import AlternatingLeastSquares
from scipy.sparse import csr_matrix

import db

logger = logging.getLogger("personalization.train")

_WEIGHTS = {"ProductViewed": 1.0, "BasketItemAdded": 3.0}


def fetch_interactions(conn) -> list[tuple[str, uuid.UUID, float]]:
    rows = conn.execute(
        "SELECT COALESCE(user_id::text, anonymous_id::text), product_id, event_type "
        "FROM behavior_events WHERE event_type = ANY(%s) AND product_id IS NOT NULL",
        (list(_WEIGHTS),)).fetchall()
    return [(identity, product_id, _WEIGHTS[event_type]) for identity, product_id, event_type in rows]


def build_matrix(interactions: list[tuple[str, uuid.UUID, float]]):
    """(kimlik, urun, agirlik) listesinden user×item CSR kurar; tekrarlar toplanir."""
    user_index: dict[str, int] = {}
    item_index: dict[uuid.UUID, int] = {}
    rows, cols, vals = [], [], []
    for identity, product_id, weight in interactions:
        rows.append(user_index.setdefault(identity, len(user_index)))
        cols.append(item_index.setdefault(product_id, len(item_index)))
        vals.append(weight)

    items = list(item_index)
    matrix = csr_matrix((vals, (rows, cols)), shape=(len(user_index), len(items)))
    matrix.sum_duplicates()
    return matrix, user_index, items


def fit_als(matrix: csr_matrix, factors: int = 32, iterations: int = 15) -> AlternatingLeastSquares:
    model = AlternatingLeastSquares(factors=factors, iterations=iterations, random_state=42)
    model.fit(matrix, show_progress=False)
    return model


def save_model(model, user_index: dict[str, int], items: list[uuid.UUID],
               model_dir: str, trained_at: datetime) -> str:
    """Atomik .npz yazimi: once tmp, sonra rename (okuyucu yarim dosya gormez)."""
    os.makedirs(model_dir, exist_ok=True)
    path = os.path.join(model_dir, f"als-{trained_at:%Y%m%d%H%M%S}.npz")
    tmp_path = path + ".tmp"
    users = sorted(user_index, key=user_index.get)
    with open(tmp_path, "wb") as f:
        np.savez(f,
                 user_factors=np.asarray(model.user_factors),
                 item_factors=np.asarray(model.item_factors),
                 users=np.array(users, dtype=object),
                 items=np.array([str(x) for x in items], dtype=object),
                 trained_at=np.array(trained_at.isoformat()))
    os.replace(tmp_path, path)
    return path


def refresh_popular(conn) -> int:
    """Son 7 gunun en cok goruntulenen top-50 urunu — fallback listesi (FR-013)."""
    rows = conn.execute(
        "SELECT product_id, COUNT(*) AS view_count FROM behavior_events "
        "WHERE event_type = 'ProductViewed' AND product_id IS NOT NULL "
        "AND occurred_at > now() - interval '7 days' "
        "GROUP BY product_id ORDER BY view_count DESC, product_id LIMIT 50").fetchall()

    now = datetime.now(timezone.utc)
    with conn.cursor() as cur:
        cur.execute("TRUNCATE popular_products")
        for rank, (product_id, view_count) in enumerate(rows, start=1):
            cur.execute(
                "INSERT INTO popular_products (rank, product_id, view_count, computed_at) "
                "VALUES (%s, %s, %s, %s)", (rank, product_id, view_count, now))
    return len(rows)


def train_once(conninfo: str, model_dir: str) -> dict:
    """Bir egitim turu: veri cek, egit, kaydet, popular yenile, model_runs'a yaz."""
    trained_at = datetime.now(timezone.utc)
    with db.connect(conninfo) as conn:
        interactions = fetch_interactions(conn)
        popular_count = refresh_popular(conn)

        if not interactions:
            conn.execute(
                "INSERT INTO model_runs (trained_at, event_count, user_count, item_count, model_path, status) "
                "VALUES (%s, 0, 0, 0, '', 'SkippedNoData')", (trained_at,))
            conn.commit()
            logger.info("Egitim atlandi: etkilesim yok (popular=%d).", popular_count)
            return {"status": "SkippedNoData", "eventCount": 0, "userCount": 0, "itemCount": 0}

        matrix, user_index, items = build_matrix(interactions)
        model = fit_als(matrix)
        path = save_model(model, user_index, items, model_dir, trained_at)

        conn.execute(
            "INSERT INTO model_runs (trained_at, event_count, user_count, item_count, model_path, status) "
            "VALUES (%s, %s, %s, %s, %s, 'Succeeded')",
            (trained_at, len(interactions), len(user_index), len(items), path))
        conn.commit()

    logger.info("Egitim tamam: %d etkilesim, %d kullanici, %d urun -> %s",
                len(interactions), len(user_index), len(items), path)
    return {"status": "Succeeded", "eventCount": len(interactions),
            "userCount": len(user_index), "itemCount": len(items), "modelPath": path}