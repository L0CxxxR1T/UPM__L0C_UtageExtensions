# Utage AvatarEx 拡張プラグイン

Utageノベルゲームエンジン用のAvatar機能拡張です。パーツ（Category＝タグ）単位で **ブレンドモード**
（Normal / Multiply / Additive / Screen）と **クリッピングマスク**（他パーツの現在表示中スプライトの
アルファ値で切り抜き、最大3ソースのOR合成・反転対応）を追加します。

## 機能

- 新規FileType `AvatarEx` を追加。既存の `FileType=Avatar` / `AvatarData` には一切影響しません
- パーツ単位のブレンドモード: `Normal`（既定）/ `Multiply` / `Additive` / `Screen`
- パーツ単位のクリッピングマスク: 他のパーツ（タグ）の現在表示中スプライトのアルファを流用（専用マスク
  画像は不要）。最大3ソースまで指定でき、OR（最大値）合成・反転（`InvertMask`）に対応
- マスクソースが表示パターンを切り替えれば、マスク対象の切り抜き形状も自動で追従する
- `EnableRenderTexture`（RenderTextureへの焼き込み）と併用可能。パーツ合成はRenderTextureに焼く前
  （Canvas内）で完結する
- Utage本体（`Assets/Utage`以下）は無改造。`AvatarImage` へはコンポジション（`OnPostRefresh` 購読）
  のみで介入する

## 前提条件

- Unity 6000.3.21f1 / Utage 4.2.9
- asmdef構成
  - Runtime `XXXL0C.UtagePlugins.AvatarEx` — `Utage` を参照
  - Editor `XXXL0C.UtagePlugins.AvatarEx.Editor` — `Utage` / `UtageEditor` / 上記Runtimeを参照
    （`includePlatforms: ["Editor"]`）
- シェーダー `UtageExtension/AvatarEx/BlendableUI` は `Runtime/Resources/AvatarEx-Shader/` に配置
  （`Shader.Find` で解決するため、フォルダを移動しないこと）

## セットアップ手順

### 1. パーツ画像の準備

1. キャラクター1体につき、タグ（body / eye / hair 等）ごとにフォルダを分けてパーツ画像を用意する
   （通常のAvatar機能と同じ構成）
2. **重要**: 画像は Utage プロジェクト設定の `CustomImportSpriteFolders`（既定で
   `Texture/Character` 等が登録されている）配下に置かないこと。このフォルダ配下のテクスチャは
   `Utage.ResourceImporter.OnPreprocessTexture` が再インポートのたびに強制的に
   `TextureImporterType.Default` へ戻してしまい、`Sprite` として使えなくなる（既存の `AvatarData`
   の `Apply` ボタンによるSprite化も同様の影響を受ける）。`Assets/<プロジェクト>/Textures/<キャラ名>/`
   のような、上記フォルダ一覧に含まれない場所に置くこと
3. `TextureImporter` を `Sprite (2D and UI)` に設定する（`Texture Type: Sprite`）

### 2. AvatarExData アセットの作成

1. Projectウィンドウで右クリック → `Create > UtageExtension > AvatarExData`
2. Character シートの `FileName` 解決規則に従い、`Resources/<RootResourceDir>/Texture/Character/`
   配下に保存する（`RootResourceDir` は `AdvEngineStarter` の設定値。既存の `FileType=Avatar` の
   `AvatarData` アセットと同じ置き場でよい）
3. インスペクタの `Data List` にタグ名とフォルダ（手順1で用意したもの）を登録し、`Apply` を押す
   （通常の `AvatarData` と同じ操作）
4. Apply後、インスペクタ下部に **AvatarEx 拡張設定** セクションが自動生成される（タグ一覧との同期は
   インスペクタ描画のたびに自動で行われる）。各タグについて `Blend Mode` / `Clip Mask Sources`
   （最大3件、ドロップダウンで選択）/ `Invert Mask` を設定する
   - `Clip Mask Sources` のドロップダウンには、マスクソースになれる条件を満たすタグのみが列挙される
     （通常タグのみ・多段不可。「制限事項」参照）

### 3. AdvEngineへの組み込み

1. Utageの **`AdvEngine` コンポーネントが付いたGameObject（または配下の子）に
   `AvatarExCustomCommandManager` を追加**する
   - `Add Component > UtageAvatarEx > Avatar Ex Custom Command Manager`
   - 既存の `FmodCustomCommandManager` 等と同じヒエラルキーに置いて問題ない
