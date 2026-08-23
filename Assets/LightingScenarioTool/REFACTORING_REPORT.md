# Lighting Scenario Tool リファクタリングレポート

## 対象

アップロードされた `LightingScenarioTool` 一式を静的レビューし、既存の操作仕様とJSON形式を極力維持したまま、バグ修正・防御的実装・責務整理・不要な再生成の削減を行いました。

主な変更ファイル:

- `Runtime/Core/ScenarioDocument.cs`
- `Runtime/Core/ScenarioDataUtility.cs`（新規）
- `Runtime/Model/ScenarioData.cs`
- `Runtime/Persistence/JsonScenarioRepository.cs`
- `Runtime/UI/LightingScenarioApp.cs`
- `Runtime/UI/PreviewPanel.cs`
- `Runtime/UI/TimelinePanel.cs`
- `Runtime/UI/AppTheme.cs`

## 修正したバグ・問題

### 1. Selection Inspector が未選択時にも表示される

**問題**

`LightingScenarioApp.RefreshInspector()` が常に `_selectionInspectorContent.SetActive(true)` を実行しており、READMEの「Color Keyframe選択中のみ表示」という仕様と不一致でした。

**修正**

有効なColor Keyframeが1件以上選択されている場合だけInspector内容を表示するよう変更しました。

---

### 2. Ctrl複数選択時の `SelectedUnitId` が不整合になる

**問題**

`SelectColorKeyframe()` は、複数トラックにまたがるCtrl選択でも最後にクリックした `unitId` を `SelectedUnitId` に保持していました。一方、別の選択APIでは複数トラック選択時に `SelectedUnitId = null` としており、選択経路によって状態が変わっていました。

この不整合により、貼り付け先トラックやトラック選択表示が「最後に触ったトラック」に暗黙依存する可能性がありました。

**修正**

`UpdateSelectedUnitFromKeyframeSelection()` に選択状態の導出処理を集約しました。

- 選択Keyframeが1トラック内のみ: その `unitId`
- 複数トラックにまたがる: `null`
- Keyframe選択なし: Keyframe選択からはUnitを決定しない

---

### 3. `unitId` を受け取るKeyframe編集APIが `unitId` を検証していない

**問題**

以下のAPIは `unitId` を引数に持つにもかかわらず、実際には `keyframeId` だけで対象を検索していました。

- `TrySetColorKeyframeTime(...)`
- `TrySetColorKeyframeTimeNoHistory(...)`
- `TrySetColorKeyframeColor(...)`
- `DeleteColorKeyframe(...)`

呼び出し側が誤った `unitId` を渡しても、別トラックのKeyframeを編集できる状態でした。

**修正**

指定された `unitId` のトラックに対象Keyframeが実在することを検証してから編集するよう変更しました。

なお、`SnapColorKeyframeTime(rawTime, unitId, ...)` は従来から全トラック横断のスナップとして動作しており、トラック間で時刻を揃える用途があるため、その挙動は互換性のため維持しています。

---

### 4. Undo / Redo で再生ヘッドまで過去の位置に戻る

**問題**

`currentTime` はDirty判定上は「プロジェクト編集ではない一時状態」として扱われている一方、Undo/Redo用JSONスナップショットには含まれていました。

そのため、例えば5秒地点で編集し、その後8秒地点へ移動してUndoすると、編集だけでなく再生ヘッドも5秒へ戻る可能性がありました。

**修正**

Undo/Redo前の `currentTime` を退避し、ドキュメント復元後に現在位置を再適用するよう変更しました。

---

### 5. 例外発生時に `ScenarioDocument.Execute()` が半端な状態を残す可能性

**問題**

従来の `Execute()` は、Mutation途中で例外が発生すると、それまでの変更が `Data` に残る可能性がありました。

**修正**

Mutation前のJSON状態を保持し、例外時には必ず復元してから例外を再送出するトランザクション的な実装へ変更しました。

---

### 6. 不正・旧JSONによってNullReferenceやID衝突が発生し得る

**問題**

従来のNormalize処理では以下が残る可能性がありました。

