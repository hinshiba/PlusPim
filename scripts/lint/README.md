# scripts/lint

リポジトリの規約の機械的な検査．
PRのCI(`.github/workflows/ci.yml` の Lint)が`scripts/lint/*.sh`をすべて実行します．

## 規約

- 1つのファイルに1つの規約を書く
- POSIX shで書き，`shellcheck -s sh`を通す
- リポジトリのルートで`sh scripts/lint/<name>.sh`として実行する
- 違反があれば`<path>:<line>: ...`の形で標準エラーに出し，0以外で終了する
- 1行目は規約
- 2行目は規約の理由
