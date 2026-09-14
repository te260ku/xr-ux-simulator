# Scenario Editor (HTML)

ブラウザだけで動作する、Command駆動のScenario CSV専用エディタです。
サーバー、Node.js、Python、npmは不要です。

## 起動

1. `index.html` をChrome/Edgeで開く
2. `Command定義を読込` から `samples/command_definitions.csv` を選ぶ
3. Inspector下部のRepositoriesから各Repository CSVを読み込む
4. `Scenario CSVを読込` から既存シナリオを読み込む、または `＋ 追加` で新規作成
5. `Scenario CSVを保存` で `scenario.csv` を出力

## Command定義CSV

必須列:

- `Command`
- `ArgIndex`
- `ArgName`
- `Type`

任意列:

- `Required` : `true` / `false`
- `Source` : Repository名。指定するとドロップダウンになる
- `Default`
- `Min`
- `Max`
- `Options` : Enum用。`Left|Center|Right` のように `|` 区切り

Type:

- `String`
- `Float`
- `Int`
- `Bool`
- `Enum`
- `Id`

例:

```csv
Command,ArgIndex,ArgName,Type,Required,Source,Default,Min,Max,Options
PlaySound,1,SoundId,Id,true,SoundRepository,,,,
PlaySound,2,Volume,Float,false,,1.0,0,1,
ShowImage,2,Position,Enum,true,,,,,Left|Center|Right
```

## Repository CSV

`Id` 列を推奨します。`Id` / `ID` / `id` がなければ先頭列をIDとして扱います。

例:

```csv
Id,Path
Warning01,Sounds/warning01.wav
Warning02,Sounds/warning02.wav
```

`Command定義CSV` の `Source=SoundRepository` としている場合、
InspectorのRepositories欄で `SoundRepository` にこのCSVを読み込んでください。

## Scenario CSV

保存形式:

```csv
Time,Command,Arg1,Arg2,Arg3
0.0,PlaySound,Warning01,0.8,
1.5,PlayLight,PedestrianAlert,true,
3.0,ShowImage,WarningImage,Left,2.0
```

HTML上では `Arg1` / `Arg2` ではなく `ArgName` が表示されます。

## Validation

保存前に以下を検証します。

- Timeが0以上の数値
- CommandがCommand定義に存在
- Required引数
- Float / Int / Boolの型
- Min / Max
- EnumのOptions
- Source指定されたIDがRepositoryに存在

エラーが1件でもある場合、Scenario CSVは保存しません。

## 設計方針

このHTMLは「編集・入力支援・事前検証」のみ担当します。
Unity側でもScenario CSV読み込み時に同等の検証を実施してください。

## Arguments列の表示

Scenario一覧のArguments列は、`SoundId=Warning02, Volume=1.0` のような平文ではなく、
引数ごとのKey-Value表示です。

```text
SoundId   Warning02
Volume    1.0
```

引数名は控えめに、値を強調して表示します。空の引数は一覧では表示しません。
表示専用の変換ルールは持たず、Scenario CSVに保存される値をそのまま表示します。


## Timeによる並べ替え

Scenario上部の次のボタンでTime列を数値として並べ替えられます。

- `Time ↑`: 昇順
- `Time ↓`: 降順

同じTimeの行は元の順序を維持します。
数値として解釈できないTimeは、昇順・降順のどちらでも末尾に配置されます。
選択中の行は並べ替え後も選択状態を維持します。


## Arguments列の罫線表示

Arguments列は、各引数を1セル内のミニテーブルとして表示します。

```text
┌────────────┬──────────────────┐
│ SoundId    │ Warning02        │
├────────────┼──────────────────┤
│ Volume     │ 1.0              │
└────────────┴──────────────────┘
```

引数名側には薄い背景色を付け、引数名と値、および各引数行の境界に罫線を表示します。


## Time + Command の複合並び替え

並び替えは `Time` を主キー、`Command` を副キーとして扱います。

- `Time ↑` : Time昇順
- `Time ↓` : Time降順
- `Command ↑` : 現在のTime順を維持したまま、同じTimeの行をCommand名昇順
- `Command ↓` : 現在のTime順を維持したまま、同じTimeの行をCommand名降順

例:

