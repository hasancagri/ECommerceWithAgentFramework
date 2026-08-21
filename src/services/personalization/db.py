"""042 Personalization — psycopg baglanti + sema init (data-model.md birebir)."""

import psycopg

SCHEMA_SQL = """
CREATE TABLE IF NOT EXISTS behavior_events (
    id              bigserial PRIMARY KEY,
    event_type      text        NOT NULL,
    channel         text        NOT NULL,
    user_id         uuid        NULL,
    anonymous_id    uuid        NOT NULL,
    product_id      uuid        NULL,
    brand           text        NULL,
    category        text        NULL,
    price           numeric(18,2) NULL,
    search_term     text        NULL,
    shown_product_ids uuid[]    NULL,
    session_id      uuid        NOT NULL,
    occurred_at     timestamptz NOT NULL,
    schema_version  int         NOT NULL,
    source_file     text        NOT NULL,
    line_no         int         NOT NULL,
    UNIQUE (source_file, line_no)
);
CREATE INDEX IF NOT EXISTS ix_behavior_events_user_id ON behavior_events (user_id);
CREATE INDEX IF NOT EXISTS ix_behavior_events_anonymous_id ON behavior_events (anonymous_id);
CREATE INDEX IF NOT EXISTS ix_behavior_events_occurred_at ON behavior_events (occurred_at);

CREATE TABLE IF NOT EXISTS ingest_offsets (
    file_name     text PRIMARY KEY,
    byte_offset   bigint      NOT NULL,
    line_count    int         NOT NULL,
    skipped_count int         NOT NULL,
    updated_at    timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS model_runs (
    id          bigserial PRIMARY KEY,
    trained_at  timestamptz NOT NULL,
    event_count bigint      NOT NULL,
    user_count  int         NOT NULL,
    item_count  int         NOT NULL,
    model_path  text        NOT NULL,
    status      text        NOT NULL
);

CREATE TABLE IF NOT EXISTS popular_products (
    rank        int PRIMARY KEY,
    product_id  uuid        NOT NULL,
    view_count  bigint      NOT NULL,
    computed_at timestamptz NOT NULL
);
"""


def connect(conninfo: str) -> psycopg.Connection:
    return psycopg.connect(conninfo)


def init_schema(conninfo: str) -> None:
    with connect(conninfo) as conn:
        conn.execute(SCHEMA_SQL)
        conn.commit()