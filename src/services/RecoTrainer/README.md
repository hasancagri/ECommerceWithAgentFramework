# RecoTrainer — 053 kişiselleştirme beyni (Python)

Sinyal feature store + zevk profili türetimi. `docs/python-conventions.md` disiplininde.
Sistem hep Aspire AppHost'tan başlar (`AddUvicornApp`). Ayrı PyCharm projesi (`.slnx` dışı).

## Yerel

```bash
uv sync
uv run ruff check .
uv run pyright
uv run pytest
```

Domain süreci: `FLOW.md`. Sözleşmeler: `specs/053-personalized-home-feed/contracts/`.