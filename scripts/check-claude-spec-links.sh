#!/usr/bin/env bash
# CLAUDE.md'deki her specs/NNN-* yolunun gerçekten var olduğunu doğrular (sessiz çürüme guard'ı).
# Kullanım: scripts/check-claude-spec-links.sh  (CI'da veya commit öncesi)
set -euo pipefail
cd "$(dirname "$0")/.."
miss=0
for p in $(grep -oE 'specs/[0-9]{3}-[a-z0-9-]+' CLAUDE.md | sort -u); do
  [ -d "$p" ] || { echo "KIRIK spec yolu: $p"; miss=1; }
done
[ "$miss" = 0 ] && echo "CLAUDE.md spec yolları: hepsi var" || { echo "CLAUDE.md kırık spec yolu içeriyor"; exit 1; }
