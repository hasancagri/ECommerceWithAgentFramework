"""042 US3 — matris kurulumu + ALS egitim smoke (sentetik mini veri; FR-011)."""

import uuid

from train import build_matrix, fit_als

U1, U2, U3 = "u1", "u2", "u3"
P1, P2, P3 = uuid.uuid4(), uuid.uuid4(), uuid.uuid4()


def _interactions():
    # u1 ve u2 ayni iki urunu gezdi; u3 yalniz P3. add-to-basket agirligi 3.
    return [
        (U1, P1, 1.0), (U1, P2, 3.0),
        (U2, P1, 1.0), (U2, P2, 1.0),
        (U3, P3, 1.0),
    ]


class TestBuildMatrix:
    def test_shapes_and_weights(self):
        matrix, user_index, items = build_matrix(_interactions())
        assert matrix.shape == (3, 3)
        assert set(user_index) == {U1, U2, U3}
        assert set(items) == {P1, P2, P3}
        # add-to-basket agirligi korunur (view=1, add=3)
        assert matrix[user_index[U1], items.index(P2)] == 3.0

    def test_duplicate_interactions_sum(self):
        matrix, user_index, items = build_matrix([(U1, P1, 1.0), (U1, P1, 1.0)])
        assert matrix[user_index[U1], items.index(P1)] == 2.0


class TestFitAls:
    def test_fit_produces_factors_for_all_users_and_items(self):
        matrix, user_index, items = build_matrix(_interactions())
        model = fit_als(matrix, factors=8, iterations=5)
        assert model.user_factors.shape[0] == 3
        assert model.item_factors.shape[0] == 3