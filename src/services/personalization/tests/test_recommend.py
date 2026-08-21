"""042 US3 — oneri zinciri testleri: personal -> session -> popular; asla bos donmez (FR-012/013)."""

import uuid

from recommend import AlsModel, recommend
from train import build_matrix, fit_als

U1, U2 = "u1", "u2"
P1, P2, P3, P4 = uuid.uuid4(), uuid.uuid4(), uuid.uuid4(), uuid.uuid4()
POPULAR = [P4, P3]


def _model() -> AlsModel:
    interactions = [
        (U1, P1, 1.0), (U1, P2, 3.0),
        (U2, P1, 1.0), (U2, P2, 1.0), (U2, P3, 1.0),
    ]
    matrix, user_index, items = build_matrix(interactions)
    fitted = fit_als(matrix, factors=8, iterations=5)
    return AlsModel(
        user_factors=fitted.user_factors,
        item_factors=fitted.item_factors,
        user_index=user_index,
        items=items,
        matrix=matrix,
        trained_at="2026-08-21T14:00:00Z",
    )


class TestRecommend:
    def test_known_identity_returns_personal(self):
        ids, source = recommend(_model(), POPULAR, identity=U1, session_product_ids=[], count=10)
        assert source == "personal"
        assert ids  # bos degil
        assert all(isinstance(x, uuid.UUID) for x in ids)

    def test_unknown_identity_with_session_products_returns_session(self):
        ids, source = recommend(_model(), POPULAR, identity="taninmayan",
                                session_product_ids=[P1], count=10)
        assert source == "session"
        assert ids
        assert P1 not in ids  # gezilen urun kendisi onerilmez

    def test_unknown_identity_without_session_returns_popular(self):
        ids, source = recommend(_model(), POPULAR, identity="taninmayan",
                                session_product_ids=[], count=10)
        assert source == "popular"
        assert ids == POPULAR

    def test_no_model_returns_popular(self):
        ids, source = recommend(None, POPULAR, identity=U1, session_product_ids=[P1], count=10)
        assert source == "popular"
        assert ids == POPULAR

    def test_count_limits_results(self):
        ids, _ = recommend(_model(), POPULAR, identity=U2, session_product_ids=[], count=1)
        assert len(ids) == 1

    def test_session_products_unknown_to_model_falls_back_to_popular(self):
        yabanci = uuid.uuid4()
        ids, source = recommend(_model(), POPULAR, identity="taninmayan",
                                session_product_ids=[yabanci], count=10)
        assert source == "popular"
        assert ids == POPULAR