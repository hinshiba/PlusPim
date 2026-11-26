#!/bin/sh
# switch式の破棄パターンのアームが投げる例外はUnreachableExceptionにする
# C#のenumはデフォルトを要求することがあるが，そのアームは列挙子が網羅されていれば到達しないため
set -eu

if git grep --untracked -nP -- '_ => throw new (?!UnreachableException\b)' -- 'src/*.cs' >&2; then
  exit 1
fi
