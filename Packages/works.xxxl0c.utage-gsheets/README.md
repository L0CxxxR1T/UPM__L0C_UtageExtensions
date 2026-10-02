# Googleスプレッドシート シナリオリーダー

Utageノベルゲームエンジン用のGoogleスプレッドシートシナリオリーダー拡張機能です。

## 機能

- Googleスプレッドシート1冊（複数シート）をまとめてシナリオデータとして読み込み
- Google Apps Script経由でシート内容を取得（非公開シートにも対応）
- Utage標準フォーマットをサポート
- シート名の先頭に指定した記号（デフォルト`#`）を付けるとインポート対象から除外
- Unity Editor上で簡単セットアップ

## セットアップ手順

### 1. Google Apps Scriptのセットアップ

1. スプレッドシートで「拡張機能」→「Apps Script」を開く
2. Unityで設定アセットを作成（後述）し、インスペクタの「Apps Script テンプレートを表示」ボタンをクリック
3. 表示されたコードをコピーしてApps Scriptエディタに貼り付け
4. 「デプロイ」→「新しいデプロイ」をクリック
5. 種類を「ウェブアプリ」に選択
6. 以下の設定でデプロイ：
   - 説明: 任意（例: "Utage Scenario Reader"）
   - 実行するユーザー: 自分
   - アクセスできるユーザー: **全員**（重要）
7. 「デプロイ」をクリック
8. 表示されるウェブアプリURLをコピー

### 2. Unityでの設定アセット作成

1. Unity Editorで右クリック → `Create > Utage > ScenarioFileReader > GoogleSheets`
2. 作成された設定アセットを選択
3. `Apps Script URL` に手順1でコピーしたURLを入力（必須。未設定の場合インポートは失敗します）
4. `Sheet Comment Prefix` はコメントアウトしたいシート名の先頭文字（デフォルト: `#`）

### 3. スプレッドシート参照アセットの作成

1. シナリオを配置するフォルダ（`AdvScenarioDataProject` の `Chapters` に設定した `Scenario Dir` の配下）で右クリック
   → `Create > Utage > ScenarioFileReader > GoogleSheetsSource`
2. 作成されたアセットを選択し、`Spreadsheet Id` にスプレッドシートのIDまたはURLを入力
   - ID直接指定: `1BxiMVs0XRA5nFMdKvBdBZjGMUUqpt35fsTguSG1TM6o`
   - URL指定: `https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjGMUUqpt35fsTguSG1TM6o/edit`

> 注意: 以前のバージョンではシナリオファイルパスに直接GoogleスプレッドシートのURLを指定する形式でしたが、
> Utageのインポータはファイルパスをアセット参照からしか集めないため、この方式は動作しません。
> 現在はこの `GoogleSheetsSource` アセットを経由する方式に変更しています。

### 4. AdvScenarioDataProjectに設定を追加

1. `AdvScenarioDataProject` アセットを選択
2. `Scenario File Reader Settings` リストの「+」ボタンをクリック
3. 手順2で作成したGoogleSheets設定アセットを追加
4. `Chapters` の `Scenario Dir` に、手順3のアセットを置いたフォルダを指定

### 5. シナリオのインポート

1. `AdvScenarioDataProject` の「**All Import**（全インポート）」ボタンをクリック
   - シート側の内容を変更しても Unity 側のファイルは変化しないため、Utage の差分インポート機能では変更を検知できません。取り込みは必ず全インポートで行ってください。
2. Googleスプレッドシートの全シートがデータとして読み込まれます

## 動作の仕組み

- 1つの `GoogleSheetsSource` アセットにつき1回のリクエストで、スプレッドシート内の全シートをまとめて取得します（Excelリーダーと同じ「1ブック=複数シート」モデル）。
- Apps Scriptはシート内容をJSON形式（`{ sheets: [ { name, rows: [ { cells: [...] } ] } ] }`）で返します。CSVではないため、カンマ・改行・ダブルクォートを含むセルでもエスケープ処理を意識する必要はありません。
- 取得したシートのうち、シート名が `Sheet Comment Prefix` で始まるものは自動的に除外されます。

## トラブルシューティング

### デプロイ時のエラー

- 「アクセスできるユーザー」が「全員」になっているか確認
- Googleアカウントの権限設定を確認
- 企業環境の場合、管理者の制限を受けている可能性があります

### 読み込みエラー・インポートが失敗する

- Apps Script URLが正しいか確認
- スプレッドシートの共有設定でApps Script実行ユーザーがアクセスできるか確認
- `GoogleSheetsSource` アセットの `Spreadsheet Id` が正しいか確認
- ブラウザで `{Apps ScriptのURL}?sheetId={スプレッドシートID}` を直接開き、JSONが返ってくるか確認

### コンパイルエラー

- Unityプロジェクトに `Utage` アセットが正しくインストールされているか確認
- asmdefの参照設定が正しいか確認

## 注意点

- Googleアカウントが必要です
- 企業環境ではGoogle Workspaceの制限でApps Scriptが使えない場合があります
- デプロイ後のURLは再デプロイ時に変更されます
- 非公開シートを使用する場合、Apps Script実行ユーザーにシートへのアクセス権が必要です
- シート内容の変更は差分インポートで検知できません。必ず全インポートを使用してください

## ライセンス

この拡張機能はUtageアセットの拡張として作成されています。
