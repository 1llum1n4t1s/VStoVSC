# リポジトリ作業規約

## 参照先と構造

- 利用者向けの説明は [README.md](README.md)、実装の構造と不変条件は [DESIGN.md](DESIGN.md) を参照する。
- アプリは `src/VStoVSC/`、xUnit テストは `tests/VStoVSC.Tests/`、ソリューションは `VStoVSC.slnx` にある。
- 共通ビルド設定とバージョンの正本は `Directory.Build.props`、依存関係は各 `.csproj` と `packages.lock.json` にある。

## ビルドと検証

Windows x64 と .NET 10 SDK を使い、リポジトリルートで実行する。警告はエラーとして扱い、Nullable とビルド時コードスタイル検証を維持する。

```powershell
dotnet restore VStoVSC.slnx --locked-mode
dotnet build VStoVSC.slnx -c Release --no-restore
dotnet test tests/VStoVSC.Tests/VStoVSC.Tests.csproj -c Release --no-build --no-restore
```

- 依存変更時は lockfile を更新し、上記の locked restore で整合性を検証する。
- 変換処理の変更時は、正常系に加え既存設定保持・置換失敗・ロールバック・呼び出しスレッド分離の既存テストを実行する。テストは並列実行を無効にしている。
- 実際のプロジェクト評価を変更した場合は、Visual Studio または MSBuild Build Tools がある環境で生成された出力パスも確認する。空ソリューションの fixture は MSBuild の存在確認を代替しており、実ビルドを保証しない。
- UI 文字列を追加・変更するときは `Resources/Locales/` の言語リソースと `App.Text` の参照を整合させる。

## 実装と配布の制約

- MSBuild の読み込みを変更するときは `Microsoft.Build` の `ExcludeAssets="runtime"` と Locator による実環境の解決を維持する。Native AOT は Locator のリフレクション利用により無効としている。
- 設定変更は `SettingsManager.Mutate` / `MutateAndSave` を使う。取得した `Current` はスナップショットとして扱う。
- 変換・更新処理の変更時は [DESIGN.md](DESIGN.md) の排他制御、ファイル保護、配信元の境界を検証する。
- 配布用の self-contained ビルドでは restore と publish の RuntimeIdentifier / SelfContained を一致させる。具体的な処理は `scripts/release-local.ps1` を参照する。
- リリース依頼時は `vava.config.json` が指定するローカル署名手順を使う。`release-local.ps1` はアップロード・キャッシュ削除・世代整理を含むため、通常の検証には上記の build / test を使う。`-VerifyOnly` も外部変更を伴う再開処理として扱う。
- 製品ページの配信は `vps-web/deploy/deploy-lp.ps1` を使う。公開ホスト・更新ファイルの既存経路を維持する。
