# AvatarEx 設計仕様書

Utage の Avatar 機能（`FileType=Avatar` / `AvatarData`）を拡張し、パーツ（Category＝タグ）単位の
**ブレンドモード** と **クリッピングマスク** を追加する。本ドキュメントは実装前の設計仕様書であり、
実装後の使い方・トラブルシューティングは別途 README として作成する。

## 1. 目的・スコープ

- ブレンドモード: パーツ画像の合成方法を `Normal`（既定）/ `Multiply`（乗算）/ `Additive`（加算）/
  `Screen`（スクリーン）から選べるようにする。将来的に種類を追加する前提で設計する。
- クリッピングマスク: あるパーツ（タグ）の表示範囲を、別のパーツ（タグ、**最大3つまで複数指定可**）の
  現在表示中スプライトのアルファ値で切り抜く。反転（マスク外を表示）にも対応する。
- 適用単位は **Category（タグ）単位**。アバター全体に一括で効果をかけたい場合は本拡張の対象外とし、
  Utage 既存の RenderTexture 経由の仕組み（`EnableRenderTexture` / `AdvEffectColor` 等）を使う。
- Utage 本体（`Assets/Utage` 以下）は一切改変しない。既存の `FileType=Avatar` / `AvatarData` にも
  影響を与えない。

## 2. 全体アーキテクチャ

```
Character シート: FileType=AvatarEx, FileName=xxx.asset（AvatarExData）
        │
        ▼
AdvGraphicInfo.CallbackCreateCustom  ← 静的コールバックに登録（Utage本体は非改変）
        │  FileType=="AvatarEx" のとき typeof(AdvGraphicObjectAvatarEx) を返す
        ▼
AdvGraphicObjectAvatarEx : AdvGraphicObjectAvatar
        │  AddGraphicComponentOnInit() を override して基底の初期化に追加コンポーネントを足す
        ▼
GameObject
  ├─ AvatarImage            … Utage 本体、無改造のまま使用
  └─ AvatarExPostProcessor  … 新規。AvatarImage.OnPostRefresh を購読し後処理でマテリアルを差し込む
```

- `AvatarImage`（`Assets/Utage/Scripts/GameLib/UI/Image/Avatar/AvatarImage.cs`）は無改造。
  各パーツ生成後に発火する公開イベント `OnPostRefresh`（`UnityEvent`）を購読することで、
  Utage 本体の private ロジック（`MakeImageFromAvartorData` 等）に一切触れずに拡張する。
- `AvatarExPostProcessor` は `OnPostRefresh` 発火後、`AvatarImage` の子（`RootChildren` 配下）に
  生成された各パーツの `Image` を走査し、対応する `PartExtraSetting`（後述）に基づきマテリアルを
  適用する。
- **タグの逆引きは `Image.sprite` の参照同一性で行う**（`Image.sprite.name` などの文字列一致では
  ない）。`AvatarData.categories` の各 `Category.Sprites`（public）を `Contains()` で走査し、
  一致した `Category.Tag` を採用する。名前ベースの逆引きは、別カテゴリに同名スプライトが存在した
  場合に誤判定するため使わない。この対応表は `AvatarExData` ごとに一度だけ構築してキャッシュし
  （`OnPostRefresh` は口パク中に約10Hzで発火するため、毎回の線形走査は避ける）、`Sprite → Tag` の
  `Dictionary` として保持する。
- 同一タグが複数の `Category`（フォルダ）に紐づく構成（通常は起きないが Utage の仕組み上禁止されて
  いない）の場合、`categories` の並び順で最初に見つかった `Category` を採用し、2件以上該当したら
  初回のみ警告ログを出す。

## 3. データ構造: `AvatarExData`

`AvatarData` を継承した新規 ScriptableObject。

```csharp
// Runtime
public class AvatarExData : AvatarData
{
    [Serializable]
    public class PartExtraSetting
    {
        public string Tag;                          // AvatarData.categories の Tag と対応
        public AvatarExBlendMode BlendMode;          // 既定 Normal
        public List<string> ClipMaskSourceTags;      // 空リスト = マスクなし。最大3件
        public bool InvertMask;                      // 既定 false。合成後の結果に対して一括反転
    }

    public List<PartExtraSetting> PartExtraSettings;
}

public enum AvatarExBlendMode
{
    Normal,   // Blend SrcAlpha OneMinusSrcAlpha（既定・変更なし）
    Multiply, // Blend DstColor Zero
    Additive, // Blend One One
    Screen,   // Blend OneMinusDstColor One
}
```

