# VStoVSC

Visual Studio のソリューションファイル（`.sln` / `.slnx`）を **VS Code 用** に変換する Windows 向けデスクトップアプリです。
ファイルをドラッグ＆ドロップするだけで、VS Code から MSBuild ビルド・F5 デバッグができるようになります。

---

## ✨ できること

- **ドラッグ＆ドロップで完結**: `.sln` / `.slnx` をウィンドウに放り込むだけ
- **`.vscode/tasks.json` 自動生成**: ビルド（Debug / Release）/ クリーン / リビルドのタスクを生成
- **`.vscode/launch.json` 自動生成**: 実行可能プロジェクト（`Exe` / `WinExe`）を検出して F5 デバッグ対応に
- **`.slnx`（VS 2022 新形式）対応**: 旧 `.sln` も `.slnx` も両方読める
- **多言語**: 17 言語（日本語 / English / 简体中文 / 繁體中文 / Deutsch / Français / Español / Italiano / Português (Brasil) / Русский / Українська / Bahasa Indonesia / Tagalog / தமிழ் / 한국어 / Latina / संस्कृतम्）— システム言語に自動追従
- **ライト / ダーク自動切替**: OS の外観設定に追従
- **自動更新**: 起動時に新バージョンを確認し、バックグラウンドでダウンロード → 次回起動時に適用

---

## 💿 インストール

[**VStoVSC-win-Setup.exe をダウンロード**](https://vs2vsc.kagayoi.com/VStoVSC-win-Setup.exe)

ダウンロードしたインストーラを実行するだけです。スタートメニューとデスクトップにショートカットが作成されます。
.NET ランタイムは同梱されているので、別途インストール不要です。

> 動作確認: Windows 10 / Windows 11

---

## 🎯 使い方

1. インストール後、スタートメニュー or デスクトップから **VStoVSC** を起動
2. アプリ中央のドロップエリアに、変換したい `.sln` または `.slnx` ファイルを **ドラッグ＆ドロップ**
   - またはドロップエリアをクリックしてファイル選択ダイアログから選んでも OK
3. ソリューションファイルと同じフォルダに `.vscode/` フォルダが生成されます
4. VS Code でそのソリューションフォルダを開けば、ビルド・F5 デバッグがすぐに使えます

### 生成されるファイル

| ファイル | 内容 |
|---|---|
| `.vscode/tasks.json` | `MSBuild ビルド (Debug)` / `(Release)` / `Clean` / `Rebuild` の 4 タスク |
| `.vscode/launch.json` | 実行可能プロジェクト用の F5 デバッグ設定（`Exe` / `WinExe` 検出時のみ） |

---

## ⚠️ 注意事項

- **既存 `.vscode/` フォルダがある場合**: 再生成して置き換えるか、既存設定を残して `tasks.json` だけ更新するかを確認ダイアログで選べます。再生成では新しいファイルの生成完了まで旧設定を保持します。残す方を選んだ場合、既存の `launch.json` は上書きしません（まだ無いときだけ新規生成します）
- **`launch.json` が生成されないとき**: ソリューション内に実行可能プロジェクト（`OutputType` が `Exe` / `WinExe`）が見つからなかった可能性があります。クラスライブラリ専用ソリューションなどでは生成されません。この場合は「警告付きで完了」ダイアログで理由をお知らせします
- **`launch.json` の起動パス**: MSBuild でプロジェクトを評価して実際の出力先を求めるため、`AssemblyName` / `OutputPath` / `RuntimeIdentifier` / `AppendTargetFrameworkToOutputPath` を変更している場合も追従します。評価できないプロジェクト（解決できない SDK など）だけ `bin/Debug/<TargetFramework>/` の既定レイアウトを仮定するので、その場合は生成後に `program` を確認してください
- **`.sln` を渡した場合**: 同名の `.slnx` が無ければ `dotnet sln migrate` を実行して `.slnx` を作成し、`tasks.json` はその `.slnx` を対象にします（元の `.sln` は残ります）
- **MSBuild が見つからない場合**: 変換は実行されずエラーを表示します。Visual Studio または MSBuild Build Tools をインストールしてからやり直してください
- **MSBuild 検出に時間がかかる場合**: Visual Studio のインストール構成によっては、初回起動時 / 変換時に MSBuild の場所特定に数秒〜数十秒かかることがあります（2 回目以降は早くなります）

---

## 🩺 トラブルシュート

### インストーラが SmartScreen に止められる
インストーラにはコード署名を付与していますが、Windows SmartScreen の評価状況によっては警告が出る場合があります。ダウンロード元が `vs2vsc.kagayoi.com`、発行元が `Open Source Developer Yuichiro Shinozaki` であることを確認してから実行してください。

### 自動更新が走らない
通信状況やインターネット接続不可の環境では更新確認に失敗することがあります。手動で最新版を取り直したい場合は、上記の Setup.exe を再ダウンロードしてインストールしてください。

### `.sln` / `.slnx` 以外の拡張子を指定したらエラーが出る
仕様です。Visual Studio のソリューションファイル形式のみ対応しています。

---

## 📝 ライセンス

[MIT License](LICENSE) — © 2026 ゆろち

---

## 🔒 プライバシー

本アプリは、ユーザー識別子・利用統計などを一切収集しません。
詳細は [プライバシーポリシー](PRIVACY_POLICY.md) を参照してください。
