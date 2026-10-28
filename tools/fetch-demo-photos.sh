#!/usr/bin/env bash
# Downloads the real property photos used by the development seed (Data/DevSeed.cs) into
# src/Amlak.Web/wwwroot/img/demo. Source: Unsplash, under the Unsplash License (free for commercial use,
# no permission needed; see https://unsplash.com/license). The list is tools/demo-photos.tsv.
# Usage: tools/fetch-demo-photos.sh   (then delete the dev database so the seed runs again)
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/src/Amlak.Web/wwwroot/img/demo"
mkdir -p "$out"
ok=0; fail=0
while IFS=$'\t' read -r file id what; do
  [[ -z "$file" || "$file" == \#* ]] && continue
  if [[ -s "$out/$file" ]]; then ok=$((ok+1)); continue; fi
  # 1600px wide JPEG, the largest size the site's WebP variants use.
  if curl -fsSL --retry 2 -o "$out/$file" "https://images.unsplash.com/$id?w=1600&q=80&fm=jpg&fit=crop"; then
    echo "ok   $file  $what"; ok=$((ok+1))
  else
    echo "FAIL $file  $id" >&2; rm -f "$out/$file"; fail=$((fail+1))
  fi
done < "$root/tools/demo-photos.tsv"
echo "$ok photos ready, $fail failed -> $out"
