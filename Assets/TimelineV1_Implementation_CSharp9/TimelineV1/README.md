# Timeline System V1

専用HTMLエディタで作成したTimeline CSVをUnityで読み込み、`[TimelineCommand]` を付けたメソッドを時刻順に実行するVersion 1実装です。

## V1の方針

- Timeline CSVは `Time,Command,Arg1,Arg2,...` のフラット形式を維持する
- JSONは使用しない
- `[TimelineCommand]` の付いたpublic methodを起動時に全Assemblyから自動検出する
- Command IDはAttribute引数。省略時はmethod名を使用する
- Unity Runtimeでは `command_definitions.csv` を参照しない
- `command_definitions.csv` はUnityのmethod signatureからEditor用に生成できる
- enumは通常Dropdown、`[Flags] enum` は複数選択として扱う
- 独自class / structはメンバーを再帰的にフラット化してArg列へ格納する
- `TimelineCommandInvocation` 型のparameterは、別Commandを引数として受けることを意味する
- `SetDiagCommand` という名称そのものは特別扱いしない
- RepositoryのID型は `IRepositoryKey` を実装する

## フォルダ

```text
TimelineV1/
├─ Unity/
│  ├─ Runtime/     Timeline実行基盤
│  ├─ Editor/      command_definitions.csv exporter
│  └─ Examples/    使用例（defineを有効化した場合のみ）
├─ Editor/         改修済み専用HTMLエディタ
└─ Samples/        CSVサンプル
```

## Unity側の主要クラス

| Type | Role |
|---|---|
| `TimelineCommandAttribute` | Timelineから呼び出せるmethodを示す |
| `TimelineOptionSourceAttribute` | 専用エディタのRepository候補Sourceを指定 |
| `IRepositoryKey` | Repository検索キーの共通契約 |
| `TimelineCommandScanner` | 全AssemblyからCommand methodを探索 |
| `TimelineCommandDescriptor` | CommandのMethodInfo/target/schemaを保持 |
| `TimelineCommandRegistry` | Command IDからDescriptorを取得 |
| `TimelineCommandSchema` | Command全体のparameter構造 |
| `TimelineParameterSchema` | parameterまたはnested memberの構造 |
| `TimelineCommandSchemaBuilder` | ReflectionからSchemaを構築 |
| `TimelineScenarioLoader` | Timeline CSVからScenarioを構築・検証 |
| `TimelineCommandArgumentBinder` | Arg列をC# parameterへ再帰的にBinding |
| `TimelineArgumentConverter` | 1セルの文字列をscalar型へ変換 |
| `TimelineObjectFactory` | 独自class/structを生成 |
| `TimelineCommandInvocation` | Command ID + 型変換済み引数を保持 |
| `TimelineCommandInvoker` | `MethodInfo.Invoke()` を実行 |
| `TimelinePlayer` | 時間進行とEvent dispatchを担当 |
| `TimelineCommandDefinitionRow` | Command定義CSVの1行 |
| `TimelineCommandDefinitionCsvExporter` | Command定義CSVを生成 |

## 1. Commandの定義

AttributeにIDを指定しない場合、method名がCommand IDになります。

```csharp
[TimelineCommand]
public void PlaySound(SoundId soundId, float volume)
{
}
```

明示的なIDも指定できます。

```csharp
[TimelineCommand("PlaySound")]
public void Play(SoundId soundId, float volume)
{
}
```

Command methodの制約:

- `public`
- return typeは`void`
- generic method不可
- `ref` / `out`不可
- instance methodの場合、DeclaringTypeをVContainerからconcrete typeでResolve可能にする

## 2. Repository Key

```csharp
[TimelineOptionSource("SoundRepository")]
public readonly struct SoundId : IRepositoryKey
{
    public string Value { get; }

    public SoundId(string value)
    {
        Value = value;
    }
}
```

`IRepositoryKey` 実装型には `public XxxId(string value)` constructorが必要です。

専用エディタでは `TimelineOptionSource` のSource名と同名のRepository CSVを読み込むとDropdownになります。

## 3. Flags enum

```csharp
[Flags]
public enum SoundArea
{
    None  = 0,
    Front = 1 << 0,
    Rear  = 1 << 1
}
```

専用エディタでは複数選択UIになります。CSVでは次のように保存します。

```csv
Front|Rear
```

Runtimeでは `|` をFlags enumとして解釈します。

## 4. 独自class / struct

```csharp
public readonly struct PlaySoundRequest
{
    public SoundId SoundId { get; }
    public float Volume { get; }
    public SoundArea Area { get; }

    public PlaySoundRequest(SoundId soundId, float volume, SoundArea area)
    {
        SoundId = soundId;
        Volume = volume;
        Area = area;
    }
}

[TimelineCommand]
public void PlaySound(PlaySoundRequest request)
{
}
```

C#上はnested objectですが、CSVではleaf memberまでフラット化します。

```csv
Time,Command,Arg1,Arg2,Arg3
5.0,PlaySound,Warning01,0.9,Front|Rear
```

対応:

