# AGENTS.md

このリポジトリで作業するエージェント（Claude Code / Codex）向けのガイド。`CLAUDE.md`はこのファイルを読み込むだけなので、編集はこのファイルに対して行う。

## プロジェクト概要

ノベルゲームエンジン「宴(Utage)」向けの拡張プラグインを、UPMパッケージとして開発・配布するUnityプロジェクト。各バージョンの一次情報は次のファイルにある。

- Unity: `ProjectSettings/ProjectVersion.txt`
- 宴: `Assets/Utage/Scripts/Version.cs`
- FMOD for Unity・URP・Input Systemなどのパッケージ: `Packages/manifest.json`

プラグインは`Packages/`配下の埋め込みパッケージで、1パッケージ1プラグイン。asmdef名は`XXXL0C.UtagePlugins.*`にそろえている。

- `Packages/works.xxxl0c.utage-fmod/`: 宴のBGM/Ambience/SE/Voice再生をFMOD Studio経由に差し替える
- `Packages/works.xxxl0c.utage-gsheets/`: Googleスプレッドシートをシナリオソースとして読み込むEditor拡張
- `Packages/works.xxxl0c.utage-avatarex/`: Avatarのパーツ単位にブレンドモードとクリッピングマスクを追加する

宴はUPMパッケージではないため、`package.json`の`dependencies`に書けない。各パッケージのasmdefは`Assets/Utage/`の`Utage`/`UtageEditor`アセンブリを直接参照しており、導入先には宴を先にインポートしてもらう前提になっている。

## Gitで管理していないもの

`Assets/Utage/`、`Assets/TextMesh Pro/`、`Assets/L0C_ExtensionsDemo/`は`.gitignore`で除外している。宴は購入アセットで再配布できず、デモは宴のアセットを使っているため。これらは作者の手元にだけあり、クローン直後の環境には存在しない。

## ドキュメントの在り処

- 宴のシナリオリファレンス（構文・コマンド一覧の一次情報）は`Assets/Utage/Docs/ScenarioReference/ja/`（正本）と`.../en/`にある。シナリオ（.xls/CSV）の列構成・コマンド引数・設定シート定義・インポートエラーの読み方はここを読む。シナリオファイルを書く/直す前や、未知のコマンド・シート仕様を調べる時は必ず先にここを参照する
- 各プラグインの機能・セットアップ手順・動作の仕組み・制限事項・トラブルシューティングは、各パッケージ直下の`README.md`に書いてある。以下のアーキテクチャ節は要点だけなので、プラグインに手を入れる前に該当パッケージのREADMEを読む
- AvatarExの設計仕様は`Packages/works.xxxl0c.utage-avatarex/Docs/Spec_AvatarEx.md`にある

## 編集してよい範囲

- 自前の実装は`Packages/works.xxxl0c.*`と、サンプル/検証用の`Assets/L0C_ExtensionsDemo/`
- `Assets/Utage/`、`Assets/TextMesh Pro/`、`Assets/Plugins/UnityEditorDarkMode/`は配布アセットなので読むだけにする。宴の挙動を変えたくなったら、宴のイベントや差し替え用のインターフェースで解決する
- FMOD for UnityはUPMパッケージ（`Library/PackageCache/com.fmod.fmod-for-unity.v203@*`）。`Assets/Plugins/FMOD/`にあるのはプロジェクト固有の設定とキャッシュだけ
- ルート直下の`*.csproj`/`*.slnx`はUnityが自動生成するので手で編集しない

## アーキテクチャ：宴のサウンドをFMODに差し替える仕組み

- `Utage.SoundManager`（`Assets/Utage/Scripts/GameLib/Sound/SoundManager.cs`）がMonoBehaviourのシングルトン。実処理は`SoundManager.System`（`SoundManagerSystemInterface`）に委譲され、初アクセス時に発火する`OnCreateSoundSystem`イベント経由で差し替えられる
- `FmodSoundBootstrap`（宴の`SoundManager`と同じGameObjectに付ける、`[DefaultExecutionOrder(-100)]`）が`OnCreateSoundSystem`を購読し、`FmodSoundSystem`を`SoundManager.System`に代入する。宴本体もシナリオコマンドも無改変でFMOD経由になる
- `FmodSoundSystem`は`SoundManagerSystemInterface`と`SoundManagerSystemInterfaceEx`の両方を実装している。`SoundManager.SystemEx`は単なるキャストなので、片方だけだとキャラ別音量などが反映されない。挙動を合わせたい時は宴側の参照実装`SoundManagerSystem.cs`を読む
- 再生管理は`FmodSoundGroup`→`FmodSoundInstance`の2層構造。フェード進行・インスタンス回収・VCAへの音量反映は`FmodSoundBootstrap.Update`から毎フレーム駆動される（コンポーネントを無効化すると止まる）
- Voiceだけ例外で、宴がロード済みの`AudioClip`からPCMデータを取り出し、共通FMODイベント（Programmer Instrument）に流し込んで再生する（`FmodVoicePcmSoundFactory`）。このためVoiceは常にUnity側のロードが必要で、ロード抑止の対象外
- `FmodAssetFileInterceptor`が`CustomLoadManager.OnFindAsset`に割り込み、FMOD化済みラベルの実体アセットのロードをダミー（`FmodDummyAssetFile`）に差し替えて無駄なロードを防ぐ
- 設定はScriptableObjectの`FmodSoundSettings`（ラベル→FMODイベント対応、グループ/マスター/キャラ別VCA）。`FmodSoundSettingsEditor`がラベル未設定・重複・イベント未割り当てを検出する
- FMODのStudio Project Path（`Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`）は作者環境の絶対パス（`F:/...`）を指している。他環境では`FMOD > Edit Settings`で設定し直す