- `PartExtraSettings` は `AvatarData.categories`（フォルダ＝タグ単位）に対して **タグ名文字列で紐づく**
  並行リスト。`AvatarData.Category` クラス自体は拡張しない（`UpdateFunction` 等の再構築ロジックが
  private でオーバーライドできないため）。
- タグ一覧との整合性は実行時には検証しない。**エディタでのみ** 同期・検証する（4章）。

### 3.1 ClipMaskSourceTags の制約

- **最大3件まで**指定可能（シェーダー側のマスクスロット数に合わせる。5.1節）。
- 指定できるのは `AvatarData.categories` 内の **通常タグのみ**（`OptionTag` と一致するタグ、
  すなわちアクセサリ等の同時複数表示カテゴリは指定不可）。
- 参照は **1段のみ**。マスクソースに指定したタグ自身が `ClipMaskSourceTags` を持つ（＝マスクを
  使っている）ことは禁止。これにより循環参照は構造的に発生しない。
- 検証すべき条件は、リスト内の **各タグそれぞれ** に対して次の3つ（すべてエディタ・実行時の
  両方でチェックする）:
  1. `categories` に実在するタグか
  2. 実在するなら `OptionTag` と一致していないか
  3. 参照先タグの `PartExtraSetting.ClipMaskSourceTags` が**空である**（＝多段になっていない）か
- 1つでも条件NGのタグがあれば、7章の方針に従い **そのタグだけでなく設定全体をマスクなしに
  フォールバック** する（部分的に有効なタグだけで合成する、という中途半端な状態にはしない）。
- **マスク適用対象（クリップされる側）には `OptionTag` を許容する**（制約があるのはマスク
  *ソース* 側のみ）。アクセサリ等の同時複数表示パーツにもマスクをかけられる。ただしこの場合、
  同一タグで複数の `Image` が同時に存在しうるため、マテリアルは `(タグ, スロット番号)` 単位で
  個別にキャッシュする（5.2節）。

### 3.2 複数マスクソースの合成方法

- 各 `ClipMaskSourceTags` のアルファ値は **OR（最大値・max）** で合成する。
  `finalAlpha = max(source0.a, source1.a, source2.a)`
- マスクソースタグが現在 `None`（非表示）パターンの場合、そのソースは **alpha=0 として合成に
  参加**する（＝実質無視される）。全ソースが `None` の場合は `finalAlpha = 0` となり、結果的に
  マスク対象パーツは完全非表示になる（単一ソース時のルールと矛盾しない）。
- `InvertMask` は **上記合成の最終結果に対して一括で** `1 - finalAlpha` を適用する
  （ソースごとの個別反転は持たない）。

## 4. エディタ拡張

### 4.1 `AvatarExData` 専用インスペクタ

`[CustomEditor(typeof(AvatarExData))]` を新規に定義する（`AvatarData.NovelAvatarDataInspector` は
サブクラスに継承されないため独立して定義可能）。

- `base.OnInspectorGUI()` で既存 `AvatarData` の UI（Apply / ReimportResources ボタン等）をそのまま
  描画する。**`base.OnInspectorGUI()` は `AvatarData.UpdateFunction`（`[UpdateFunction]` 属性の
  ドロワー）経由で Apply/Reimport 実行時に `GUIUtility.ExitGUI()`（`ExitGUIException` 送出）を
  使う。このメソッド呼び出しを `try/catch` で囲んで例外を握り潰してはいけない**（Apply/Reimport
  自体が壊れる）。同期処理は例外を伝播させた次の再描画で走る前提にする。
- `OnInspectorGUI` は毎フレーム呼ばれるため、`categories` の現在のタグ一覧と `PartExtraSettings`
  の突き合わせは**差分がある場合のみ**書き戻す（無条件に書き戻すと `Undo` 履歴と dirty フラグが
  毎フレーム荒れる）。差分検出時は `Undo.RecordObject` を呼んでから
  - 存在しなくなったタグの設定は自動削除
  - 新規タグには既定値（`Normal` / マスクなし）の設定を自動追加
  して同期を保ち（手動の同期ボタンは用意しない）、変更があれば `EditorUtility.SetDirty` する。
