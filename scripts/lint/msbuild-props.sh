#!/bin/sh
# MSBuildのプロパティは`.csproj`, `.props`, `.targets`のみ
# 散在するとクライアント間で異なるビルドになる可能性があるため
set -eu

if git grep --untracked -nE -- '(^|[ "'\''`])(-p:|/p:|-property:|--self-contained|--sc( |$))' \
  -- ':!*.csproj' ':!*.props' ':!*.targets' ':!scripts/lint/'; then
  echo "error: set MSBuild properties in src/PlusPim/PlusPim.csproj or Directory.Build.props, not on the command line" >&2
  exit 1
fi
