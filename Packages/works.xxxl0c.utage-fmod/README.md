# Utage FMOD 統合プラグイン

Utageノベルゲームエンジン用のFMOD Studio統合拡張機能です。BGM/Ambience/SE/Voiceの再生をFMOD Studio経由に丸ごと差し替えます。

## 機能

- Utageの `SoundManager.System` をFMOD実装に差し替え（Utage本体・シナリオコマンドの改変は不要）
- BGM/Ambience/SEはラベル単位でFMODイベントに対応づけて再生
- Voiceは共通のFMODイベント（Programmer Instrument）にUtage側でロード済みのAudioClipをPCMとして流し込んで再生
- グループ音量・マスター音量・キャラ別音量をFMODのVCAに反映
- セーブ/ロード（Utageのバイナリセーブ機構）に対応
- Unity Editor上でラベル未設定・重複・イベント未割り当てを検出するカスタムインスペクタ付き
- カスタムコマンド `FmodParam` でシナリオからFMODパラメータ（グローバル/イベントローカル、float/Labeled Parameter、補間対応）を操作可能
- WebGLでブラウザの自動再生ポリシーにより止まっている音声出力を、クリック・タッチ・キー入力のいずれでも開始できるようにする

## 前提条件

- Unity 6000.3.25f1 / Utage 4.3.1 / FMOD for Unity 2.03
- asmdef構成
  - Runtime `XXXL0C.UtagePlugins.FMOD` — `Utage` と `FMODUnity` を参照
  - Editor `XXXL0C.UtagePlugins.FMOD.Editor` — `FMODUnity` と上記Runtimeを参照（`includePlatforms: ["Editor"]`）

## セットアップ手順

### 1. FMOD Studio側の準備

1. BGM/Ambience/SE用のイベントを作成する
   - 例: `event:/BGM/Title`、`event:/BGM/Talk`
2. Voice用のイベントを**1つだけ**作成し、**Programmer Instrumentを1つ**配置する
   - 例: `event:/Voice/CommonVoice`
   - 全キャラクターのボイスがこの共通イベント経由で再生されます
3. 音量制御用のVCAを作成する
   - 例: `vca:/Master`、`vca:/BGM`、`vca:/Ambience`、`vca:/SE`、`vca:/Voice`
4. ループさせたいBGMは**FMOD Studio側のループリージョン**で表現する（Utageのイントロ付きループ設定は未対応。詳細は「制限事項」参照）
5. バンクをビルドする

### 2. Unity側でFMODバンクを読み込む

1. `FMOD > Edit Settings` を開き、Studio Project Path / バンクの出力先を**自分の環境のパスに設定し直す**
   - このリポジトリの初期状態では作者環境の絶対パス（`F:/...`）を指しているため、他環境ではそのままでは解決できません
2. Import Type / Bank Load Type はプロジェクトの初期設定（StreamingAssets / All）のままで問題ありません
3. FMODのバンクリストにバンクが取り込まれていることを確認する

### 3. FmodSoundSettings アセットの作成

1. Projectウィンドウで右クリック → `Create > UtageFmod > Fmod Sound Settings`（既定ファイル名 `FmodSoundSettings`）
2. `Resources` フォルダに置く必要はありません。次の手順で直接参照します
3. 各項目を設定する

   | 項目 | 内容 |
   |---|---|
   | ラベル対応 | Utageの**サウンドシートのラベル**とFMODイベントの対応。1行が1ラベル |
   | ボイス再生用の共通イベント | Programmer Instrumentを1つ持つFMODイベント |
   | グループ音量を流し込むVCA | キーはUtageのグループ名（`Bgm` / `Ambience` / `Se` / `Voice`） |
   | マスターVCAパス | 例 `vca:/Master` |
   | キャラ別音量を流し込むVCA | 未設定のタグは再生中インスタンスへ直接音量を反映します（設定は任意） |
   | Unity側のAudioClipロード抑止 | 既定でON。FMOD化対象ラベルについてUnity側のロードを止めます |

4. インスペクタ上部にラベル未設定・ラベル重複・FMODイベント未割り当て・ボイスイベント未設定の警告が出るので、無くなるまで解消する

### 4. シーンへの組み込み

1. Utageの **`SoundManager` コンポーネントが付いたGameObjectに `FmodSoundBootstrap` を追加**する
   - `Add Component > UtageFmod > Fmod Sound Bootstrap`
2. インスペクタで `Sound Manager`（同じGameObjectのSoundManager）と `Settings`（手順3のアセット）を割り当てる。**両方必須**（未割り当てだとConsoleにエラーが出ます）
3. `SoundManager` の `On Create Sound System` イベントにInspectorから何かを登録する必要は**ありません**。`FmodSoundBootstrap` が `Awake` で自動的に購読します

### 5. FmodParamコマンドを使う場合（任意）

シナリオからFMODパラメータを操作したい場合のみ、追加で以下を行います。

1. Utageの **`AdvEngine` コンポーネントが付いたGameObject（または配下の子）に `FmodCustomCommandManager` を追加**する
   - `Add Component > UtageFmod > Fmod Custom Command Manager`