- `ClipMaskSourceTags` の入力はテキストではなく、3.1 節の条件を満たすタグのみを列挙した
  複数選択 UI（最大3件、追加/削除ボタン付きのリスト等）にする。条件を満たさない設定が残っている
  場合は `HelpBox` 等でエディタ上に警告を表示する。

### 4.2 プレビュー非対応

`AvatarData.OnPreviewGUI` / `AvatarImage` の `OnPreviewGUI`（`GUI.DrawTexture` によるインスペクタ
プレビュー）はブレンドモード・マスクを反映しない。**素のプレビュー（単純重ね、ブレンド・マスクなし）
自体は既存の `AvatarData.OnPreviewGUI` をそのまま呼んで維持する**（プレビュー機能自体を消さない）。
ブレンド・マスクを反映した見た目の確認は PlayMode 実行時に行う。

## 5. シェーダー・マテリアル

### 5.1 シェーダー方式

uGUI 用シェーダー1本に対し、`Blend` ステートをプロパティ参照で指定する方式を採る（モード追加時も
シェーダーは1本のまま、enum とマッピングの追加のみで済む）。ただし以下2点の理由から、
単純な `Blend [_SrcBlend] [_DstBlend]` の2プロパティだけでは不十分と判明したため、RGB用/Alpha用を
分離し、色の前処理プロパティを追加する。

- **透過パーツでの破綻**: パーツ画像はストレートアルファ（透明領域は概ね `rgb=0, a=0`）の PNG。
  これに対し単純な `Blend DstColor Zero`（乗算）を適用すると、透明領域で `dst*0` となり
  **矩形全体が真っ黒になる**。`Additive`/`Screen` も同様にアルファを考慮しないと不自然になる。
  → フラグメントシェーダー側で `_ColorOp` に応じた rgb の前処理を行ってから固定の Blend ステートに
  渡す（乗算は `rgb = lerp(1, rgb, a)`、加算・スクリーンは `rgb *= a` のプリマルチプライ）。
- **RenderTexture 併用時のアルファ規約**: `EnableRenderTexture` 有効時、Utage は `Utage/RenderTexture`
  シェーダー（`Blend SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha`）で全パーツを描画し、
  RenderTexture のアルファチャンネルを「1 − カバレッジ」として蓄積する独自規約を使っている
  （後段の `Utage-DrawByRenderTexture.shader` が `color.a = (1-color.a)*IN.color.a` で復元する）。
  AvatarEx のパーツ別マテリアルもこの規約に乗る必要があるため、RGB 用と Alpha 用で別々の
  Blend 式を持たせる。

```
Shader "UtageExtension/AvatarEx/BlendableUI"
{
    Properties
    {
        [HideInInspector] _SrcBlendRGB("__srcRGB", Float) = 5   // SrcAlpha
        [HideInInspector] _DstBlendRGB("__dstRGB", Float) = 10  // OneMinusSrcAlpha
        [HideInInspector] _SrcBlendA("__srcA", Float) = 1       // One
        [HideInInspector] _DstBlendA("__dstA", Float) = 10      // OneMinusSrcAlpha
        [HideInInspector] _ColorOp("__colorOp", Float) = 0      // 0=None 1=Premultiply(rgb*=a) 2=LerpToWhite
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _MaskTex0("Clip Mask 0", 2D) = "black" {}
        _MaskTex1("Clip Mask 1", 2D) = "black" {}
        _MaskTex2("Clip Mask 2", 2D) = "black" {}
        _MaskXform0("Mask UV Xform 0", Vector) = (1,1,0,0)
        _MaskXform1("Mask UV Xform 1", Vector) = (1,1,0,0)
        _MaskXform2("Mask UV Xform 2", Vector) = (1,1,0,0)
        _MaskBounds0("Mask UV Bounds 0", Vector) = (0,0,1,1)
        _MaskBounds1("Mask UV Bounds 1", Vector) = (0,0,1,1)
        _MaskBounds2("Mask UV Bounds 2", Vector) = (0,0,1,1)
        _MaskCount("Mask Count", Float) = 0   // 0=マスクなし, 1〜3=有効なマスクスロット数
        _MaskInvert("Invert Mask", Float) = 0
        // uGUI標準のStencil関連プロパティ（Mask/RectMask2Dとの共存用）も必要
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend [_SrcBlendRGB] [_DstBlendRGB], [_SrcBlendA] [_DstBlendA]
        Pass { ... }
    }
}
```

