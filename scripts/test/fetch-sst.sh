#!/bin/sh
# SingleStepTests/r3000 のテストデータを取得する
# 取得済みで一致するファイルは取り直さない
set -eu

COMMIT=51e227447d0b93882a8f13c87c6cee72819efdd3

cd "$(dirname "$0")/.."
out=artifacts/sst/r3000
mkdir -p "$out"
while read -r sum name; do
  if [ -f "$out/$name" ] && echo "$sum  $out/$name" | sha256sum -c --status; then
    continue
  fi
  curl -fsSL "https://raw.githubusercontent.com/SingleStepTests/r3000/$COMMIT/v1/$name" -o "$out/$name"
  if ! echo "$sum  $out/$name" | sha256sum -c --status; then
    echo "$out/$name: sha256 mismatch" >&2
    exit 1
  fi
done < scripts/test/sst-r3000.sha256
