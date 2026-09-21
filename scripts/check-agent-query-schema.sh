#!/usr/bin/env bash
# 069 R8 guard: storefront_sellable şema tek-kaynağındaki (StorefrontSellableSchema.cs) her kolon
# adının sorgu rehberinin KANONİK evinde geçtiğini doğrular — kolon ekleme/silme/yeniden adlandırma
# driftini yakalar. 070 US5: kanonik ev ChatAgent prompt'undan query_storefront tool description'ına
# (StorefrontMcpTools.cs) taşındı; ChatAgent kopyası donduruldu, guard YALNIZ yeni evi denetler.
# BİLİNÇLİ SINIR: tek yönlü (rehberdeki bayat kolonu ve tip/açıklama driftini yakalamaz — review).
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"

schema_file="src/services/storefront/Storefront.Api/AgentSql/StorefrontSellableSchema.cs"
prompt_file="src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Agents/Queries/QueryStorefront.cs"

for f in "$schema_file" "$prompt_file"; do
    if [ ! -f "$f" ]; then
        echo "check-agent-query-schema: dosya yok: $f"
        exit 1
    fi
done

# Kolon satırı formatı: new("kolon_adı", ... (StorefrontSellableSchema.Columns listesi).
columns=$(grep -oE 'new\("[a-z_]+"' "$schema_file" | sed -E 's/new\("([a-z_]+)"/\1/' | sort -u)

if [ -z "$columns" ]; then
    echo "check-agent-query-schema: $schema_file içinde kolon bulunamadı (format değişti mi?)."
    exit 1
fi

fail=0
total=0
for col in $columns; do
    total=$((total + 1))
    if ! grep -qE "\b${col}\b" "$prompt_file"; then
        echo "DRIFT  '$col' kolonu sorgu rehberinde ($prompt_file) geçmiyor — şema bloğunu güncelle."
        fail=1
    fi
done

if [ "$fail" -ne 0 ]; then
    echo "check-agent-query-schema: DRIFT var — view şeması ↔ tool description şema bloğu hizasız."
    exit 1
fi

echo "check-agent-query-schema: OK — $total kolon, hepsi tool description'da mevcut."
