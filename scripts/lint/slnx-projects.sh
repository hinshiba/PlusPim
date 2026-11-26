#!/bin/sh
# すべてのプロジェクトが`PlusPim.slnx`に属するようにする
# CIのフォーマットとリントを強制するため
set -eu

status=0
for project in $(git ls-files --cached --others --exclude-standard '*.csproj'); do
  if ! grep -qF "Path=\"$project\"" PlusPim.slnx; then
    echo "$project: error: not listed in PlusPim.slnx" >&2
    status=1
  fi
done
exit $status
