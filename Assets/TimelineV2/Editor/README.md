# Timeline CSV Editor V2

`index.html` をブラウザで開いて使用します。

## 入力

1. Unity で自動生成した `command_definitions.csv`
2. `Source` 列で参照される Repository CSV
3. 必要に応じて既存の `scenario.csv`

## 対応UI

- `Bool`: true / false
- `Enum`: Dropdown
- `[Flags] enum`: 複数選択チェックボックス
- `RepositoryKey`: Repository CSV の ID Dropdown
- `Command`: 別の TimelineCommand を選択し、その引数を後続 `ArgN` に展開
- 複合クラス: `MemberPath` を表示名に使い、CSV上では `Arg1, Arg2, ...` にフラット化

複合クラスそのものを Command 候補として表示することはありません。Command Dropdown に表示されるのは `Command` 列に存在する `[TimelineCommand]` のIDだけです。

## DiagCommand の例

同じ `DiagCommandId` に複数の `SetDiagCommand` 行を設定できます。
Unity側ではそれらが登録順に保持され、`DiagCommandKeyboardInput` から該当IDを実行すると、割り当てられたCommandがすべて順番に実行されます。
