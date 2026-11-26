#!/bin/sh
# scripts/ のスクリプトは自身の位置からの相対深さではなく`git rev-parse --show-toplevel`でルートへ移動する
# 相対深さでは，スクリプトを移動したときに移動先がずれるため
set -eu

if git grep --untracked -nF -- 'dirname "$0")/..' -- 'scripts/*.sh' ':!scripts/lint/' >&2; then
  exit 1
fi
