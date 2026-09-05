# Changelog

## 2.0.17 - 2026-09-05

- アプリの機能・操作に変更はありません。

## [2.0.16] — Git 記録日: 2026-09-05

- 依存ライブラリとWranglerを更新
- 個人開発環境の除外設定を整理
- NuGet依存関係を更新 (#14)

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/d8e22a2a86b4a9ecf39d9ca161050fe24639bf8a) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/8de34a4bf49d4c062d03557d7f9f03049ff4bd21...d8e22a2a86b4a9ecf39d9ca161050fe24639bf8a)。

## [2.0.15] — Git 記録日: 2026-08-06

- launch.json: 既存 .vscode 保持時に既存 launch.json を上書きしない
- launch.json: MSBuild 評価 (TargetDir) で AssemblyName / OutputPath / RuntimeIdentifier / net4x SDK 形式の出力先取り違えを解消
- launch.json: 構成の個別生成失敗を警告として表示（黙殺を廃止）
- MSBuild: 未検出時に空 command の tasks.json を生成して成功表示していた 問題を修正、プロジェクト評価キャッシュを変換ごとに破棄
- .sln→.slnx migrate 実行対象を明示、タイムアウト付きに変更
- ロケール: 既存 .vscode 確認文言・ファイル選択ダイアログを17言語対応、 未登録 Locale 設定時のフォールバックを追加

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/8de34a4bf49d4c062d03557d7f9f03049ff4bd21) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/c35f5ff6ad1a1ac99a75bbbb97a3e1f93d79aeaf...8de34a4bf49d4c062d03557d7f9f03049ff4bd21)。

## [2.0.14] — Git 記録日: 2026-07-27

- リリース時の R2 クリーンアップを「直近 2 世代保持」に変更
- タスクバーの AppUserModelID を明示設定してショートカットと同一グループ化
- 不要になった NU1903 抑制を削除
- deploy-landing の依存を更新 (checkout v7.0.1 / setup-node v7.0.0 / wrangler 4.114.0)
- NuGet を更新 (SuperLightLogger 1.0.10 / VelopackUpdateDialog.Avalonia 1.0.12)
- 配信ドメインを nephilim.jp から kagayoi.com へ移行

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/c35f5ff6ad1a1ac99a75bbbb97a3e1f93d79aeaf) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/3d13c148ab4c3f2ca9b22f4bf946eb7234dd3d74...c35f5ff6ad1a1ac99a75bbbb97a3e1f93d79aeaf)。

## [2.0.13] — Git 記録日: 2026-07-20

- スタートメニューショートカットを移行
- R2 レスポンスが byte[] になるケースに対応 (UTF-8 デコード + keep set 形式検証 + CDN キャッシュバイパス)
- バージョン読み取りを XPath 化 (Version 無し PropertyGroup 混在で StrictMode が throw する問題)

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/3d13c148ab4c3f2ca9b22f4bf946eb7234dd3d74) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/a2e57e0f3e83a19fb7865d956cba3f8d64934394...3d13c148ab4c3f2ca9b22f4bf946eb7234dd3d74)。

## [2.0.12] — Git 記録日: 2026-06-12

- VelopackUpdateDialog 1.0.7 / Microsoft.Build 18.7.1 更新 + コード署名導入
- vpk を実行時に NuGet 最新安定版へ解決する方式に変更 (ハードコード固定を廃止)
- vpk を最新安定版 1.2.0 に更新 (Velopack は最新必須ルール)
- Lhamiel 互換のランディングページを追加（vs2vsc.nephilim.jp）

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/a2e57e0f3e83a19fb7865d956cba3f8d64934394) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/7dbfb83e3eb0f39989656dfa2f65dd2af738a5dd...a2e57e0f3e83a19fb7865d956cba3f8d64934394)。

## [2.0.11] — Git 記録日: 2026-06-01

- VelopackUpdateDialog.Avalonia を 1.0.6 に更新

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/7dbfb83e3eb0f39989656dfa2f65dd2af738a5dd) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/f695845ec05ee8ad24686d15d98479ad995e6886...7dbfb83e3eb0f39989656dfa2f65dd2af738a5dd)。

## [2.0.10] — Git 記録日: 2026-05-29

- NuGet パッケージ最新化

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/f695845ec05ee8ad24686d15d98479ad995e6886) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/2aa2b807721c5e125a2d63c89c1011d51adb2a44...f695845ec05ee8ad24686d15d98479ad995e6886)。

## [2.0.9] — Git 記録日: 2026-05-28

- Microsoft Store公開廃止 + Lhamielスタイル全面リファクタ (#12)

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/2aa2b807721c5e125a2d63c89c1011d51adb2a44) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/80ddf67bbcc70cb2173f38212cbc113758d91663...2aa2b807721c5e125a2d63c89c1011d51adb2a44)。

## [2.0.8] — Git 記録日: 2026-02-21

- Microsoft Store パッケージ作成時に x64 構成を選択できない問題を修正し、ソリューションと公開用の構成を整備。

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/80ddf67bbcc70cb2173f38212cbc113758d91663) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/853b52a593a7867291725c99fb80a843185fb836...80ddf67bbcc70cb2173f38212cbc113758d91663)。

## [2.0.4] — Git 記録日: 2026-01-30

- Avalonia UI化

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/853b52a593a7867291725c99fb80a843185fb836) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/48300d950d028052db24ca1ffc26f06b78bf8913...853b52a593a7867291725c99fb80a843185fb836)。

## [2.0.2] — Git 記録日: 2026-01-21

- README.md最新化

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/48300d950d028052db24ca1ffc26f06b78bf8913) / [変更差分](https://github.com/1llum1n4t1s/VStoVSC/compare/64db38c00275011f5102feb4c0da17aaecb96d94...48300d950d028052db24ca1ffc26f06b78bf8913)。

## [2.0.1] — Git 記録日: 2026-01-19

- Velopack による配布ワークフローを追加し、プロジェクト名・アイコン・画面設定を整備。

出典: [版の記録](https://github.com/1llum1n4t1s/VStoVSC/commit/64db38c00275011f5102feb4c0da17aaecb96d94)。
