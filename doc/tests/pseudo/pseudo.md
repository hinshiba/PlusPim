
## 疑似命令

展開後の命令列を単位として検証する．[../pseudo/pseudo.md](../pseudo/pseudo.md) と [テストパラメータ一覧](values.md) を参照．

- `li $rt imm`: rt = imm (32bit)．1命令/2命令に展開される値と，10進/16進の表記を含める
- `la $rt $label`: rt = ラベルのアドレス．データ/テキストセグメントのラベルと未定義ラベルを含める．未定義ラベルでは展開に失敗する
- `move $rd $rs`: rd = rs
- `nop`: いかなる状態も変化しない．テスト用パラメータなし

**既知バグ注記**: `li` は16進表記を受け付けない(`doc/todo.md`)．


# 共通前提: 疑似命令

[instructions.md](../instructions.md) の `li`/`la`/`move`/`nop` から参照される．疑似命令は1つの `IInstruction` ではなく実命令の列に展開されるため，展開結果の命令列を単位として検証する．

## 検証方法

1. `InstructionRegistry.TryParseAll` で命令列に展開する
2. 展開された命令数が `GetInstructionCount` の返す値と一致することを確認する(ラベルアドレス計算の整合性のため)
3. 命令列を先頭から順に `execute` し，最終状態を確認する
4. 命令列を末尾から逆順に `undo` し，実行前の状態に戻ることを確認する

## テスト用パラメータ

- `li $rt, imm`
  - 値: `0`，`1`，`0xffff`(下位16bitのみ，1命令)，`0x10000`(上位16bitのみ)，`-1`，[values.md](values.md) の代表値・境界値(`INT_MIN`/`INT_MAX` を含む)
  - 表記: 10進，負の10進，`0x` で始まる16進(`0x80000000` 以上を含む)
  - 上位16bitが0の値は1命令，それ以外は2命令に展開される
- `la $rt, label`
  - データセグメントのラベル，テキストセグメントのラベル，下位16bitが `0x8000` 以上になるアドレスのラベル
  - 未定義ラベル
  - 常に2命令に展開される
- `move $rd, $rs`
  - レジスタエイリアス: 2レジスタ命令パターン([values.md](values.md))(`rd==rs`)
  - 値: [values.md](values.md) の代表値・境界値
- `nop`: パラメータなし

## 振る舞いの種類

[execution_model.md](execution_model.md) の正常系を，命令列全体に対して適用する．

- `li`: `rt` が `imm` の32bit値と一致する．`rt` 以外は変化しない
- `la`: `rt` がラベルのアドレスと一致する．未定義ラベルでは展開に失敗する
- `move`: `rd` が `rs` の値と一致する
- `nop`: いかなる状態も変化しない
- 書き込み先が `$zero` のケースは [values.md](values.md) に従う
- 既知バグ: `li` は16進表記を受け付けない(`doc/todo.md`)