## アーキテクチャ：Googleスプレッドシートのシナリオリーダー

- `AdvScenarioFileReaderGoogleSheets`（`IAdvScenarioFileReader`実装）は、対象パスが`GoogleSheetsScenarioSource`アセットかどうかで判定する。宴のインポータはファイルパスをアセット参照からしか集められないため、URLを直接シナリオパスに指定する方式にはしていない
- Apps ScriptにHTTP GETし、1回のリクエストでスプレッドシート内の全シートをJSONでまとめて取得する（Excelリーダーと同じ「1ブック=複数シート」モデル）
- `TryReadFile`は宴の同期APIのため、`HttpClient`を`Task.Run(...).GetAwaiter().GetResult()`で同期待ちしている。メインスレッドで`UnityWebRequest`をブロックすると処理が進まなくなるので、この構成は理由ごと維持する
- シート名が`Sheet Comment Prefix`（既定`#`）で始まるシートは自動で除外される
- シート内容の変更は宴の差分インポートで検知できないため、取り込みは必ずAll Importで行う

## アーキテクチャ：AvatarEx

- `AvatarExCustomCommandManager`が`OnBootInit()`で`AdvGraphicInfo.CallbackCreateCustom`に登録し、`FileType=AvatarEx`を`AdvGraphicObjectAvatarEx`に解決する。PlayMode開始のたびにコールバックがリセットされるので、毎回`-=`してから`+=`で登録し直す
- 宴の`AvatarImage`は改変しない。`AvatarExPostProcessor`が`AvatarImage.OnPostRefresh`を購読し、再生成された各パーツの`Image`にマテリアルを後から差し込む
- 目パチ・口パク中は`OnPostRefresh`が約10Hzで発火する。`AvatarExPostProcessor`の処理は、タグの逆引き辞書の再利用と、状態が変わらないマテリアル更新のスキップを前提に組んでいるので、毎回の走査や割り当てを足さない
- シェーダー`UtageExtension/AvatarEx/BlendableUI`は`Shader.Find`で解決するため、`Runtime/Resources/AvatarEx-Shader/`から移動しない
- `AvatarExData`のインスペクタボタンは、宴の`AvatarData`のprivateメソッドをリフレクションで呼び出して動かしている。宴を更新したら、`AvatarExData.cs`の`InvokeBaseEditorMethod`の呼び出し先が残っているか確認する

## リリース

- `release`ブランチにpushすると、`.github/workflows/release-utage-*.yml`がパッケージごとにタグとGitHub Releaseを作る。共通処理は`.github/workflows/release-package.yml`にある
- タグ名は`<接頭辞>v<package.jsonのversion>`（例`utage-fmod-v0.1.0`）。同じタグがあればReleaseは作られないので、リリース前に該当パッケージの`package.json`の`version`を上げる
- Releaseの説明文に載せる前提パッケージは、各呼び出し側ワークフローの`prerequisites`に書いている。宴やFMODの対応バージョンを変えたら、ここも合わせて直す
- パッケージを追加したら、`release-utage-fmod.yml`を複製して呼び出し側ワークフローを1つ足す

## ビルド・実行・テスト

- ビルド/コンパイルはUnity Editor上で行う。CLIのビルドスクリプトはない
- Editor操作・コンパイル確認・テスト実行は`unity-coding-rules`スキルの方針に従う。公式Unity CLIのpipeline（`com.unity.pipeline`）は導入済みで、uloop（`io.github.hatayama.uloopmcp`）はpipelineで足りない操作の補完に使う
- Playでの動作確認は`Assets/L0C_ExtensionsDemo/Scenes/SampleScene.unity`を使う。Consoleはパッケージごとのプレフィックス（`[UtageFmod]`、`[UtageAvatarEx]`）で絞る
- テストアセンブリはまだない（`com.unity.test-framework`は導入済み）。追加する場合は各パッケージの`Tests/`に置く

## コーディング規約

Unity/C#の規約は`unity-coding-rules`スキルに従う。このファイルには重複して書かない。

- DIは使わない（VContainer未導入）。宴のイベントや差し替え用のインターフェース経由で処理を差し込む
- ログにはパッケージごとのプレフィックス（`[UtageFmod]`、`[UtageAvatarEx]`）を付ける
