"""042 Personalization — env'den ayarlar (Aspire enjeksiyonu; bkz. research R2/R3)."""

import os
from dataclasses import dataclass


def _pg_conninfo_from_dotnet(conn: str) -> str:
    """Aspire'in enjekte ettigi .NET-stili connection string'i psycopg conninfo'ya cevirir."""
    key_map = {
        "host": "host",
        "port": "port",
        "username": "user",
        "user id": "user",
        "password": "password",
        "database": "dbname",
    }
    parts: dict[str, str] = {}
    for chunk in conn.split(";"):
        if "=" not in chunk:
            continue
        key, value = chunk.split("=", 1)
        mapped = key_map.get(key.strip().lower())
        if mapped:
            parts[mapped] = value.strip()
    return " ".join(f"{k}={v}" for k, v in parts.items())


@dataclass(frozen=True)
class Settings:
    conninfo: str
    behavior_log_dir: str
    port: int
    ingest_interval_seconds: int
    train_interval_seconds: int
    model_dir: str


def load_settings() -> Settings:
    conn = os.environ["ConnectionStrings__personalizationDb"]
    return Settings(
        conninfo=_pg_conninfo_from_dotnet(conn),
        behavior_log_dir=os.environ["BEHAVIOR_LOG_DIR"],
        port=int(os.environ.get("PORT", "8000")),
        ingest_interval_seconds=int(os.environ.get("INGEST_INTERVAL_SECONDS", "30")),
        train_interval_seconds=int(os.environ.get("TRAIN_INTERVAL_SECONDS", "600")),
        model_dir=os.environ.get("MODEL_DIR", os.path.join(os.path.dirname(__file__), "models")),
    )