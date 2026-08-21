"""042 US2 — parse + dosya-offset mantigi testleri (saf kisim; DB idempotency canli dogrulamada).

Kontrat: specs/042-behavior-personalization/contracts/behavior-log-line.md
"""

import json
import uuid

from ingest import parse_line, read_new_lines

AID = str(uuid.uuid4())
SID = str(uuid.uuid4())
UID = str(uuid.uuid4())
PID = str(uuid.uuid4())
TS = "2026-08-21T14:03:22.512Z"


def _line(**overrides):
    base = {"channel": "web", "anonymousId": AID, "sessionId": SID, "timestamp": TS, "schemaVersion": 1}
    base.update(overrides)
    return json.dumps(base)


class TestParseLine:
    def test_product_viewed_valid(self):
        row = parse_line(_line(eventType="ProductViewed", userId=UID, productId=PID,
                               brand="Acme", category="Telefon", price=18999.90))
        assert row is not None
        assert row["event_type"] == "ProductViewed"
        assert row["user_id"] == uuid.UUID(UID)
        assert row["product_id"] == uuid.UUID(PID)
        assert row["brand"] == "Acme"
        assert float(row["price"]) == 18999.90

    def test_list_shown_valid(self):
        shown = [str(uuid.uuid4()), str(uuid.uuid4())]
        row = parse_line(_line(eventType="ListShown", shownProductIds=shown))
        assert row is not None
        assert row["shown_product_ids"] == [uuid.UUID(x) for x in shown]
        assert row["user_id"] is None

    def test_search_performed_valid(self):
        row = parse_line(_line(eventType="SearchPerformed", searchTerm="kablosuz kulaklik"))
        assert row is not None
        assert row["search_term"] == "kablosuz kulaklik"

    def test_basket_item_added_valid(self):
        row = parse_line(_line(eventType="BasketItemAdded", userId=UID, productId=PID,
                               brand="Acme", category="Telefon", price=1.5))
        assert row is not None
        assert row["event_type"] == "BasketItemAdded"

    def test_broken_json_returns_none(self):
        assert parse_line("{bozuk json") is None

    def test_unknown_event_type_returns_none(self):
        assert parse_line(_line(eventType="Bilinmeyen")) is None

    def test_missing_required_field_returns_none(self):
        # ProductViewed productId ister
        assert parse_line(_line(eventType="ProductViewed")) is None
        # anonymousId her tipte zorunlu
        raw = json.loads(_line(eventType="SearchPerformed", searchTerm="x"))
        del raw["anonymousId"]
        assert parse_line(json.dumps(raw)) is None

    def test_unknown_schema_version_returns_none(self):
        assert parse_line(_line(eventType="SearchPerformed", searchTerm="x", schemaVersion=2)) is None

    def test_unknown_extra_field_ignored(self):
        row = parse_line(_line(eventType="SearchPerformed", searchTerm="x", yeniAlan="yok say"))
        assert row is not None


class TestReadNewLines:
    def test_reads_complete_lines_and_advances_offset(self, tmp_path):
        p = tmp_path / "behavior-20260821.jsonl"
        p.write_text("satir1\nsatir2\n", encoding="utf-8")

        lines, offset = read_new_lines(str(p), 0)
        assert lines == ["satir1", "satir2"]

        # ayni offset'ten tekrar okuma: yeni satir yok (idempotent ilerleme)
        lines2, offset2 = read_new_lines(str(p), offset)
        assert lines2 == []
        assert offset2 == offset

    def test_partial_last_line_left_for_next_round(self, tmp_path):
        p = tmp_path / "behavior-20260821.jsonl"
        p.write_text("tam satir\nyarim sat", encoding="utf-8")

        lines, offset = read_new_lines(str(p), 0)
        assert lines == ["tam satir"]

        # yazici satiri tamamlayinca kaldigi yerden okunur
        with open(p, "a", encoding="utf-8") as f:
            f.write("ir\n")
        lines2, _ = read_new_lines(str(p), offset)
        assert lines2 == ["yarim satir"]

    def test_appended_lines_read_from_stored_offset(self, tmp_path):
        p = tmp_path / "behavior-20260821.jsonl"
        p.write_text("a\n", encoding="utf-8")
        _, offset = read_new_lines(str(p), 0)

        with open(p, "a", encoding="utf-8") as f:
            f.write("b\n")
        lines, _ = read_new_lines(str(p), offset)
        assert lines == ["b"]