ブレンド値マッピング（`UnityEngine.Rendering.BlendMode`: Zero=0, One=1, DstColor=2, SrcAlpha=5,
OneMinusDstColor=6, OneMinusSrcAlpha=10）:

| Mode | `_ColorOp` | rgb前処理 | `_SrcBlendRGB` | `_DstBlendRGB` |
|---|---|---|---|---|
| Normal | 0 | なし | SrcAlpha(5) | OneMinusSrcAlpha(10) |
| Multiply | 2 | `rgb = lerp(1, rgb, a)` | DstColor(2) | Zero(0) |
| Additive | 1 | `rgb *= a` | One(1) | One(1) |
| Screen | 1 | `rgb *= a` | OneMinusDstColor(6) | One(1) |

Alpha側のブレンド（ブレンドモードに依存せず、描画先で決まる）:

| 描画先 | `_SrcBlendA` | `_DstBlendA` |
|---|---|---|
| 通常 Canvas | One(1) | OneMinusSrcAlpha(10) |
| RenderTexture 空間内 | Zero(0) | OneMinusSrcAlpha(10)（`Utage/RenderTexture` と同一式） |

- `_MaskXform` はメインスプライトのUVをマスクスプライトのUVへ直接写すアフィン変換（scale.xy,
  offset.zw）。C# 側で「メインUV→メイン矩形→マスク矩形（実サイズ比 `rect.size / pixelsPerUnit`
  で中心そろえ）→マスクUV」を1本に合成して渡す。各スプライトの描画範囲とUVは `Image`（Simple）と
  同じ `Sprites.DataUtility.GetPadding` / `GetOuterUV` で求めるため、トリムやアトラス詰めで
  `textureRect` が `rect` より小さいスプライトも伸縮せずに重なる。範囲外クランプ用の矩形
  `_MaskBounds` にはマスクスプライトの outerUV を渡す（詳細は6章）。`UguiFlip` は頂点座標のみを反転し UV は変更しないため、Flip 時の UV 補正は不要。
- 未使用のマスクスロットは既定値 `"black"`（alpha=0）で埋めるが、これは保険であり、
  実際には `_MaskCount` の値で **明示的に分岐**して未使用スロットの `tex2D` 自体を評価しない
  （`_MaskXform` 未設定時の不定動作を避けるため）。`_MaskBounds` の範囲外は明示的に alpha=0 に
  クランプし、隣接するアトラス領域を誤って拾わないようにする。
  `maskAlpha = _MaskCount<=0.5 ? 1 : max(...)`（有効スロットのみ）の後に `_MaskInvert` を一括適用する。
- マスクを使わないパーツは `_MaskCount=0` とし、そもそもマスク分岐を通らない。

### 5.2 マテリアル管理方針

- **マスクを使わないパーツ**: `AvatarExBlendMode` の値ごと・描画先（通常Canvas / RenderTexture空間内）
  ごとに **共有マテリアル**（static にキャッシュした計8種のインスタンス、`_MaskCount=0`）を使い回す。
  さらに `Normal` かつマスクなしのパーツは `Image.material` に一切触れず、Utage が設定した既定
  マテリアル（`null` または RenderTexture 有効時の `Utage/RenderTexture`）をそのまま保持する
  （既存挙動を完全に保持し、回帰を避ける）。
- **マスクを使うパーツ**: マスクソースのテクスチャが表示中パターンに応じて変わるため、
  `(タグ, スロット番号)` の組ごとに固有のマテリアルインスタンスを1つ生成してキャッシュし、
  切替時は `SetTexture`/`SetFloat`/`SetVector` で使い回す（毎回 `new Material` しない）。
  スロット番号を含めるのは、マスク対象に `OptionTag`（3.1節）を許容した結果、同一タグで複数の
  `Image` が同時に存在しうるため。

### 5.3 座標系・サイズ差の扱い