2. シナリオシートに `FmodParam` コマンドを記述する（列構成は下記「FmodParamコマンド」参照）
3. `FmodCustomCommandManager` を追加したシーンを開いた状態で **All Import** を行う（インポート時にもカスタムコマンドの解決が必要なため。開いていないと未知コマンド扱いでインポートエラーになります）

### 6. 動作確認

- 実例として `Assets/L0C_ExtensionsDemo/Scenes/SampleScene.unity` の `SoundManager` GameObjectと `Assets/L0C_ExtensionsDemo/FmodSoundSettings.asset` を参照してください
- シナリオを再生し、BGM/ボイスが鳴ること、Consoleに `[UtageFmod]` の警告が出ないことを確認する

## FmodParamコマンド

シナリオからFMODパラメータ（グローバルパラメータ、またはFMODイベントローカルのパラメータ）を設定するカスタムコマンドです。`FmodCustomCommandManager` を導入していないと使えません（上記セットアップ手順5参照）。

| 列 | 内容 |
|---|---|
| Command | `FmodParam` |
| Arg1 | パラメータ名（必須） |
| Arg2 | 値。数値としてパースできればfloatパラメータ、できなければ Labeled Parameter のラベル文字列として扱う（必須） |
| Arg3 | 対象。`Global`（既定）/ `Bgm` / `Ambience` / `Se` / `Voice` |
| Arg4 | ラベル。Arg3がグループの時のみ有効。省略時はそのグループの再生中インスタンス全部が対象 |
| Arg6 | 補間時間（秒）。既定0＝即時設定 |
| WaitType | 補間完了待ち。既定 `NoWait`（未指定なら待たずに次のコマンドへ進みます） |

```csv
FmodParam,Rain,0.8                      グローバルパラメータ Rain を即時 0.8 に設定
FmodParam,Weather,Rainy                 グローバルの Labeled Parameter Weather に Rainy を設定
FmodParam,LowPass,1,Bgm,,,2.0           再生中BGM全部のローカルパラメータ LowPass を2秒かけて1へ補間
FmodParam,Pitch,1.2,Se,爆発              Seのラベル「爆発」で再生中のインスタンスだけ Pitch を1.2に設定
```

補足:
- 即時設定（Arg6省略/0）はFMOD Studio側のSeek Speedを尊重します。補間中（Arg6>0）は毎フレーム値を送るため強制的に即時反映（ignoreSeekSpeed）になります
- Labeled Parameterは補間に対応していません。Arg6を指定していた場合は警告を出して即時設定にフォールバックします
- 対象グループに該当インスタンスが1本もない場合はエラーではなく警告ログのみです（コマンド自体は失敗しません）
- パラメータはコマンド実行時点で再生中のインスタンスにのみ適用されます。後から再生されたインスタンスには引き継がれません
- グローバルパラメータの値はUtageのセーブデータに保存されません。ロード後に必要なら再設定してください

## 文字送り音をFMOD経由で鳴らす（任意）

`AdvFMODTextSound`（`Runtime/Core/Extra/`）はUtage標準の `AdvTextSound`（文字送り音）をFMOD経由に差し替えるオプション機能です。デモでは使用していません。

- Add Componentから名前検索で追加する（専用メニューはありません）
- 主な項目: `Disable` / `Engine` / `Type`（`Time` または `CharacterCount`）/ `Default Sound` / `Sound Info List`（キャラクターラベル別のSE）/ `Interval Count` / `Interval Time`
- 内部的にはVoiceグループで `SoundPlayMode.Add` を指定して再生します（標準のReplayだと直前のボイスをフェードアウトさせてしまうため）

## WebGLの自動再生ポリシー対策

ブラウザはユーザーが操作するまで音声の再生を止めます。FMOD for UnityのWebGL出力は `window` の `click` / `touchend` を待って音声出力を開始しますが、キー入力では開始しないため、キーボードだけで操作すると音が鳴りません。

`FmodSoundBootstrap` は `Awake` で `Runtime/Plugins/WebGL/UtageFmodAudioUnlock.jslib` の監視を登録し、`keydown` / `mousedown` / `pointerup` / `touchend` / `click` のどれかが来た時点でFMOD本体と同じ開始処理を呼び出します。追加の設定は要りません。

- 監視はキャプチャフェーズで登録しているので、Unityのキャンバス側でイベントの伝播が止められていても動作します
- 開始後もブラウザ側でAudioContextが停止された場合は、次の入力で再開を試みます
- 最初の入力より前は音が出ません。タイトル画面などで最初にクリックやキー入力を求める画面を置いてください
- Unity Editor上と、WebGL以外のプラットフォームでは何もしません

## 動作の仕組み

