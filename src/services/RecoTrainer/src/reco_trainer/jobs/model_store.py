"""Fitted model saf-dosya registry: versiyonlu joblib. DB tablosu YOK — dosya = artifact (S3'e portlanabilir).

`vectorizer-v{N}.joblib`. Latest = en yüksek N. recompute yeni versiyon yazar; faz-2/inceleme `load_latest`
ile geri okur (faz-1 serving okumaz — profiller precompute DB'de). Dizin gitignore'lı (commit edilmez).
"""

from __future__ import annotations

import re
from pathlib import Path

import joblib
from sklearn.feature_extraction.text import TfidfVectorizer

from reco_trainer.config import settings

_PATTERN = re.compile(r"vectorizer-v(\d+)\.joblib$")


def _dir() -> Path:
    path = Path(settings.model_dir)
    path.mkdir(parents=True, exist_ok=True)
    return path


def latest_version() -> int:
    """En yüksek mevcut versiyon; hiç yoksa 0."""
    versions = [
        int(m.group(1)) for f in _dir().glob("vectorizer-v*.joblib") if (m := _PATTERN.search(f.name))
    ]
    return max(versions, default=0)


def save_vectorizer(vectorizer: TfidfVectorizer) -> int:
    """Yeni versiyon (latest+1) olarak joblib yazar; versiyonu döner (registry append)."""
    version = latest_version() + 1
    joblib.dump(vectorizer, _dir() / f"vectorizer-v{version}.joblib")
    return version


def load_latest() -> tuple[int, TfidfVectorizer] | None:
    """En son fitted vectorizer'ı yükler (faz-2/inceleme). Hiç yoksa None."""
    version = latest_version()
    if version == 0:
        return None
    return version, joblib.load(_dir() / f"vectorizer-v{version}.joblib")