`AvatarData.RebuildSize()`（全パーツ同一サイズを検証する意図の処理）は実装上のバグにより
実際には機能しておらず、**全パーツ同一サイズは Utage 側で保証されていない**。そのため AvatarEx は
サイズ差を吸収できる設計とする：マスクソースのスプライトとマスク対象パーツのスプライトの
実サイズ比を求め、両者が中心そろえで重なる前提でシェーダー側の UV を線形変換する（5.1節・6章）。
トリムによる描画範囲のずれも同じ変換に含める。これに加えて、AvatarEx 独自のエディタ検証（全パーツ
同一サイズかどうか・回転パック・タイトパック）を用意し、極端な相違や UV 変換の前提が崩れる構成には `HelpBox` で警告する。

## 6. 実行時の解決ロジック（`AvatarExPostProcessor`）

`AvatarImage.OnPostRefresh` 発火時に、対象アバターの `AvatarExData`（`AvatarImage.AvatarData as
AvatarExData`）を取得し、`AvatarImage` の子（`childRoot`。private プロパティのため
`transform.Find("childRoot")` で辿る）配下の各パーツ `Image` に対して以下を行う。

0. `avatar.AvatarData` が `AvatarExData` でなければ何もしない（既存 `FileType=Avatar` には無関係）。
   自身が RenderTexture 空間内（`AdvGraphicObjectAvatarEx` から渡される `isRenderTextureSpace`
   フラグ、または `GetComponentInParent<AdvRenderTextureSpace>()` での自動判定）かどうかを
   保持しておき、5.1節のAlpha側ブレンド選択に使う。
1. `Image.sprite` の参照同一性で所属タグを特定する（`AvatarExDataCache` が事前構築した
   `Sprite → Tag` 辞書を引くのみ。線形走査はしない）。
2. 対応する `PartExtraSetting` を引く。無ければ `Normal` / マスクなし扱い（`Image.material` に
   触れない）。
3. `ClipMaskSourceTags` が1件以上設定されている場合、リスト内の **各タグ** に対して3.1節の3条件を
   検証する（検証結果自体もキャッシュ済みのものを使う）。
   - **1つでも** 検証NGのタグがあれば、リスト全体を無効とみなし、`(AvatarExData, タグ)` 単位で
     初回のみ `Debug.LogError` で `[UtageAvatarEx]` プレフィックス付きログを出し、マスクなし・
     `Normal` 表示にフォールバックする（実行は止めない。ログ洪水を防ぐため2回目以降は抑制する）。
   - 全件OKの場合、各マスクソースタグの **現在表示中スプライト** を取得する（同一タグが複数
     Category に紐づく場合は先頭1件のみ、2章参照）。
     - マスクソースタグが現在 `None`（非表示）パターンの場合、そのソースは合成対象から除外する
       （＝alpha=0として扱うのと等価、3.2節）。
     - 有効なソースについて、対象パーツのスプライトとの実サイズ比から `_MaskXform` / `_MaskBounds`
       を算出し、テクスチャを `_MaskTex0`〜`_MaskTex2` に設定して `_MaskCount` を有効件数に合わせる。
     - **全指定ソースが `None` だった場合**（有効件数0）:
       - `InvertMask == false` のとき: マスク対象パーツを **完全非表示** にする（`Image.enabled =
         false`。反転していない＝「何も無ければ隠す」が自然なため）。
       - `InvertMask == true` のとき: `_MaskCount=0`（マスクなし）として **全面表示** する
         （反転した結果 `1-0=1` になるべきところを非表示にしては矛盾するため）。
4. `BlendMode` と 描画先（通常Canvas / RenderTexture空間内）に応じて共有／専用マテリアルを選択し、
   `Image.material` に設定する（`InvertMask` は `_MaskInvert` としてマテリアルに渡す）。
   前回適用した状態（sprite / mask / count / invert / mode）と変化がなければ、`SetTexture` 等を
   一切呼ばずスキップする（口パク中の高頻度呼び出しに対する負荷対策）。
   `Image.enabled` は非表示から復帰する場合に備え、上記の完全非表示ケース以外では毎回 `true` に
   戻す。

## 7. エラーハンドリング方針

- ログプレフィックスは `[UtageAvatarEx]` で統一する（既存 FMOD プラグインの `[UtageFmod]` に倣う）。
- 不正な設定（存在しないタグ、`OptionTag` 指定、多段参照）はすべて **ログを出しつつ安全側
  （マスクなし・Normal表示）にフォールバックし、シナリオの進行は止めない**。複数マスクソースの
  うち1件でもNGなら、部分適用はせずリスト全体を無効化する。