- `FmodSoundBootstrap` が `SoundManager.OnCreateSoundSystem` を購読し、`FmodSoundSystem` を `SoundManager.System` に代入します。`[DefaultExecutionOrder(-100)]` により、`SoundManager.System` への最初のアクセスより先に差し込まれます
- `FmodSoundSystem` は `SoundManagerSystemInterface` と `SoundManagerSystemInterfaceEx` の両方を実装しています（片方だけだとキャラ別音量が効きません）
- 再生管理はグループ（`FmodSoundGroup`）→インスタンス（`FmodSoundInstance`）の2層構造。フェード進行・インスタンス回収・VCAへの音量反映は `FmodSoundBootstrap.Update` から毎フレーム駆動されます（**コンポーネントを無効化すると止まります**）
- BGM/Ambience/SEはラベルからFMODイベントを解決して `RuntimeManager.CreateInstance` で再生します
- Voiceのみ、Utageがロード済みのAudioClipからPCMデータを取り出し、共通イベントのProgrammer Instrumentに流し込んで再生します（`FmodVoicePcmSoundFactory`）。この都合上、Voiceだけは常にUnity側でのロードが必要で、ロード抑止の対象外です
- `FmodAssetFileInterceptor` が `CustomLoadManager.OnFindAsset` に割り込み、FMOD化済みラベルの実体アセットロードをダミー（`FmodDummyAssetFile`）に置き換えて無駄なロードを防ぎます
- 音量はグループ音量→対応VCA、マスター音量→マスターVCA、キャラ別音量→タグVCA（未設定なら再生中インスタンスへ直接反映）という経路で反映されます
- セーブ/ロードはUtageの保存対象グループ（`saveStreamNameList`）のみバイナリで保存されます
- `FmodCustomCommandManager`（`Utage.AdvCustomCommandManager` 継承）が `AdvCommandParser.OnCreateCustomCommandFromID` を購読し、コマンドID `FmodParam` を `AdvCommandFmodParam` に解決します。パラメータの補間・即時反映は `FmodParameterController` が担い、`FmodSoundSystem.Update` から毎フレーム駆動されます（グローバルは `RuntimeManager.StudioSystem`、グループ対象は `FmodSoundGroup` 経由で再生中の `FmodSoundInstance` に反映）

## トラブルシューティング

### 音が鳴らない

- バンクがビルド・取り込み済みか確認する
- `FMOD > Edit Settings` のパスが自分の環境を指しているか確認する
- `Assets/StreamingAssets` にバンクが配置されているか確認する

### `ラベル "..." に対応する FMOD イベントが登録されていません`

`FmodSoundSettings` のラベル対応に、Utageのサウンドシートと同じ名前のラベルが登録されているか確認してください。

### `ラベル "..." が重複登録されています`

後勝ちで上書きされます。重複しているラベルを解消してください。

### ボイスが鳴らない

- ボイス用イベントにProgrammer Instrumentが1つ配置されているか確認する
- `Voice Event` が設定済みか確認する
- Consoleに `AudioClipが取得できません` / `PCMデータを取得できませんでした` が出ていないか確認する（Utage側のロードが完了する前に再生しようとしている可能性があります）

### VCAの取得に失敗する

`vca:/` から始まるパスがFMOD Studio側のVCA名と一致しているか確認してください。

### `AssetFileManager が見つからない`

シーンにUtageの `AssetFileManager` が存在するか確認してください。

### 標準のUnityサウンドのまま切り替わらない

- `FmodSoundBootstrap` の `Sound Manager` が未割り当て（Consoleにエラーが出ます）
- `FmodSoundBootstrap` がUtageの `SoundManager` と別のGameObjectに付いている

のいずれかが疑われます。

### コンパイルエラー

UtageとFMOD for Unityが正しく導入され、asmdefの参照設定が解決できているか確認してください。

### `FmodParam` がインポート時に構文エラーになる

`FmodCustomCommandManager` を追加した `AdvEngine` を含むシーンを開いていない状態でインポートすると、未知のコマンドIDとして扱われエラーになります。対象シーンを開いた状態でAll Importしてください。

### `FmodParam: SoundManager.System が FmodSoundSystem ではありません`

`FmodSoundBootstrap` が正しくセットアップされていない（手順4参照）か、`FmodSoundBootstrap` より先に何かが `SoundManager.System` にアクセスして既定実装が生成されています。実行順序（`[DefaultExecutionOrder(-100)]`）が保たれているか確認してください。

## 制限事項

- イントロ付きループ（`IntroTime`）は未対応です。設定されている場合は警告ログが出るので、FMOD Studio側のループリージョンで表現してください
- `GetAudioSource` は常に `null` を返します（FMODはUnityの `AudioSource` を経由しないため）。`AudioSource` を前提にした自作処理があると動作しません
- リップシンク用の `GetSamplesVolume` はFMODのDSPメータリングによる代替実装です
- ボイスはUtageがロードしたAudioClipからPCMを抽出する方式のため、ボイス素材は従来どおりUnity側のアセットとして必要です（バンクに音声データを入れる方式ではありません）
- セーブデータのフォーマットはこのプラグイン独自のバイナリ形式で、Utage標準実装のセーブデータとの互換性はありません

## 既知の表示上の注意

- デモのシーン・アセットには旧アセンブリ名由来の `m_EditorClassIdentifier: UtageFmod::UtageFmod.*` という表記が残っていますが、Unityはスクリプトの参照をGUIDで解決するため実害はありません

## ライセンス

この拡張機能はUtageアセットおよびFMOD for Unityの拡張として作成されています。
