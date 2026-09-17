# Timeline V2

Timeline CSV を Unity で読み込み、`[TimelineCommand]` が付いたメソッドを時刻順に実行するための V2 実装です。

## V2の方針

- JSONは使用しません。
- Timeline CSV は `Time,Command,Arg1,Arg2,...` の単純な形式です。
- `[TimelineCommand]` は起動時にロード済み Assembly 全体から自動探索します。
- Command の対象インスタンスは VContainer から解決します。
- 独自クラスは任意の深さまで再帰的にフラット化・復元できます。
- V2自身のデータモデルとサンプルID/Requestは class で実装しています。
- `enum` と `[Flags] enum` を汎用的に扱います。
- Repository のIDは `IRepositoryKey` で表現します。
- `command_definitions.csv` は C# の `[TimelineCommand]` 定義から自動生成します。
- 同じ DiagCommandId に複数の Command を割り当て、キー入力でまとめて実行できます。

## 主要クラス

### `TimelineCommandRegistry`
Assembly 全体から `[TimelineCommand]` を探索し、Command ID と `MethodInfo` / 対象インスタンスを登録します。独立した Scanner クラスはありません。

### `TimelineTypeUtility`
複合引数の再帰解析、末端型判定、Repository Key / enum / Flags enum の変換規則、Editor用フラット定義生成を一か所にまとめます。

### `TimelineCsvLoader`
`Time,Command,Arg1...` を読み込み、`TimelineScenario` を作ります。型変換は行いません。

### `TimelineCommandInvoker`
実行時に `ArgN` をメソッドの型へ変換します。独自クラスはコンストラクタまたは writable public member を使って再帰的に復元します。

### `TimelinePlayer`
時刻を進め、到達済みの `TimelineEvent` を順番に `TimelineCommandInvoker` へ渡します。

## Command の定義例

```csharp
public sealed class PlaySoundRequest
{
    public SoundId SoundId { get; }
    public SoundOutputSettings Output { get; }

    public PlaySoundRequest(SoundId soundId, SoundOutputSettings output)
    {
        SoundId = soundId;
        Output = output;
    }
}

public sealed class SoundOutputSettings
{
    public float Volume { get; }
    public SoundArea Areas { get; }

    public SoundOutputSettings(float volume, SoundArea areas)
    {
        Volume = volume;
        Areas = areas;
    }
}

[TimelineCommand]
public void PlaySound(PlaySoundRequest request)
{
}
```

CSVでは次のようにフラットになります。

```csv
Time,Command,Arg1,Arg2,Arg3
0.0,PlaySound,Warning01,0.8,Front|Rear
```

対応は以下です。

- `Arg1` -> `request.SoundId`
- `Arg2` -> `request.Output.Volume`
- `Arg3` -> `request.Output.Areas`

ネストの深さは固定していません。循環参照型だけは禁止します。

## 複合クラスの生成ルール

V2ではルールを意図的に少なくしています。

1. public な引数付きコンストラクタが1つだけ存在する場合、その引数順で復元します。
2. 引数付き public constructor がない場合、parameterless constructor で生成し、public writable property / field を設定します。
3. public な引数付きコンストラクタが複数ある型は曖昧なのでエラーにします。
4. 配列・List・Dictionary等のCollectionは対象外です。明示的なclassで表現してください。
5. 循環参照を含む型は対象外です。

この制約により、複雑なSchema/ObjectFactoryクラスを増やさず、復元規則をコードから判断しやすくしています。

## Repository Key

```csharp
[TimelineOptionSource("SoundRepository")]
public sealed class SoundId : IRepositoryKey
{
    public string Value { get; }

    public SoundId(string value)
    {
        Value = value;
    }
}
```

`IRepositoryKey` 実装型には `public XxxId(string value)` が必要です。

## Flags enum

```csharp
[Flags]
public enum SoundArea
{
    None = 0,
    Front = 1 << 0,
    Rear = 1 << 1
}
```

CSVでは `Front|Rear` と保存します。専用エディタでは複数選択UIになります。

## DiagCommand とキーボード実行

V2 には `DiagCommandStore` / `DiagCommandExecutor` / `DiagCommandKeyboardInput` を用意しています。

`SetDiagCommand` は `DiagCommandStore` が提供する TimelineCommand です。
同じ `diagCommandId` に対して複数回 `SetDiagCommand` を実行すると、その ID に Command が実行順で追加されます。