2. `AvatarExCustomCommandManager` を追加したシーンを開いた状態で **All Import** を行う
   （FMODプラグインの `FmodParam` コマンドと同様、`FileType=AvatarEx` の解決に必要）

### 4. シナリオシートへの記述

Character シートに、通常の `FileType=Avatar` と同じ列構成で行を追加し、`FileType` に `AvatarEx` を
指定する。

```csv
CharacterName,NameText,Pattern,FileName,FileType,body,eye,brow,hair,cheek
みお,みお,通常,MioAvatarEx.asset,AvatarEx,normal,open,normal,front,blush
```

- `FileName` にはAvatarExDataアセット名を拡張子付きで指定する（`.asset`。拡張子を省略すると
  デフォルト拡張子 `.png` が付与されてしまうため必須）
- タグ列（`body` / `eye` 等）は通常のAvatar機能と同じく、Characterシートの列名としてタグ名をそのまま
  使う。ブレンドモード・マスクの設定はシナリオ側では行わず、AvatarExDataアセット側で固定する
- `RenderTexture` / `RenderRect` 列を指定すればRenderTexture併用も可能（既存のAvatarと同じ）

### 5. 動作確認

- 実例として `Assets/L0C_ExtensionsDemo/Scenarios/L0C_ExtensionsDemo.xls` の Character シート
  （`AvatarExDemo` / `AvatarExDemoRT` 行）と
  `Assets/L0C_ExtensionsDemo/Resources/L0C_ExtensionsDemo/Texture/Character/AvatarExDemo.asset`
  を参照してください
- シナリオを再生し、Consoleに `[UtageAvatarEx]` のエラー・警告が出ないことを確認する

## 動作の仕組み

- `AvatarExCustomCommandManager`（`Utage.AdvCustomCommandManager` 継承）が `OnBootInit()` で
  `AdvGraphicInfo.CallbackCreateCustom` に登録し、`FileType=="AvatarEx"` を
  `AdvGraphicObjectAvatarEx` に解決する。PlayMode開始のたびにコールバックがリセットされるため、
  `OnBootInit()` のたびに `-=;+=` で登録し直す（多重登録を避けつつ再登録する）
- `AdvGraphicObjectAvatarEx`（`AdvGraphicObjectAvatar` 継承）は `AddGraphicComponentOnInit()` で
  基底の初期化（`AvatarImage` 等の生成）の後に `AvatarExPostProcessor` を追加するだけで、Utage本体の
  `AvatarImage` には一切手を加えない
- `AvatarExPostProcessor` は `AvatarImage.OnPostRefresh`（パーツ再生成のたびに発火する公開イベント）
  を購読し、生成済みの各パーツ `Image` にタグ別設定（ブレンドモード・マスク）に応じたマテリアルを
  後から差し込む。**目パチ・口パク中はこの再生成が約10Hzで発生する**ため、以下の対策を行っている
  - タグ逆引き（`Sprite → Tag`）は `AvatarExDataCache` が `AvatarExData` ごとに1回だけ構築した
    辞書を引くのみで、毎回の線形走査はしない
  - `Normal` かつマスクなしのパーツは `Image.material` に一切触れず、Utageの既定マテリアル
    （`null` またはRenderTexture用の `Utage/RenderTexture`）をそのまま保持する
  - マスクを使わないパーツは `AvatarExBlendMode` × 描画先（通常Canvas / RenderTexture空間内）ごとの
    共有マテリアルを使い回す。マスクを使うパーツのみ `(タグ, スロット番号)` 単位で専用マテリアルを
    キャッシュし、前回と状態が変わっていなければ `SetTexture` 等を一切呼ばずスキップする
- シェーダー `UtageExtension/AvatarEx/BlendableUI` は1本のみで、`_ColorOp`（ブレンドモードに応じた
  rgb前処理）と `_SrcBlendRGB`/`_DstBlendRGB`/`_SrcBlendA`/`_DstBlendA`（RGB/Alpha別のBlendステート）
  をC#側から `Material.SetFloat` で切り替える。Alpha側のブレンド式は、RenderTexture空間内では
  Utage本体の `Utage/RenderTexture` シェーダーと同一の式に合わせている（アルファチャンネルの
  反転エンコード規約に追従するため）
