# AGENTS.md

このリポジトリで作業するエージェント（Claude Code / Codex）向けのガイド。`CLAUDE.md` はこのファイルを読み込むだけなので、編集はこのファイルに対して行う。

## プロジェクト概要

ノベルゲームエンジン「宴(Utage) 4.2.9」（`Assets/Utage/Scripts/Version.cs`）向けの拡張プラグイン集を作る Unity プロジェクト（Unity 6000.3.21f1 / URP 17.3.0 / Input System 1.20.0 / FMOD for Unity 2.3.14〈`com.fmod.fmod-for-unity-2-03`〉）。

実装は2つ、いずれも `Assets/L0C_UtagePlugins/` 配下（asmdef 名は `XXXL0C.UtagePlugins.*`）:

- `Assets/L0C_UtagePlugins/FMOD/` — Utage の BGM/Ambience/SE/Voice 再生を FMOD Studio 経由に丸ごと差し替える。実装済み
- `Assets/L0C_UtagePlugins/GoogleSheets/` — Google スプレッドシートをシナリオソースとして読み込む Editor 拡張。実装済み

動作サンプルは `Assets/L0C_ExtensionsDemo/`（`Scenes/SampleScene.unity`、`FmodSoundSettings.asset`、`Scenarios/`）。

## ドキュメントの在り処

- **Utage のシナリオリファレンス（構文・コマンド一覧の一次情報）は `Assets/Utage/Docs/ScenarioReference/ja/`（正本）と `.../en/`**。`01_ScenarioBasics` 〜 `09_Localization` の9本 + README。シナリオ（.xls/CSV）の列構成・コマンド引数・設定シート定義・インポートエラーの読み方はここを読む。シナリオファイルを書く/直す前や、未知のコマンド・シート仕様を調べる時は必ず先にここを参照する
- 各プラグインの機能・セットアップ手順・動作の仕組み・制限事項・トラブルシューティングは `Assets/L0C_UtagePlugins/FMOD/README.md` と `Assets/L0C_UtagePlugins/GoogleSheets/README.md` に詳しく書かれている。以下のアーキテクチャ節はこれを前提にした要点のみなので、作業前にそれぞれの README を読むこと

## 編集してよい範囲

- `Assets/Utage/` と `Assets/Plugins/FMOD/`、`Assets/TextMesh Pro/` は購入・配布アセットのベンダーコード。読解対象であり基本的に改変しない。Utage 側の挙動を変えたくなったら、まず拡張ポイント（下記）で解決できないか検討する
- ルート直下の `*.csproj` / `*.slnx` は Unity が自動生成するファイルなので手編集しない
- 自前の実装は `Assets/L0C_UtagePlugins/` と、サンプル/検証用の `Assets/L0C_ExtensionsDemo/`

## アセンブリ構成

- `XXXL0C.UtagePlugins.FMOD`（Runtime）は `Utage` と `FMODUnity` を GUID 参照している
- `XXXL0C.UtagePlugins.FMOD.Editor` は `FMODUnity` と上記 Runtime を参照し `includePlatforms: ["Editor"]`
- `XXXL0C.UtagePlugins.GoogleSheets` は Editor 専用（`includePlatforms: ["Editor"]`）で `Utage` / `UtageEditor` を名前参照
- Utage 純正の拡張例として `Assets/Utage/ExtensionSupport/InputSystem/`（`InputSystemStrategy.cs`）や `RenderPipelines/URP/` があり、「別 asmdef でストラテジを差し込む」という同じ流儀を踏襲している

## アーキテクチャ：Utage のサウンドを FMOD に差し替える仕組み