```text
Time  Command
1.0   PlayLight
1.0   PlaySound
2.0   PlayLight
2.0   ShowImage
```

Timeの並び順はCommandソート後も維持されます。
TimeとCommandの両方が同じ場合は元の行順を維持します。


## Repository ID の削除

Repositoryを読み込むと、`Repositories` の `ID一覧を管理` からIDを削除できます。

使用中のIDも削除可能です。

削除ボタンを押すと、確認ダイアログにそのIDを使用しているScenario行数を表示します。

例:

```text
"Warning01" は Scenario の 3 行で使用されています。

削除すると、そのIDが使われているすべての箇所を自動的に空欄にします。

削除しますか？
```

削除確定後は以下を同時に実行します。

1. RepositoryからIDを削除
2. Scenario内でそのRepository IDを参照していたすべての引数を空欄に変更
3. 必須引数の場合はScenario行をValidation Errorとして表示

Repositoryを変更した後は、対象Repositoryの `保存` ボタンから更新済みCSVを保存してください。

参照行数は、同じ行で同一IDを複数箇所から参照していても1行として数えます。


## Repository ID の追加・リネーム・Path変更

Repository管理画面では、削除に加えて以下の操作ができます。

### ID追加

`＋ Add ID` から新しいIDを追加できます。

- IDは必須
- 既存IDとの重複は禁止
- IDは英字で始まり、英数字・`_`・`-` を使用可能
- Repositoryに `Path` 列がある場合はPathも必須

### IDリネーム

`Rename` からIDを変更できます。

リネーム対象IDがScenarioで使用されている場合は、確認ダイアログに使用行数を表示します。
リネーム確定後は、Scenario内の該当参照を新しいIDへ自動更新します。

### Path変更

`Path` ボタンからRepositoryのPathだけを変更できます。

Path変更はScenario内のID参照には影響しません。

### 保存

Repositoryを変更した後は、そのRepositoryの `保存` ボタンから更新済みCSVを保存してください。


## SetDiagCommand

`SetDiagCommand` は第二引数に応じて第三引数以降の入力項目が変化する特殊Commandです。

Scenario CSV上の形式:

```csv
Time,Command,Arg1,Arg2,Arg3,Arg4,...
4.0,SetDiagCommand,101,PlaySound,Warning02,1.0,...
```

意味:

- `Arg1`: `DiagCommandId`。整数必須
- `Arg2`: `AssignedCommand`。Command一覧から選択
- `Arg3`以降: `AssignedCommand` が本来必要とする引数

例:

```text
SetDiagCommand
  DiagCommandId    101
  AssignedCommand  PlaySound
  SoundId          Warning02
  Volume           1.0
```

`AssignedCommand` のドロップダウンはCommand定義から自動生成されます。
再帰的な定義を避けるため、`SetDiagCommand` 自身は選択肢から除外しています。

`AssignedCommand` を変更すると、以前のCommand用に入力されていたArg3以降はクリアされ、
新しいCommandのDefault値が設定されます。

Validation、Repository IDのリネーム・削除による参照更新も、SetDiagCommandのArg3以降を対象にします。


## Scenario / Repository タブ

画面上部に `Scenario` と `Repository` の2タブを追加しています。

### Scenario

従来どおり、左側にScenario一覧、右側に選択行のInspectorを表示します。

### Repository

Repository編集専用の画面です。
画面幅全体を使用して以下を表示します。

- Repository名
- 読み込んだCSV
- ID
- Path
- 使用行数
- Rename
- Path変更
- Delete
- Add ID
- Repository CSV保存

Repository編集をScenario Inspectorから分離したため、IDやPathが長い場合でも確認・編集しやすくなっています。


## パネルヘッダー統合タブ

`Scenario` と各Repositoryのタブは、Scenarioの追加・複製ボタンの直上、
従来 `Scenario` と表示していたパネルヘッダー位置に統合されています。

```text
[ Scenario ] [ Sound ] [ Light ] [ Image ]
──────────────────────────────────────────
＋追加  複製  削除  Time↑ ...
```

Repositoryタブは `command_definitions.csv` の `Source` から動的生成されます。
表示名は末尾の `Repository` を省略します。

- `SoundRepository` → `Sound`
- `LightRepository` → `Light`
- `ImageRepository` → `Image`

