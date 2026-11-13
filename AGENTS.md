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
dotnet test tests/VStoVSC.Tests/VStoVSC.Tests.csproj -c Release -p:Platform=x64 --no-build --no-restore
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
- リリース依頼時は GitHub Actions の `.github/workflows/release.yml`（手動起動・Environment `release`）を使う。ワークフローは `scripts/release-local.ps1` を実行し、署名は [Sigilune](https://github.com/1llum1n4t1s/Sigilune)（Certum SimplySign のクラウド鍵を signtool `/dlib` で使う CLI。SimplySign Desktop・手入力コード不要）が行う。/vava で版を上げて push した後、`gh workflow run release.yml --ref <ブランチ>` で起動し `gh run watch` で完了を確認する（`-f skip_upload=true` は配信せず署名済み成果物だけを Actions に残す）。ローカル実行は検証・再開に使う。`release-local.ps1` はアップロード・キャッシュ削除・世代整理を含むため、通常の検証には上記の build / test を使う。`-VerifyOnly` も外部変更を伴う再開処理として扱う。
- 製品ページの配信は `vps-web/deploy/deploy-lp.ps1` を使う。公開ホスト・更新ファイルの既存経路を維持する。

### リリースの実行

R2 配布は PowerShell 7.2 以降と同梱の `scripts/R2Distribution.Common.ps1` を使う。配布に Node.js・pnpm・Wrangler は不要（製品の JS ビルド要件は別途維持）。署名・転送・検証の正本は同梱の `scripts/R2Transfer.Core.cs` とし、PS helper は公開関数の互換入口とする。更新時は中核と使用する入口を同じディレクトリへ同梱する。固定名は `no-cache, must-revalidate`、バージョン付き成果物は不変として 1 年キャッシュし、payload を先、assets・RELEASES・releases を最後に送信する。

署名の共通関数は `scripts/Sigilune.Common.ps1`（実行ファイルは `SIGILUNE_EXE` → PATH → `%USERPROFILE%\.local\Sigilune\bin\sigilune.exe`）。Velopack は `--signTemplate "<sigilune> sign {{file}}"` で署名し、Sigilune が署名後に有効性・署名者・タイムスタンプを検証する。CI は Environment `release` の Secrets（`SIGILUNE_EMAIL` / `SIGILUNE_TOTP_URI` / `CLOUDFLARE_API_TOKEN`）を使い、終了時に `sigilune logout` する。ローカルは `sigilune setup` 済みの資格情報と、環境変数が無い場合だけ secrets.json の `cloudflare.api_token` を使う。

ローカルで実行する場合は、リポジトリルートで Sigilune のクラウドログインを事前確認し、成功した場合だけ実行する（`release-local.ps1` もビルド前に同じ確認を行う）。失敗時は `sigilune status` で登録を確認するよう案内し、ログインを自動再試行せず、署名を省略して続行しない。

```powershell
$sigilune = if ($env:SIGILUNE_EXE) { $env:SIGILUNE_EXE } else { Join-Path $env:USERPROFILE '.local\Sigilune\bin\sigilune.exe' }
& $sigilune login
if ($LASTEXITCODE -eq 0) { pwsh -NoProfile -File scripts/release-local.ps1 } else { throw 'Sigilune でクラウドにログインできなかったよ。sigilune status で登録を確認してから /vava を再実行してね' }
```