- `Utage.SoundManager`（`Assets/Utage/Scripts/GameLib/Sound/SoundManager.cs`）が MonoBehaviour のシングルトン。実処理は `SoundManager.System`（`SoundManagerSystemInterface`）に委譲され、初アクセス時に発火する `OnCreateSoundSystem` イベント経由で差し替え可能
- `FmodSoundBootstrap`（Utage の `SoundManager` と同じ GameObject に付ける、`[DefaultExecutionOrder(-100)]`）が `OnCreateSoundSystem` を購読し、`FmodSoundSystem` を `SoundManager.System` に代入する。Utage 本体もシナリオコマンドも無改変で FMOD 経由になる
- `FmodSoundSystem` は `SoundManagerSystemInterface` と `SoundManagerSystemInterfaceEx` の両方を実装している。`SoundManager.SystemEx` は単なるキャストなので、片方だけだとキャラ別音量などが効かない。挙動を合わせたい時は Utage 側の参照実装 `SoundManagerSystem.cs` を読む
- 再生管理は `FmodSoundGroup` → `FmodSoundInstance` の2層構造。フェード進行・インスタンス回収・VCA への音量反映は `FmodSoundBootstrap.Update` から毎フレーム駆動される（コンポーネントを無効化すると止まる）
- Voice だけ例外: Utage がロード済みの `AudioClip` から PCM データを取り出し、共通 FMOD イベント（Programmer Instrument）に流し込んで再生する（`FmodVoicePcmSoundFactory`）。このため Voice だけは常に Unity 側ロードが必要で、ロード抑止の対象外
- `FmodAssetFileInterceptor` が `CustomLoadManager.OnFindAsset` に割り込み、FMOD 化済みラベルの実体アセットロードをダミー（`FmodDummyAssetFile`）に差し替えて無駄なロードを防ぐ
- 設定は ScriptableObject `FmodSoundSettings`（ラベル→FMODイベント対応、グループ/マスター/キャラ別 VCA）。`FmodSoundSettingsEditor` がラベル未設定・重複・イベント未割り当てを検出する
- 既知の制限（`IntroTime` 未対応、`GetAudioSource` は常に null、セーブデータは独自バイナリで Utage 標準と非互換など）は `Assets/L0C_UtagePlugins/FMOD/README.md` の「制限事項」を参照

## アーキテクチャ：Google スプレッドシートのシナリオリーダー

- `AdvScenarioFileReaderGoogleSheets`（`IAdvScenarioFileReader` 実装）は、対象パスが `GoogleSheetsScenarioSource` アセットかどうかで判定する。URL を直接シナリオパスに指定する方式ではない（Utage のインポータがファイルパスをアセット参照からしか集められないため）
- Apps Script に HTTP GET し、1回のリクエストでスプレッドシート内の全シートを JSON でまとめて取得する（Excel リーダーと同じ「1ブック=複数シート」モデル）。`TryReadFile` は Utage の同期 API のため、`UnityWebRequest` ではなく `HttpClient` を `Task.Run(...).GetAwaiter().GetResult()` で同期待ちしている（メインスレッドで `UnityWebRequest` をブロックすると進行しないため。変更時に踏みやすい地雷なので理由ごと維持する）
- シート名が `Sheet Comment Prefix`（既定 `#`）で始まるものは自動除外される
- シート内容の変更は Utage の差分インポートで検知できないため、取り込みは必ず All Import で行う

## ビルド・実行・テスト

- ビルド/コンパイルは Unity Editor 上で行う。CLI ビルドスクリプトはこのリポジトリには存在しない
- Editor 操作・コンパイル確認・テスト実行の方針は `unity-coding-rules` スキルに従う
  - 公式 Unity CLI の pipeline（`com.unity.pipeline`）: 未導入。uloop（`io.github.hatayama.uloopmcp`）は移行期間中の補完用
  - Play での動作確認は `Assets/L0C_ExtensionsDemo/Scenes/SampleScene.unity` を使う。Console は `[UtageFmod]` プレフィックスで絞る
  - テストアセンブリはまだ無い（`com.unity.test-framework` 1.6.0 は導入済み）。追加する場合は各プラグインの `Assets/L0C_UtagePlugins/<Plugin>/Tests/` に置く
- FMOD の Studio Project Path はリポジトリ初期状態で作者環境の絶対パス（`F:/...`）を指しているため、他環境では `FMOD > Edit Settings` で自分の環境に合わせて再設定する必要がある（詳細は FMOD プラグインの README）

## コーディング規約

Unity/C# の規約は `unity-coding-rules` スキルに従う。このファイルには重複して書かない。

- DI: なし（VContainer 未導入。Utage のイベント・拡張ポイント経由で差し込む）
- ログのプレフィックスは既存コードに倣い `[UtageFmod]` に揃える