Scenarioタブでは従来のScenario一覧とInspectorを表示します。
Repositoryタブでは選択中Repositoryだけを画面幅いっぱいに表示し、
`ID / Path / Used / Actions` を表形式で編集できます。


## Scenarioの直接編集

ScenarioタブではInspectorを使用せず、表を直接編集します。

```text
Time | Command | Arguments | Status
```

- `Time`: 数値入力
- `Command`: Dropdown
- `Arguments`: Command定義に基づいて入力UIを自動生成
- `Status`: 行単位のValidation結果

### Arguments

Argumentsは縦積みではなく横一列で表示します。

```text
SoundId [Warning01 ▼]  Volume [0.8]
```

引数が多い場合はArgumentsセル内だけ横スクロールします。
そのため、引数数が増えてもScenario行の高さは基本的に増えません。

`SetDiagCommand`では、

```text
DiagCommandId [101]
AssignedCommand [PlaySound ▼]
SoundId [Warning02 ▼]
Volume [1.0]
```

のように横並びで表示されます。

`AssignedCommand`を変更すると、Arg3以降は選択したCommand用の入力欄へ即座に切り替わります。

Validation Errorがある入力欄は赤枠になり、Status列には `Error (n)` を表示します。


## 固定幅Argumentスロット

Scenario一覧のArgumentsは、Commandや型に関係なく1引数あたり同じ幅で表示します。

```text
SoundId             Volume
[Warning01      ▼]  [0.8             ]

ScenarioId          Loop
[PedestrianAlert▼]  [true            ▼]
```

仕様:

- 1 Argument = 180px
- Argument間 = 10px
- ラベルは入力欄の上に表示
- String / ID / Float / Int / Bool / Enum / Command で幅を変えない
- 引数が多い場合はArgumentsセル内だけ横スクロール
- 長いラベルや値はレイアウトを広げず、ツールチップで全文を確認可能

これにより、Commandごとの引数型や名称の違いによる横位置のばらつきを抑えます。


## Excel型Scenario表

ScenarioはExcelに近い表形式で直接編集します。

```text
# | Time | Command | Arg1 | Arg2 | Arg3 | ... | Status
```

各Argumentは独立した列です。

セル内は、

```text
SoundId
Warning01
```

のように、上段に引数名、下段に入力値を表示します。

重要な点として、**Time / Command / Arg1 / Arg2 / ... の入力欄はすべて同じ入力段に配置されるため、1行内で一直線に揃います。**

通常時はInput / Selectの独立した角丸枠を表示せず、表そのものの罫線だけでセルを区切ります。
フォーカスしたセルだけ青いフォーカス枠を表示します。

- Time列: 92px
- Command列: 180px
- 各Argument列: 170px
- Status列: 92px
- Argument数が多い場合は表全体を横スクロール
- 空のArgument列は空セルとして表示
- `SetDiagCommand` のArg3以降も選択Commandに応じて同じ列へ動的展開


## 視覚階層の改善

Excel型の表構造は維持したまま、一覧性を高めるため次の視覚調整を追加しています。

- ヘッダー背景を本文より濃くし、下罫線を強調
- 偶数行にごく薄いゼブラ背景
- 縦罫線より行境界をやや強く表示
- Argument名をセル内の薄いラベル帯として表示
- Command列を薄い背景＋太字で強調
- 選択行は薄い背景と左端アクセントで表示
- Validation Errorセルは赤い左アクセント＋薄赤背景
- Statusは `✓ OK` / `! Error n` のコンパクトなバッジ表示

入力欄自体は通常時Borderlessのままで、フォーカス時のみ青枠を表示します。


## Calm Grid UI

高密度Scenario表の視認性を優先し、装飾を最小限にしています。

- 背景は基本的に白
- ヘッダーのみ薄いグレー
- 罫線は薄い1pxで統一
- ゼブラ背景を使用しない
- Command列専用背景を使用しない
- Argument名は小さな補助ラベルのみ
- 必須項目の `*` は一覧では表示しない
- 選択行は左端のアクセント線だけで表示
- エラーはセル塗りつぶしではなく赤い下線で表示
- Statusは `OK` / `Error n` のプレーンテキスト

通常時はInput / Selectの独立枠を表示せず、フォーカス時のみ青枠を表示します。
