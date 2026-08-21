"""042 US2 — JSONL ingest: offset-takipli, idempotent, bozuk satiri atlayan aktarim (R4).

Kontrat: specs/042-behavior-personalization/contracts/behavior-log-line.md
Idempotency: INSERT ... ON CONFLICT (source_file, line_no) DO NOTHING + offset guncellemesi
ayni transaction'da; offset kaybolsa bile UNIQUE cift kaydi engeller.
"""

import glob
import json
import logging
import os
import uuid
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation

import db

logger = logging.getLogger("personalization.ingest")

_EVENT_TYPES = {"ProductViewed", "ListShown", "SearchPerformed", "BasketItemAdded"}
_PRODUCT_EVENTS = {"ProductViewed", "BasketItemAdded"}


def _uuid_or_none(value):
    try:
        return uuid.UUID(value) if value else None
    except (ValueError, AttributeError, TypeError):
        return None


def parse_line(raw: str) -> dict | None:
    """Tek JSONL satirini dogrulanmis DB satirina cevirir; gecersizse None (FR-010)."""
    try:
        data = json.loads(raw)
    except (json.JSONDecodeError, UnicodeDecodeError):
        return None
    if not isinstance(data, dict):
        return None
    if data.get("schemaVersion") != 1:
        return None

    event_type = data.get("eventType")
    if event_type not in _EVENT_TYPES:
        return None

    anonymous_id = _uuid_or_none(data.get("anonymousId"))
    session_id = _uuid_or_none(data.get("sessionId"))
    if anonymous_id is None or session_id is None:
        return None

    try:
        occurred_at = datetime.fromisoformat(str(data.get("timestamp")).replace("Z", "+00:00"))
    except (ValueError, TypeError):
        return None

    product_id = _uuid_or_none(data.get("productId"))
    price = None
    if data.get("price") is not None:
        try:
            price = Decimal(str(data["price"]))
        except InvalidOperation:
            return None

    shown = None
    if data.get("shownProductIds") is not None:
        shown = [_uuid_or_none(x) for x in data["shownProductIds"]]
        if not shown or any(x is None for x in shown):
            return None

    # tip-bazli zorunlu alanlar
    if event_type in _PRODUCT_EVENTS and product_id is None:
        return None
    if event_type == "SearchPerformed" and not data.get("searchTerm"):
        return None
    if event_type == "ListShown" and shown is None:
        return None

    return {
        "event_type": event_type,
        "channel": data.get("channel") or "web",
        "user_id": _uuid_or_none(data.get("userId")),
        "anonymous_id": anonymous_id,
        "product_id": product_id,
        "brand": data.get("brand"),
        "category": data.get("category"),
        "price": price,
        "search_term": data.get("searchTerm"),
        "shown_product_ids": shown,
        "session_id": session_id,
        "occurred_at": occurred_at,
        "schema_version": 1,
    }


def read_new_lines(path: str, offset: int) -> tuple[list[str], int]:
    """offset'ten itibaren TAMAMLANMIS (\\n ile biten) satirlari okur; yarim satir sonraki tura kalir."""
    with open(path, "rb") as f:
        f.seek(offset)
        chunk = f.read()

    last_newline = chunk.rfind(b"\n")
    if last_newline < 0:
        return [], offset

    complete = chunk[: last_newline + 1]
    lines = [line.decode("utf-8", errors="replace") for line in complete.split(b"\n")[:-1]]
    return lines, offset + last_newline + 1


_INSERT_SQL = """
INSERT INTO behavior_events (event_type, channel, user_id, anonymous_id, product_id, brand,
    category, price, search_term, shown_product_ids, session_id, occurred_at, schema_version,
    source_file, line_no)
VALUES (%(event_type)s, %(channel)s, %(user_id)s, %(anonymous_id)s, %(product_id)s, %(brand)s,
    %(category)s, %(price)s, %(search_term)s, %(shown_product_ids)s, %(session_id)s,
    %(occurred_at)s, %(schema_version)s, %(source_file)s, %(line_no)s)
ON CONFLICT (source_file, line_no) DO NOTHING
"""

_OFFSET_SQL = """
INSERT INTO ingest_offsets (file_name, byte_offset, line_count, skipped_count, updated_at)
VALUES (%(file_name)s, %(byte_offset)s, %(line_count)s, %(skipped_count)s, %(updated_at)s)
ON CONFLICT (file_name) DO UPDATE SET
    byte_offset = EXCLUDED.byte_offset,
    line_count = EXCLUDED.line_count,
    skipped_count = EXCLUDED.skipped_count,
    updated_at = EXCLUDED.updated_at
"""


def ingest_once(conninfo: str, log_dir: str) -> dict:
    """Tum behavior-*.jsonl dosyalarini bir tur isler. Donus: islenen/atlanan satir sayilari."""
    processed = 0
    skipped = 0

    with db.connect(conninfo) as conn:
        for path in sorted(glob.glob(os.path.join(log_dir, "behavior-*.jsonl"))):
            file_name = os.path.basename(path)

            row = conn.execute(
                "SELECT byte_offset, line_count, skipped_count FROM ingest_offsets WHERE file_name = %s",
                (file_name,)).fetchone()
            offset, line_count, skipped_count = row if row else (0, 0, 0)

            lines, new_offset = read_new_lines(path, offset)
            if not lines:
                continue

            with conn.cursor() as cur:
                for i, raw in enumerate(lines):
                    parsed = parse_line(raw)
                    if parsed is None:
                        skipped += 1
                        skipped_count += 1
                        continue
                    parsed["source_file"] = file_name
                    parsed["line_no"] = line_count + i
                    cur.execute(_INSERT_SQL, parsed)
                    processed += 1

                cur.execute(_OFFSET_SQL, {
                    "file_name": file_name,
                    "byte_offset": new_offset,
                    "line_count": line_count + len(lines),
                    "skipped_count": skipped_count,
                    "updated_at": datetime.now(timezone.utc),
                })
            conn.commit()  # INSERT'ler + offset TEK transaction (R4)

    if skipped:
        logger.warning("Ingest: %d satir atlandi (bozuk/bilinmeyen sema).", skipped)
    return {"processedLines": processed, "skippedLines": skipped}