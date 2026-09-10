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
