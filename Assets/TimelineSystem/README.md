# TimelineSystem

Unity / VContainer 向けの Timeline Command 実行基盤です。

## 基本フロー

```text
timeline.json
    ↓
TimelineEventRepository.Load()
    ├─ Command ID → TimelineCommand
    └─ JSON Arguments → C# method arguments
    ↓
実行準備済み TimelineEvent[]
    ↓
TimelinePlayer
    ↓
TimelineEvent.Execute()
    ↓
TimelineCommand.Execute()
    ↓
[TimelineCommand] method
```

Command 解決と JSON → C# 引数変換は `Load()` 時に1回だけ行います。
再生時には JSON 変換を行いません。

## 必要な依存

- Unity
- VContainer
- Newtonsoft.Json / Unity Newtonsoft Json package

C# 9 で利用できる構文だけを使用しています。

## Timeline JSON

ルートは Event の配列です。version などのルートメタデータは持ちません。

```json
[
  {
    "time": 0.0,
    "command": "PlaySound",
    "arguments": {
      "request": {
        "soundId": "Warning01",
        "settings": {
          "volume": 0.8,
          "position": "Front"
        }
      }
    }
  }
]
```

同時刻の Event は配列順に実行されます。
JSON は時刻昇順で保存してください。

## Command

```csharp
public sealed class SoundController
{
    [TimelineCommand("PlaySound")]
    public void PlaySound(PlaySoundRequest request)
    {
    }
}
```

Timeline Command には以下の制約があります。

- public instance method
- void
- non-generic
- ref / out なし
- optional parameter なし
- Command ID は空でなく、全体で一意

Command を持つクラスは VContainer に登録してください。

## Repository Key

```csharp
[RepositoryKeySource("Sound")]
public sealed class SoundId : IRepositoryKey
{
    public string Value { get; }

    public SoundId(string value)
    {
        Value = value;
    }
}
```

`IRepositoryKey` 実装型は JSON 上では文字列として扱います。

```json
"soundId": "Warning01"
```

実装型には `public XxxId(string value)` コンストラクタが必要です。

`RepositoryKeySource` は専用 Editor の Dropdown 候補元を示します。

## Flags enum

`[Flags]` enum は JSON 配列として扱います。

```json
"areas": ["Left", "Right"]
```

## Runtime validation policy

Runtime では複雑な Schema 検証を行いません。

行うのは主に以下です。

- Event の Time / Command / Arguments の最低限チェック
- 時刻昇順チェック
- Command ID の存在確認
- メソッド直下の引数の不足・余剰確認
- Json.NET による型変換
- `MissingMemberHandling.Error` を設定した場合の未知のネスト JSON member 検出

ネストした C# property の「不足」を完全には検出しません。
Timeline JSON は専用 Editor から生成することを前提としています。

## Playback

```csharp
player.Reset();
player.Play();

player.Stop();
```

`Reset()` は再生位置を0秒へ戻しますが、再生 / 停止状態は変更しません。

## VContainer

`Examples/ExampleTimelineLifetimeScope.cs` に登録例があります。

`TimelineInitializer : IStartable` が起動時に Timeline をロードし、
`TimelineUpdater : ITickable` が `TimelinePlayer.Update(Time.deltaTime)` を呼びます。

VContainer は Plain C# class を EntryPoint として PlayerLoop へ接続できます。

## Command Definition CSV

Unity Editor メニュー:

`Tools > Timeline > Export Command Definitions`

出力列:

```text
Command,ParameterPath,Type,Source,Options,MultiSelect
```

ネストした引数クラスは leaf property まで
`request.settings.volume` のように展開します。

Runtime の Timeline 実行にはこの CSV を使用しません。
専用 HTML Editor の入力 UI 生成用です。

## 今回まだ含めていないもの

この再設計でまだ責務・仕様を再確定していないため、以下は含めていません。

- SetDiagCommand / DiagCommandId
- Keyboard による Diag Command 一括実行
- Loop / Seek / PlaybackSpeed
- Timeline の Hot Reload
