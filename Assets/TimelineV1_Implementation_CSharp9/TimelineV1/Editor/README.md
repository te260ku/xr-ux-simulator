# Scenario Editor - Timeline V1

Timeline V1対応のブラウザ専用Scenario CSVエディタです。Node.js等は不要です。

## 起動

1. `index.html` をChrome/Edgeで開く
2. `Command定義を読込` から `samples/command_definitions.csv` を選ぶ
3. Repositoriesで対応するRepository CSVを読み込む
4. `Scenario CSVを読込` または `＋ 追加`
5. `Scenario CSVを保存`

## Command Definition CSV

```csv
Command,ArgIndex,ArgName,MemberPath,Type,Required,Source,Default,Min,Max,Options,MultiSelect
```

### Type

- `String`
- `Float`
- `Int`
- `Bool`
- `Enum`
- `RepositoryKey` (`Id`も互換)
- `Command`

### MemberPath

独自class/structをフラット化したmember pathです。

```csv
PlaySound,1,request,SoundId,RepositoryKey,...
PlaySound,2,request,Volume,Float,...
PlaySound,3,request,Area,Enum,...
```

UIでは `request.SoundId` のように表示します。

### MultiSelect

`Enum` かつ `MultiSelect=true` の場合、複数選択UIになります。
CSV値は `Front|Rear` のように `|` で保存します。

### Command

`Type=Command` の引数ではCommand一覧を選択できます。選択したCommandの引数がその直後のArg列へ動的に展開されます。

例:

```csv
SetDiagCommand,1,DiagCommandId,,Int,true,,,,,,false
SetDiagCommand,2,AssignedCommand,,Command,true,,,,,,false
```

Scenario:

```csv
4.0,SetDiagCommand,101,PlaySound,Warning02,1.0
```

`SetDiagCommand` という名前はエディタ内で特別扱いしていません。

## Scenario CSV

```csv
Time,Command,Arg1,Arg2,Arg3,...
```

JSONは使用しません。独自class/structもleaf値までArg列へ展開します。
