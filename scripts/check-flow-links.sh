#!/usr/bin/env bash
# İLKE VII guard: her BC'nin FLOW.md'sindeki kenar-anchor tip/sınıf adlarının kod tabanında hâlâ
# VAR olduğunu doğrular. Rename/silme driftini yakalar (adım-sırası driftini DEĞİL — o review'a kalır).
#
# Anchor = FLOW.md'de BACKTICK'li kod referansları, ör: `(PoolProduct.UpsertListing → CanonicalProductUpserted)`
# Oradan PascalCase tanımlayıcılar çıkarılır; her biri `src/` altında aranır. Düz prose parantezi
# (ör. "(EventStorming altitude)") backtick'siz olduğundan taranmaz.
# Bulunamayan = bayat anchor → exit 1.
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

# Aranacak FLOW.md'ler (BC kökleri). macOS bash 3.2 uyumlu (mapfile yok).
flow_files=()
while IFS= read -r f; do
    [ -n "$f" ] && flow_files+=("$f")
done < <(find src -name FLOW.md 2>/dev/null | sort)

if [ "${#flow_files[@]}" -eq 0 ]; then
    echo "check-flow-links: hiç FLOW.md bulunamadı (src/**/FLOW.md)."
    exit 0
fi

fail=0
total_anchors=0

for flow in "${flow_files[@]}"; do
    # Backtick'li kod referanslarını al, PascalCase (en az bir küçük harf) tokenları çıkar, tekilleştir.
    tokens=$(grep -oE '`[^`]+`' "$flow" \
        | grep -oE '\b[A-Z][A-Za-z0-9]*[a-z][A-Za-z0-9]*\b' \
        | sort -u || true)

    for tok in $tokens; do
        total_anchors=$((total_anchors + 1))
        # Kod tabanında (src/, .cs veya .py) herhangi bir geçiş yeterli — tanım ya da kullanım.
        if ! grep -rqE "\b${tok}\b" --include='*.cs' --include='*.py' src 2>/dev/null; then
            echo "STALE  $flow: '$tok' kod tabanında yok (rename/silme? FLOW.md güncelle)."
            fail=1
        fi
    done
done

if [ "$fail" -ne 0 ]; then
    echo "check-flow-links: BAYAT anchor(lar) var — FLOW.md domain süreciyle hizasız (İLKE VII)."
    exit 1
fi

echo "check-flow-links: OK — ${#flow_files[@]} FLOW.md, $total_anchors anchor, hepsi kodda mevcut."
