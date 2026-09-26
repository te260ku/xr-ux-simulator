# Timeline System Final

Unity Runtime、Unity Editor用Command Definition Exporter、専用HTMLエディタをまとめた最終版です。

## 依存ライブラリ

- Unity 6.x
- VContainer
- Newtonsoft.Json / Json.NET

## Timeline JSON

Timeline Eventは `time + execution` です。

```json
[
  {
    "time": 0.0,
    "execution": {
      "command": "PlaySound",
      "arguments": {
        "soundId": {
          "value": "Warning01"
        },
        "volume": 0.8
      }
    }
  }
]
```

すべての `TimelineCommandExecution` は同じ構造です。

```json
{
  "command": "CommandId",
  "arguments": {}
}
```

`SetDiagCommand` のようにCommand引数が `TimelineCommandExecution` の場合も、
同じExecution構造を引数内に持つだけです。

## JSON変換方針

独自 `JsonConverter` は使用しません。

- 通常の値・class・値オブジェクト: Json.NET標準
- enum / Flags enum: `StringEnumConverter`
- `TimelineCommandExecution`: `TimelineRepository.CreateExecution()` を再帰的に使用

値オブジェクトはJSON上でもobjectとして保存します。

```json
{
  "soundId": {
    "value": "Warning01"
  }
}
```

## Timeline Command追加

```csharp
[TimelineCommand("PlaySound")]
public void PlaySound(
    SoundId soundId,
    float volume)
{
    ...
}
```

Command methodの制約:

- public
- instance
- void
- non-generic
- ref/outなし
- optional parameterなし
- Command IDは空文字不可・重複不可

また、`[TimelineCommand]` を持つクラスの具象型をVContainerからResolveできる必要があります。

Command methodのparameter名はTimeline JSONの外部契約です。
既存Timelineとの互換性を維持する場合は不用意にrenameしないでください。

### Parameter description

専用エディタへ表示する引数説明は
`TimelineParameterDescriptionAttribute` で指定します。

```csharp
[TimelineCommand("PlaySound")]
public void PlaySound(
    [TimelineParameterDescription("再生する音声ID。")]
    SoundId soundId,
    [TimelineParameterDescription("再生音量。0.0〜1.0。")]
    float volume)
{
    ...
}
```

Exporterは説明文をCommand Definition JSONの
`description` として出力します。

```json
{
  "name": "volume",
  "type": "float",
  "description": "再生音量。0.0〜1.0。"
}
```

属性は command parameter だけでなく、
その引数型の `field` / `property` にも付与できます。

```csharp
[TimelineCommand("PlaySound")]
public void PlaySound(
    [TimelineParameterDescription("再生する音声ID。")]
    SoundId soundId,
    [TimelineParameterDescription("元のクラス")]
    Req req,
    [TimelineParameterDescription("再生音量。0.0〜1.0。")]
    float volume)
{
    ...
}

public sealed class Req
{
    [TimelineParameterDescription("a1の説明")]
    public float a1;

    [TimelineParameterDescription("a2の説明")]
    public int a2;

    [TimelineParameterDescription("a3の説明")]
    public UiState a3;
}
```

この場合、Command Definition JSONでは `req` と
`req.a1` / `req.a2` / `req.a3` のそれぞれに
`description` を持てます。

専用エディタでは実際に入力する末端項目だけを表示するため、
`req.a1` には `a1の説明`、`req.a2` には `a2の説明`、
`req.a3` には `a3の説明` が表示されます。

説明の優先順位は次の通りです。

1. field / property 自身の `TimelineParameterDescription`
2. 直近の親 object の説明
3. command parameter の説明
4. 説明なし

したがって、子メンバーに属性がない場合は
`[TimelineParameterDescription("元のクラス")]` が
フォールバックとして利用されます。

`field` / `property` は Json.NET の ContractResolver で
実際にシリアライズ対象となるメンバーだけが対象です。

### Numeric min / max

`int` / `float` の parameter・field・property には
`TimelineParameterRangeAttribute` で入力範囲を指定できます。

```csharp
[TimelineCommand("PlaySound")]
public void PlaySound(
    [TimelineParameterDescription("再生音量。0.0〜1.0。")]
    [TimelineParameterRange(0.0, 1.0)]
    float volume)
{
    ...
}

public sealed class Req
{
    [TimelineParameterRange(0, 100)]
    public int a2;
}
```

Command Definition JSONには `min` / `max` が出力されます。

```json
{
  "name": "volume",
  "type": "float",
  "min": 0.0,
  "max": 1.0
}
```

専用エディタでは数値入力のHTML `min` / `max` に反映し、
Validationでも範囲外をエラーにします。

`TimelineParameterRange` を `int` / `float` 以外へ付与した場合は、
Command Definition出力時にエラーになります。

## Repository Dropdown

Repository ID型には以下のAttributeを付けます。

```csharp
[RepositoryKeySource("Sound")]
public sealed class SoundId
{
    public string Value { get; }

    [JsonConstructor]
    public SoundId(string value)
    {
        Value = value;
    }
}
```

専用エディタでは `source` を見てRepository CSVのIDをDropdown表示します。

`source` を持つobjectは、現仕様では **JSON上のstring propertyを1個だけ**
持つことを要求します。property名は `value` 固定ではありません。

## Command Definition生成

Unity Editor:

`Tools -> Timeline -> Export Command Definitions`

で `command-definitions.json` を出力します。

Command DefinitionはHTML Editor専用です。
Unity RuntimeはCommand Definition JSONを読みません。

サポートするDefinition type:

- `string`
- `int`
- `float`
- `bool`
- `enum`
- `object`
- `execution`

`TimelineCommandExecution` は現在、Commandの直接引数としてのみ対応します。
これにより専用JsonConverterを不要にしています。

## 専用HTML Editor

`DedicatedEditor/timeline_editor.html`

をブラウザで開いて使用します。

1. Unityで生成した `command-definitions.json` を読み込む
2. Repositoriesタブで必要なCSVを読み込む
3. Timeline JSONを読み込む、または新規作成する
4. 編集後にTimeline JSONを保存する

`type: "execution"` は通常のCommand Definitionを再利用して再帰表示します。
`SetDiagCommand` 専用の分岐はありません。

Repository IDをrenameするとTimeline内の参照も更新します。
使用中IDを削除する場合は使用箇所数を表示し、削除後は該当参照を空文字にします。
Execution内部の参照も同じ再帰処理で検索します。

## Diag

`SetDiagCommand` は加算型です。

例:

- 1秒: `SetDiagCommand(10, A)`
- 5秒: `SetDiagCommand(10, B)`

5秒以降のDiag ID 10は A -> B の順で両方実行します。

`TimelinePlayer.Reset()` は再生時間と次Event indexだけをResetします。
`DiagCommandRegistry.Clear()` はDiag割当だけをClearします。

完全な再実行を行う場合は、アプリケーション側Coordinatorなどで必要なReset/Clearを
組み合わせてください。

## VContainer

登録例:

`Unity/Samples/TimelineCompositionRootExample.cs`

Command Ownerが `TimelineCommandRegistry` に依存すると、
Registry構築時のResolveで循環依存になり得るため避けてください。

## Hot Reload

この実装は、再生開始前にTimelineをLoadし、再生中にTimelineを差し替えない前提です。

`TimelineRepository.Load()` は新しいTimelineを最後まで生成できた後にだけ
`Current` を差し替えるため、Load途中の失敗では現在のTimelineを維持します。

## サンプル

`SampleData/` に以下を同梱しています。

- `command-definitions.example.json`
- `timeline.example.json`
- Sound / Light / Image / UiTarget Repository CSV
