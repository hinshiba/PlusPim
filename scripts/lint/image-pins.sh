#!/bin/sh
# digestで固定したコンテナイメージは，同じ名前とタグならどのファイルでも同じdigestにする
# dependabot非対応のパッケージ管理のため
set -eu

pins=$(git grep --untracked -hoE -- '[a-z0-9./-]+:[A-Za-z0-9._-]+@sha256:[0-9a-f]{64}' -- ':!scripts/lint/' | sort -u)
dups=$(printf '%s\n' "$pins" | sed 's/@.*//' | sort | uniq -d)
[ -z "$dups" ] && exit 0
for image in $dups; do
  echo "error: $image is pinned to different digests:" >&2
  git grep --untracked -nF -- "$image@sha256:" -- ':!scripts/lint/' >&2
done
exit 1