```text
Arg1 -> request.SoundId
Arg2 -> request.Volume
Arg3 -> request.Area
```

複合型の生成規則:

1. public constructorにparameterがある場合、そのconstructorを使用
2. 複数constructorがある場合はparameter数が最大のものを使用
3. 同数の候補が複数ある場合は曖昧としてエラー
4. parameter付きconstructorがない場合、parameterless construction + public writable property/fieldを使用
5. 循環参照型は非対応として起動時にエラー

constructor方式を推奨します。C# 9互換性を保つため、例では通常の `class` / `struct` を使用します。

## 5. Nested Command

別Commandをparameterとして受ける場合は `TimelineCommandInvocation` を使用します。

```csharp
[TimelineCommand]
public void SetDiagCommand(
    int diagCommandId,
    TimelineCommandInvocation assignedCommand)
{
}
```

CSV:

```csv
Time,Command,Arg1,Arg2,Arg3,Arg4
4.0,SetDiagCommand,101,PlaySound,Warning02,1.0
```

意味:

```text
Arg1 = diagCommandId
Arg2 = nested Command ID = PlaySound
Arg3以降 = PlaySoundの引数
```

Runtimeも専用エディタも `SetDiagCommand` という名前は認識しません。`TimelineCommandInvocation` / `Type=Command` の型情報だけで処理します。

## 6. VContainer登録

Command自体のRegistry登録は自動です。ただしinstance methodを持つControllerは通常どおりVContainerに登録してください。

```csharp
builder.Register<SoundController>(Lifetime.Singleton);
builder.Register<DiagController>(Lifetime.Singleton);

builder.Register<TimelineCommandScanner>(Lifetime.Singleton);
builder.Register<TimelineCommandSchemaBuilder>(Lifetime.Singleton);
builder.Register<TimelineCommandRegistry>(Lifetime.Singleton);
builder.Register<TimelineArgumentConverter>(Lifetime.Singleton);
builder.Register<TimelineObjectFactory>(Lifetime.Singleton);
builder.Register<TimelineCommandArgumentBinder>(Lifetime.Singleton);
builder.Register<ICsvReader, CsvReader>(Lifetime.Singleton);
builder.Register<TimelineScenarioLoader>(Lifetime.Singleton);
builder.Register<TimelineCommandInvoker>(Lifetime.Singleton);
builder.Register<TimelinePlayer>(Lifetime.Singleton);
```

`TimelineCommandRegistry` の生成時に:

```text
AppDomain.CurrentDomain.GetAssemblies()
 -> Type
 -> public method
 -> [TimelineCommand]
 -> VContainer Resolve
 -> Registry
```

の順で自動登録されます。

## 7. Scenarioのロードと再生

```csharp
var csv = File.ReadAllText(path);
var scenario = scenarioLoader.Load(csv);

player.Load(scenario);
player.Play();
```

Unityの`Update()`等から:

```csharp
player.Tick(Time.deltaTime);
```

を呼びます。

フレームがEvent時刻を飛び越えても、到達済みEventを`while`で順次すべて実行します。

## 8. Command定義CSVの生成

Unity Editor menu:

```text
Tools > Timeline > Export Command Definitions CSV
```

生成列:

```csv
Command,ArgIndex,ArgName,MemberPath,Type,Required,Source,Default,Min,Max,Options,MultiSelect
```

例:

```csv
PlaySound,1,request,SoundId,RepositoryKey,true,SoundRepository,,,,,false
PlaySound,2,request,Volume,Float,true,,,,,,false
PlaySound,3,request,Area,Enum,true,,,,,Front|Rear,true
```

`MemberPath` は独自class/structのフラット化位置を表します。

`Min` / `Max` はC# method signatureだけから安全に推論できないため、V1 exporterでは空欄です。必要な制約は生成後CSVで指定できます。

## 9. 専用エディタの変更点

- `MemberPath` 列に対応
- `MultiSelect` 列に対応
- `RepositoryKey` typeに対応（旧`Id`も互換）
- `MultiSelect=true` のEnumをcheckbox式複数選択として表示
- `Type=Command` をgenericなnested Commandとして展開
- `SetDiagCommand` 固有のJavaScript分岐を削除
- nested Command変更時に後続Arg列を再構築
- Scenario CSV形式は従来どおり `Time,Command,Arg1,...`

## 10. command_definitions.csvとRuntimeの責務

```text
C# method signature
        |
        +--> Runtime Schema -> Timeline binding
        |
        +--> command_definitions.csv -> Dedicated Editor UI
```

Runtimeは `command_definitions.csv` を読みません。C# method signatureがRuntime型情報の唯一の正です。

## 注意点

- Command IDは保存済みTimeline CSVとの外部契約です。method名を変更してもCSVを維持したい場合は `[TimelineCommand("StableId")]` を使用してください。
- `IRepositoryKey` の値そのものがRepositoryに存在するかの検証は、専用エディタ側で行います。V1 Runtimeは型変換とCommand構造の妥当性を検証します。
- Managed Code Stripping対策として `TimelineCommandAttribute` はUnityの `PreserveAttribute` を継承しています。