- `lightingUnits` 内の `null`
- 重複した `unitId`
- 複数トラック間で重複した `keyframeId`
- `NaN` / `Infinity` 相当の不正値
- 範囲外のPreview座標・色値

特に重複IDは、DictionaryベースのUI管理や選択処理を曖昧にします。また `CreateNextUnitId()` など一部処理はnull Unitに対して例外を起こし得ました。

**修正**

新規 `ScenarioDataUtility` に正規化を集約し、以下を保証するようにしました。

- Unitのnull除去
- Unit IDの一意性
- Keyframe IDの全トラック横断での一意性
- null Track / Keyframe Listの補完
- 時刻・Preview座標・色値・Zoom・Preview Light Sizeの範囲補正
- 不正な有限値の補正
- Keyframe時刻衝突時の決定的な整理
- フォーマット既定値の一元管理

---

### 7. 将来バージョンのJSONを古いツールで開いて上書きできる

**問題**

`JsonUtility` は未知フィールドを無視するため、例えば将来の `4.x` 形式を `3.0.0` のツールで開いた場合、未知情報を失った状態で保存し直す危険があります。

**修正**

`JsonScenarioRepository.Load()` で `dataFormatVersion` を確認し、現在対応している `3.0.0` より新しい形式は明示的に拒否するよう変更しました。古い形式・バージョン未記載データは従来どおり正規化対象とします。

---

### 8. JSON保存中の失敗で既存ファイルを破損する可能性

**問題**

従来は `File.WriteAllText()` で保存先を直接上書きしていました。書き込み途中のI/O失敗等では、既存ファイルが途中までの内容になる可能性があります。

**修正**

同一ディレクトリの一時ファイルへ書き出した後、既存ファイルがある場合は `File.Replace()`、新規の場合は `File.Move()` で確定する方式へ変更しました。

---

### 9. PreviewがDocument変更のたびに全Light ViewをDestroy/Instantiateする

**問題**

Scenario名、Snap、Lock、Keyframe色などPreview Unit構造と無関係な変更でも、Previewの全Light GameObjectを破棄・再生成していました。

これは編集頻度が上がるほどGC allocationとUnity Object生成コストにつながります。

**修正**

`unitId` をキーに既存 `PreviewLightView` を再利用するreconcile方式へ変更しました。

- 削除されたUnitだけDestroy
- 新規UnitだけInstantiate
- 既存UnitはViewを再利用
- 順序、色、選択、名前表示だけ更新

---

### 10. UIイベントを `RemoveAllListeners()` で消していた

**問題**

複数箇所で `RemoveAllListeners()` が使われていました。これはInspectorで設定されたpersistent UnityEventを消すAPIではありませんが、他コンポーネントが実行時に追加したnon-persistent listenerを巻き込む可能性があります。

また、EditorでUIを生成する処理とRuntimeでイベントをBindingする処理が混在していました。

**修正**

- UI生成時は主に見た目とHierarchyだけを作成
- Runtime初期化時にイベントをBinding
- 必要な箇所では特定listenerだけ `RemoveListener()` → `AddListener()`
- 動的に新規生成したTimeline Rowでは不要な `RemoveAllListeners()` を廃止

としました。

---

### 11. Undo履歴が無制限にJSON全文を保持する

**問題**

Undo/Redoは各編集時点のScenario JSON全文を保持する方式で、履歴数に上限がありませんでした。長時間編集・大型Scenarioではメモリ使用量が増え続けます。

**修正**

既存のスナップショット方式は互換性と変更リスクを考慮して維持しつつ、Undo/Redo履歴を最大100件に制限しました。

---

### 12. Timeline再構築時、Destroy待ちの旧UIが同一フレームに残る

**問題**

`Object.Destroy()` はフレーム末まで実体が残るため、全再構築時に旧Rowと新Rowが一時的に同居し得ます。

**修正**

破棄対象を先に `SetActive(false)` にしてから `Destroy()` するよう変更し、同一フレームの描画・Layoutへの影響を抑えました。

---

### 13. AppThemeのactive参照が無効化済みThemeを保持し得る

**問題**