- `OnPostRefresh` は口パク中に約10Hzで発火するため、同一の不正設定について毎回ログを出すと
  ログが埋もれる。**`(AvatarExData, タグ)` 単位で初回のみ**エラー・警告ログを出す抑制機構を持つ。
- 循環参照は3.1節の制約（1段のみ・マスクソースは非マスクタグ限定）により構造的に発生しないため、
  専用の循環検出ロジックは不要。

## 8. 制限事項（現時点で判明している分）

- ブレンドモード・マスクは Category（タグ）単位の設定であり、シナリオ側（Characterシートの列や
  カスタムコマンド）から実行時に動的変更することはできない。変更したい場合は `AvatarExData` 側の
  設定を変更する必要がある。
- マスク**ソース**に指定できるのは通常タグのみ。`OptionTag`（アクセサリ等）はマスクソースにできない
  （マスク**対象**側は `OptionTag` を許容する）。
- マスク参照は1段まで。マスクソース自身をマスク対象にする多段構成はできない。
- マスクソースは **最大3件まで**（シェーダーのマスクスロット数による）。複数指定時は OR（max）合成。
- エディタのインスペクタプレビュー（ブレンド・マスクを反映したもの）は未対応。見た目確認は
  PlayMode 実行時に行う（素のプレビュー自体は維持する、4.2節）。
- パーツ画像のサイズ差はシェーダー側のUVリマップ（中心そろえのアフィン変換）で吸収するが、
  極端なアスペクト比の相違やスプライトの回転パック・タイトパック設定によっては見た目が崩れる場合が
  ある。エディタ検証（4章）で警告するが、実行時の完全な安全は保証しない。
- uGUI の `Mask`（Stencilクリッピング）配下で AvatarEx のパーツを使う構成は**非サポート**。
  `StencilMaterial.Add` がベースマテリアルをコピーするため、コピー後の `SetTexture` 等の更新が
  反映されない可能性がある。クリッピングが必要な場合は `RectMask2D`（`CanvasRenderer` によるクリップ、
  マテリアル非改変）を使うこと。
- `EnableRenderTexture`（RenderTexture への焼き込み）と併用可能。ブレンド・マスクの合成は
  RenderTexture に焼く前（Canvas内）で完結し、その合成結果がそのまま RenderTexture に書き込まれる。
- **パーツ画像の配置場所に注意**: `AdvScenarioDataProject.CustomImportSpriteFolders`
  （プロジェクト設定でSprite化を強制するフォルダ一覧、既定で `Texture/Character` 等が登録されている）
  配下に画像を置くと、`Utage.ResourceImporter.OnPreprocessTexture` が再インポートのたびに
  `TextureImporterType.Default` へ強制的に戻してしまい、`Sprite` として使えなくなる
  （`AvatarData` 標準の `Apply` ボタンによる Sprite 化も同様にこの影響を受ける）。
  AvatarEx／Avatar 用のパーツ画像は **`CustomImportSpriteFolders` に登録されていない場所**
  （例: `Assets/<プロジェクト>/Textures/<キャラ名>/` 等）に置くこと。`AvatarExData`／`AvatarData`
  アセット自体（`.asset`）は `TextureImporter` の対象外なので、Character の `FileName` 解決規則
  （`Resources/<RootResourceDir>/Texture/Character/` 配下）にそのまま従ってよい。

## 9. 将来拡張の余地

- ブレンドモード種類の追加（Overlay, SoftLight, Divide 等）は `AvatarExBlendMode` enum と
  Src/Dst Blend マッピングの追加のみで対応可能。
- パーツ単位ではなくアバター全体への一括ブレンド／マスクが必要になった場合は、既存の
  RenderTexture 経由の仕組み（`AdvGraphicObjectAvatar` の `EnableRenderTexture` 分岐）との
  連携を別途検討する（本拡張は RenderTexture 併用に対応済み、8章参照）。
- 本プロジェクトは Linear カラースペースのため、ハードウェアブレンドによる Multiply/Screen は
  Photoshop 等（sRGB空間でのプレビュー）と見た目が一致しない（Multiplyはより暗く/コントラスト
  強めに、Screenはより明るく出る傾向）。今回はリニア空間ブレンドのまま実装し、実機の見た目で
  許容可否を判断する。許容できない場合、GrabPass 等で宛先色を読んで sRGB 空間で合成する版を
  将来拡張として別モードで追加することを検討する。
