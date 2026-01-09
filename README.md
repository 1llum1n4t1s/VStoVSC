# VStoVSC

Visual Studio のソリューション（.sln / .slnx）を VS Code 用に変換するための Windows 向け WPF アプリです。指定したソリューションの近くに `.vscode/tasks.json` を生成し、VS Code から MSBuild タスクを実行できるようにします。

## 主な機能
- ソリューションファイルのパス入力またはファイル選択で変換を開始
- `.vscode` フォルダの自動生成（既存フォルダは再作成）
- `tasks.json` に以下のタスクを生成
  - ビルド（Debug / Release）
  - クリーン
  - リビルド
- 進行状況や結果をログで表示

## 動作環境
- Windows
- .NET 10.0 (WPF)
- Visual Studio 2022 の MSBuild（MSBuildLocator で検出。必要に応じて既定パスを利用）
- Velopack（初回インストールと自動更新に使用）

## 使い方
1. アプリを起動します。
2. ソリューションファイルのパスを入力するか、「参照して変換」ボタンで `.sln` / `.slnx` を選択します。
3. 変換完了後、ソリューションと同階層に `.vscode/tasks.json` が生成されます。

## 生成されるファイル
```
<ソリューションのディレクトリ>/.vscode/tasks.json
```

## ビルド方法
```
dotnet build
```

## Velopack 初回インストール / 自動更新
- 起動時に Velopack のイベント処理を実行し、初回インストール時のショートカット作成などを行います。
- 自動更新は `VELOPACK_UPDATE_URL` 環境変数が設定されている場合のみ有効になります。
  - 例: `VELOPACK_UPDATE_URL=https://example.com/updates/`
  - 更新が見つかった場合はダウンロード後にアプリが再起動されます。

## 注意事項
- 既存の `.vscode` フォルダは削除して再作成されます。
- Visual Studio 2022 のインストール状況によっては MSBuild の検出に時間がかかる場合があります。