`AppTheme` はstaticなactive instanceを持ちますが、Disable時に参照を解除していませんでした。

**修正**

`OnDisable()` で自分がactiveなら解除し、再解決時は有効なThemeを優先して検索するよう変更しました。

---

### 14. 既定値・制約値の重複

**問題**

`3.0.0`、Duration、Pixels Per Second、Preview Light Size、時刻比較epsilonなどの値が複数ファイルに散在していました。

**修正**

`ScenarioDataUtility` に定数を集約し、Model / Document / UI / Previewから参照する形へ変更しました。

---

### 15. 古い `README.md.tmp` が残っている

**問題**

`README.md.tmp` は現在廃止済みの `Multi` や旧Inspector仕様を含む古い文書で、現行READMEと矛盾していました。

**修正**

リファクタリング版から削除しました。

## 意図的に残した課題

### `LightingScenarioApp` / `TimelinePanel` の責務集中

両クラスはいずれも1,000行を大きく超え、UI生成、入力、選択、編集コマンド、ファイル操作、表示更新など複数責務を持っています。

今回は動作互換性を優先し、一度にPresenter/Controller/Serviceへ全面分解することはしていません。次段階では例えば以下への分割が適切です。

- `ScenarioSelectionController`
- `ScenarioClipboardService`
- `ScenarioPlaybackController`
- `ScenarioCommandController`
- `TimelineView` / `TimelineInteractionController`
- `ProjectFileService`

### Timelineの全再構築

`Document.Changed` のたびにTimelineは依然として全Row/Keyframeを再生成します。Preview側はreconcile化しましたが、Timelineは構造が複雑で回帰リスクが高いため今回は限定修正としました。

次段階では変更種別を通知するイベント（Structure / KeyframeValue / Selection / ProjectState等）を導入し、差分更新またはObject Poolへ移行するのが適切です。

### Undo/RedoのJSONスナップショット方式

履歴を100件に制限して無制限増加は防止しましたが、1操作ごとにJSON全文を保持する点は残っています。Scenarioが大規模化する場合はCommand Patternまたは差分履歴へ置き換える余地があります。

### `DummyBinaryScenarioExporter`

現在のBinary Exportは名前どおりダミー実装で、Unit数など限定情報しか出力しません。本番照明ライブラリが要求するBinary仕様が確定した段階で別実装へ差し替える必要があります。

### Preview背景画像の絶対パス

現仕様どおりパスをそのままJSONへ保存しています。別PCへProject JSONを移動した場合の可搬性はありません。必要ならProject相対パスまたはAssetコピー方式に変更してください。

### `AppTheme` のstaticアクセス

Disable時の不正参照は改善しましたが、Theme自体はstatic globalです。同一Sceneに複数のLightingScenarioAppを同時に置く構成まで完全に分離するなら、Themeを各ViewへDIする設計が適切です。

## 検証

この実行環境にはUnity Editor / C# compiler (`dotnet`, `csc`, `mcs`) がないため、Unityでの実コンパイルおよびPlay Modeテストは実施できていません。

代わりに以下を実施しました。

- 全C#ファイルの括弧・波括弧・角括弧の静的バランスチェック
- 変更箇所のAPI利用関係確認
- `RemoveAllListeners()` がRuntime配下から残っていないことを確認
- 新規C#ファイルの `.meta` を追加
- staleな `README.md.tmp` を除去
- originalとのdiffを生成

Unityへ導入後は、最低限以下の回帰確認を推奨します。

1. New / Open / Save / Save As
2. Undo / Redo時にPlayhead位置が変わらないこと
3. 1トラック・複数トラックのCtrl Keyframe選択
4. Copy / Paste / Duplicate / Delete
5. Keyframe dragとSnap
6. Track Lock / Mute
7. Preview Unit drag / Unit Name表示 / Light Size
8. Preview背景画像読み込み
9. Color Picker / Color Preset
10. Fullscreen / Keyboard shortcut
11. 既存の3.0.0 JSON読み込み
12. 壊れたIDを含むJSONの読み込み
13. 3.0.0より新しいformat versionのJSONが拒否されること

