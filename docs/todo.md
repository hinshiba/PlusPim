# PlusPim Todo

## バグ修正等

### 実行系

#### MIPS準拠



#### 履歴



#### ランタイム系


### 前処理系


### VS Code 拡張

- [ ] ログ出力を `console.log` から `LogOutputChannel` (`createOutputChannel(name, { log: true })`) に移す (`extension.ts`)
- [ ] `resolveBinary` の同期 I/O (`fs.accessSync`, `fs.chmodSync`) を `fs.promises` にそろえる (`binaryLayout.ts`)

### ドキュメンテーション


## 新機能

### 実行系

### 前処理系

## コード品質

### テストと CI

- [ ] テストの追加
  - [ ] 結合テスト
    - [ ] テスト用アセンブリディレクトリの自動実行


## 低優先度

- [ ] Emacs, Vim, Neovim, Zed への拡張機能提供
