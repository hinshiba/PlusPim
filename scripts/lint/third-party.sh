#!/bin/sh
# third_party/ の各ディレクトリはLICENSEを持ち，SHA256SUMSに一致する
# 取り込んだテストはライセンスの表示を要し，除外の表が行番号で参照するため
set -eu

status=0
for dir in third_party/*/; do
  if [ ! -f "${dir}LICENSE" ]; then
    echo "${dir}LICENSE:1: error: missing" >&2
    status=1
  fi
  if ! (cd "$dir" && sha256sum -c --quiet SHA256SUMS) >&2; then
    echo "${dir}SHA256SUMS:1: error: does not match" >&2
    status=1
  fi
done
exit $status
