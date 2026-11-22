# 基本的命令の実行の振る舞い

命令は `FromOperands` で生成し，`Src` の宣言順に値を与えて `Eval` を呼ぶ．
レジスタの読み書き，Undo，例外の適用はランタイムの責務であり，命令のテストでは扱わない．

## 検証する事項

- `FromOperands`: 受け付けるオペランドの形と，拒否するもの(`null` を返す)
- `Src` と `Dest`: 宣言するレジスタとその順序
- `Eval` が `dest` へ書き込む値
- `Eval` が返す `Effect`

## 共通

- PC の変更は `Jump`，`Call`，`ExceptionReturn` で表し，PC を `Dest` に宣言しない
- 書いてあること以外は返さず，書き込まないことを原則とする

## 正常系(例外を起こさない系)

- `dest` の値が，命令の定義通りの計算結果と一致する
- `Effect` が命令の定義通りのケースと値になる

## 異常系(例外が生じる系)

- `Raise` または `AddressError` を返す
- `AddressError` の `BadVAddr` は実効アドレスである
- `dest` へ書き込まない

## ランタイムエラー

MIPS で未定義または PlusPim が未対応の操作は，例外ではなくランタイムエラーとする．
発生条件は [ランタイムエラーの振る舞い](runtime_error_model.md) を参照する．

- `Fail` を返す
- `dest` へ書き込まない