- クリッピングマスクは専用画像を使わず、マスクソースタグの「現在表示中スプライト」のアルファを
  そのままシェーダーでサンプリングする。パーツごとにアトラス内位置・実サイズが異なりうるため、
  `AvatarExUvTransform` が両者の実サイズ比から中心そろえのアフィン変換を計算してUVをリマップする

## トラブルシューティング

### キャラクターが表示されない / `FileType :AvatarEx is not support` 系のログが出る

`AvatarExCustomCommandManager` が `AdvEngine` 配下に追加されているか、そのシーンを開いた状態で
All Importを行ったか確認してください（セットアップ手順3）。

### インスペクタでSpriteに設定してもすぐ元に戻ってしまう

パーツ画像が `CustomImportSpriteFolders`（`Texture/Character` 等）配下に置かれています。
セットアップ手順1を参照し、別の場所に配置してください。

### `[UtageAvatarEx] タグ "..." のマスク設定が無効です`

`ClipMaskSourceTags` に指定したタグが以下のいずれかに該当しています。ログのメッセージに従って
設定を修正してください。マスクは自動的に無効化（マスクなし表示）されるため、シナリオの進行は
止まりません。

- 指定したタグが存在しない
- 指定したタグが `OptionTag`（アクセサリ等）である
- 指定したタグ自身がさらに `ClipMaskSourceTags` を持っている（多段参照）

### `AvatarExData` のインスペクタで「Apply」「ReimportResources」ボタンが反応しない

Utage の `AvatarData` は、これらのボタンや `Data List` 下のプレビュー設定を `[Button]` /
`[UpdateFunction]` / `[NovelAvatarPattern]` 属性経由でリフレクションにより呼び出している。呼び出し先
（`ReimportResources` / `OnApply` / `UpdateFunction` / `GetThis`）はいずれも `AvatarData` 側の
**private** メソッドで、`Type.GetMethod` は実行時型（`AvatarExData`）から検索するため、基底クラスの
private メンバは見つからずボタンが無言で無反応になる（`Assert.IsTrue(true, ...)` の第1引数が常に
`true` のため警告すら出ない）。`AvatarExData.cs` 側に同名のメソッドを用意し、リフレクションで
`AvatarData` の実装へ委譲することで解決済み。Utage 側でこれらのメソッド名が変わるバージョンアップが
あった場合は、この委譲が壊れて再びボタンが無反応になる（その場合は `[UtageAvatarEx]` エラーログが
出るので `AvatarExData.cs` の `InvokeBaseEditorMethod` 呼び出し部分を新しいメソッド名に追従させる）。

### マスクが効いているのに見た目がズレる

パーツ画像がトリム設定・回転パック設定でインポートされていないか確認してください。
`AvatarExData` インスペクタの「パーツスプライトの検証」セクションに警告が出ます。

### Multiply/Screenの見た目がPhotoshop等でのプレビューと違う

このプロジェクトはLinearカラースペースのため、ハードウェアブレンドによる合成結果は sRGB空間での
プレビュー（Photoshop等）と一致しません（Multiplyはより暗く/コントラスト強めに、Screenはより
明るく出る傾向があります）。実機・PlayModeでの見た目を基準に調整してください。

## 制限事項

- ブレンドモード・マスクはCategory（タグ）単位の設定で、シナリオ側から実行時に動的変更することは
  できません。変更したい場合は `AvatarExData` 側の設定を変更する必要があります
- マスク**ソース**に指定できるのは通常タグのみです（`OptionTag` はマスクソースにできません。マスク
  **対象**側は `OptionTag` を許容します）
- マスク参照は1段までです。マスクソース自身をマスク対象にする多段構成はできません
- マスクソースは最大3件までです。複数指定時はOR（max）合成になります
- エディタのインスペクタプレビューはブレンド・マスクを反映しません（素の重ね合わせのみ）。
  見た目確認はPlayMode実行時に行ってください
- uGUIの `Mask`（Stencilクリッピング）配下でAvatarExのパーツを使う構成は非サポートです。
  `StencilMaterial.Add` がベースマテリアルをコピーするため、コピー後の `SetTexture` 等の更新が
  反映されない可能性があります。クリッピングが必要な場合は `RectMask2D`
  （`CanvasRenderer` によるクリップ、マテリアル非改変）を使ってください
- 本プロジェクトはLinearカラースペースのため、Multiply/Screenの見た目はsRGB空間でのプレビューと
  一致しません（「トラブルシューティング」参照）

## ライセンス

この拡張機能はUtageアセットの拡張として作成されています。