```csv
Time,Command,Arg1,Arg2,Arg3,Arg4,Arg5
0.0,SetDiagCommand,101,PlaySound,Warning02,1.0,Rear
0.0,SetDiagCommand,101,PlayLight,PedestrianAlert,false,
0.0,SetDiagCommand,102,PlaySound,Warning01,0.8,Front|Rear
```

この例では DiagCommandId `101` に `PlaySound` と `PlayLight` の2つが割り当てられます。

シーンに `DiagCommandKeyboardInput` を追加し、Inspector で例えば以下を設定します。

- `Alpha1` -> `101`
- `Alpha2` -> `102`

`Alpha1` を押すと、ID 101 に割り当てられた `PlaySound`、`PlayLight` が登録順にすべて実行されます。

`DiagCommandKeyboardInput` は `Input.GetKeyDown` を使用するため、Unity の Active Input Handling は `Input Manager (Old)` または `Both` を使用してください。

### VContainer登録

```csharp
builder.Register<DiagCommandStore>(Lifetime.Singleton);
builder.Register<DiagCommandExecutor>(Lifetime.Singleton);

// DiagCommandKeyboardInput をシーンに配置した場合
builder.RegisterComponentInHierarchy<DiagCommandKeyboardInput>();
```

`DiagCommandStore` は `TimelineCommandInvoker` に依存しません。これにより、起動時に `TimelineCommandRegistry` が `SetDiagCommand` を自動探索・Resolveしても循環依存が発生しない構成にしています。

## 別Commandを引数にする仕組み

別Commandを引数として受け取る場合は `TimelineCommandCall` を使用します。
`DiagCommandStore.AssignCommand` がこの仕組みの実例です。

```csharp
[TimelineCommand("SetDiagCommand")]
public void AssignCommand(int diagCommandId, TimelineCommandCall assignedCommand)
```

CSVでは `TimelineCommandCall` の位置にCommand IDを書き、その後ろにそのCommandの引数を続けます。

```csv
Time,Command,Arg1,Arg2,Arg3,Arg4,Arg5
0.0,SetDiagCommand,101,PlaySound,Warning02,1.0,Rear
```

- `Arg1` = `diagCommandId`
- `Arg2` = 割り当てるCommand ID (`PlaySound`)
- `Arg3...` = `PlaySound` の引数

専用エディタは `Type=Command` を見て後続引数を自動展開します。

## command_definitions.csv の自動生成

Unity Editor の以下のメニューを実行します。

`Tools > Timeline V2 > Export Command Definitions CSV`

出力例:

```csv
Command,ArgIndex,ArgName,MemberPath,Type,Required,Source,Default,Options,MultiSelect
PlaySound,1,request,SoundId,RepositoryKey,true,SoundRepository,,,false
PlaySound,2,request,Output.Volume,Float,true,,,,false
PlaySound,3,request,Output.Areas,Enum,true,,,Front|Rear,true
```

複合引数型（例: `PlaySoundRequest`）は `Command` 列には出ません。Commandとして出力されるのは `[TimelineCommand]` が付いたメソッドだけです。

## VContainer登録例

```csharp
builder.Register<SoundController>(Lifetime.Singleton);
builder.Register<LightController>(Lifetime.Singleton);
builder.Register<DiagCommandStore>(Lifetime.Singleton);

builder.Register<TimelineCommandRegistry>(Lifetime.Singleton);
builder.Register<ICsvReader, CsvReader>(Lifetime.Singleton);
builder.Register<TimelineCsvLoader>(Lifetime.Singleton);
builder.Register<TimelineCommandInvoker>(Lifetime.Singleton);
builder.Register<DiagCommandExecutor>(Lifetime.Singleton);
builder.Register<TimelinePlayer>(Lifetime.Singleton);

// DiagCommandKeyboardInput をシーンに配置した場合
builder.RegisterComponentInHierarchy<DiagCommandKeyboardInput>();
```

Commandを持つControllerは concrete type としてVContainerからResolveできる必要があります。

## 再生例

```csharp
var scenario = timelineCsvLoader.Load(csvText);
timelinePlayer.Load(scenario);
timelinePlayer.Play();

// MonoBehaviour.Update 等から
 timelinePlayer.Tick(Time.deltaTime);
```

`Tick` がイベント時刻を飛び越えても、その間に到達したイベントを `while` で順番にすべて実行します。

## C#互換性

C# 10 の `record struct` 等は使用していません。サンプルのID・Requestも通常のclassです。

### DiagCommandを再設定する場合

`SetDiagCommand` は同じIDへの割り当てを追加する動作です。同じ設定Timelineを再度実行する場合は、重複登録を避けるため、再設定前に `DiagCommandStore.Clear()` を呼んでください。
