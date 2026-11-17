# release-local.ps1 — ローカル署名付き Velopack リリース (Lhamiel テンプレートから横展開)
#
# 署名は Sigilune (Certum SimplySign のクラウド鍵を signtool /dlib で使う CLI) で行う。
# 通常のリリースは GitHub Actions の .github/workflows/release.yml (release/** への push で起動・Environment "release"。署名・配信の前に回帰テストを通す) が
# 本スクリプトを実行する。ローカル実行も同じ手順で動き、検証や再開に使う。
#
# 前提:
#   - Sigilune を使えること (scripts/Sigilune.Common.ps1 参照)。
#     ローカルは `sigilune setup` で Certum アカウントを登録済み、CI は SIGILUNE_EMAIL / SIGILUNE_TOTP_URI を渡す
#   - Directory.Build.props の <Version> がリリースしたいバージョンになっていること (/vava 済み)
#   - 環境変数 CLOUDFLARE_API_TOKEN (CI)、なければ D:\Dev\Secret\secrets.json の cloudflare.api_token (ローカル)
#
# 使い方:
#   pwsh scripts/release-local.ps1                # フルリリース (build + sign + upload + cleanup)
#   pwsh scripts/release-local.ps1 -SkipUpload    # ビルド + 署名のみ (アップロードしない動作確認用)
#   pwsh scripts/release-local.ps1 -VerifyOnly    # 既存成果物でキャッシュ削除と世代整理を再開

[CmdletBinding()]
param(
    [switch]$SkipUpload,
    [switch]$VerifyOnly,
    [string[]]$Runtimes = @('win-x64')
)


# 検証済みの作業対象だけを標準APIで削除する。
function Remove-LocalWorkItem {
    param([string[]]$LiteralPath, [string]$AllowedRoot, [switch]$IgnoreMissing, [switch]$AllowLeafJunction)
    if ([string]::IsNullOrWhiteSpace($AllowedRoot) -or -not [IO.Path]::IsPathRooted($AllowedRoot)) { throw 'Cleanup root must be absolute.' }
    $boundary = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    foreach ($target in $LiteralPath) {
        if ([string]::IsNullOrWhiteSpace($target) -or -not [IO.Path]::IsPathRooted($target)) { throw 'Cleanup target must be absolute.' }
        $resolved = [IO.Path]::GetFullPath($target)
        if (-not $resolved.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target is outside the allowed root.' }
        $ancestor = $resolved
        while ($ancestor) {
            if (Test-Path -LiteralPath $ancestor) {
                $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
                if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -and -not ($AllowLeafJunction -and $ancestor -eq $resolved -and $item.LinkType -eq 'Junction')) { throw 'Cleanup path contains a reparse point.' }
            }
            $ancestor = Split-Path $ancestor -Parent
        }
        if (-not (Test-Path -LiteralPath $resolved)) { if ($IgnoreMissing) { continue }; throw "Cleanup target is missing: $resolved" }
        $item = Get-Item -LiteralPath $resolved -Force -ErrorAction Stop
        $leafLink = [bool]($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
        if ($item.PSIsContainer -and -not $leafLink) {
            if (@(Get-ChildItem -LiteralPath $resolved -Recurse -Force -ErrorAction Stop | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw 'Cleanup directory contains a reparse point.' }
        }
        Remove-Item -LiteralPath $resolved -Recurse:($item.PSIsContainer -and -not $leafLink) -Force -ErrorAction Stop
        if (Test-Path -LiteralPath $resolved) { throw "Cleanup target remains: $resolved" }
    }
}

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'R2Distribution.Common.ps1')

# ---- 定数 (旧 CI 版 velopack-release.yml と揃える) ----
# Velopack (vpk) は常に最新安定版を使う (ゆろ君ルール): NuGet から実行時に最新を解決して pin する
$VpkVersion = (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/vpk/index.json' -TimeoutSec 30).versions |
    Where-Object { $_ -notmatch '-' } | Select-Object -Last 1
if (-not $VpkVersion) { throw 'vpk の最新安定版バージョンの取得に失敗しました (NuGet API)' }
Write-Host "vpk 最新安定版: $VpkVersion"
$Bucket = 'vs2vsc-updates'
$BaseUrl = 'https://vs2vsc.kagayoi.com'
$AccountId = '10901bfadbf1005164774a7350082985'
$SecretsPath = 'D:\Dev\Secret\secrets.json'
# 署名者の CN。証明書の年次更新で thumbprint が変わっても同じ値で照合できる。
$CertSubjectName = 'Open Source Developer Yuichiro Shinozaki'

# VStoVSC は win-x64 のみ配信 (ARM64 配信なし)
$RuntimeMatrix = @{
    'win-x64' = @{ Channel = 'win' }
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot
$WorkDir = Join-Path $RepoRoot 'local-release'
$ArtifactsDir = Join-Path $WorkDir 'artifacts'

function Invoke-Native {
    param([string]$Description, [scriptblock]$Block)
    & $Block
    if ($LASTEXITCODE -ne 0) { throw "$Description が失敗しました (exit $LASTEXITCODE)" }
}

# ---- 0. プリフライト ----
Write-Host '== プリフライト ==' -ForegroundColor Cyan

# Git Bash (MSYS) 経由で起動すると括弧入り環境変数が落ちて vswhere.exe 解決等が
# 壊れることがあるため補完する (非 AOT でも実害なしの保険)
if (-not ${env:ProgramFiles(x86)}) { ${env:ProgramFiles(x86)} = 'C:\Program Files (x86)' }

# VS Installer ディレクトリを PATH に追加 (vswhere.exe 解決用、非 AOT でも実害なしの保険)
$vsInstallerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if ($env:PATH -notlike "*$vsInstallerDir*") { $env:PATH = "$env:PATH;$vsInstallerDir" }

# vpk (dotnet tool) は .NET 9 ランタイム要求だがローカルは 10 のみ → ロールフォワード
$env:DOTNET_ROLL_FORWARD = 'Major'

# XPath で取得 (member enumeration は Version を持たない PropertyGroup 混在時に StrictMode で throw する)
$versionNode = ([xml](Get-Content 'Directory.Build.props' -Raw)).SelectSingleNode('/Project/PropertyGroup/Version')
$version = if ($versionNode) { $versionNode.InnerText.Trim() } else { $null }
if (-not $version) { throw 'Directory.Build.props から <Version> を取得できませんでした' }
Write-Host "バージョン: $version"

# 署名の準備 (Sigilune の場所とクラウドへのログインを、ビルド前に確認して早く落とす)。
# 既存成果物で配信後処理だけを再開する -VerifyOnly では署名しないためログインしない。
. (Join-Path $PSScriptRoot 'Sigilune.Common.ps1')
if (-not $VerifyOnly) {
    $Sigilune = Resolve-SigiluneExe
    Connect-Sigilune -SigiluneExe $Sigilune
    # Velopack はファイルごとに別プロセスで署名テンプレートを実行する。Sigilune はログインを共有し、
    # 署名後に有効性・署名者・タイムスタンプを検証して失敗なら非0で終わる。
    $SignTemplate = "`"$Sigilune`" sign {{file}}"
}

# vpk を固定バージョンで用意
if (-not $VerifyOnly) {
$vpkInstalled = (dotnet tool list --global | Select-String -SimpleMatch 'vpk') -match [regex]::Escape($VpkVersion)
if (-not $vpkInstalled) {
    Write-Host "vpk $VpkVersion をインストールします..."
    dotnet tool uninstall --global vpk 2>$null | Out-Null
    Invoke-Native 'vpk のインストール' { dotnet tool install --global vpk --version $VpkVersion }
}
}

# Cloudflare トークン (アップロード時のみ必要)
if (-not $SkipUpload) {
    # CI では Environment "release" の Secret が環境変数で渡る。ローカルは secrets.json から必要なキーだけ読む。
    if (-not $env:CLOUDFLARE_API_TOKEN) {
        $secrets = Get-Content $SecretsPath -Raw | ConvertFrom-Json
        if (-not $secrets.cloudflare.api_token) { throw "secrets.json に cloudflare.api_token が見つかりません" }
        $env:CLOUDFLARE_API_TOKEN = $secrets.cloudflare.api_token
    }
    $env:CLOUDFLARE_ACCOUNT_ID = $AccountId
}

if ($VerifyOnly) {
    if ($SkipUpload) { throw '-VerifyOnly と -SkipUpload は併用できません' }
    $publishedManifest = Get-Content (Join-Path $ArtifactsDir 'releases.win.json') -Raw | ConvertFrom-Json
    if (@($publishedManifest.Assets).Count -eq 0 -or @($publishedManifest.Assets | Where-Object { $_.Version -ne $version }).Count -gt 0) {
        throw '既存成果物のバージョンが現在のリリースと一致しません'
    }
} else {
if (Test-Path $WorkDir) {  Remove-LocalWorkItem -LiteralPath ($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($WorkDir)) -AllowedRoot $RepoRoot }
New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

# ---- 1. ビルド + 署名付きパッケージング (RID ごと) ----
foreach ($runtime in $Runtimes) {
    $config = $RuntimeMatrix[$runtime]
    if (-not $config) { throw "未知の runtime: $runtime" }
    $publishDir = Join-Path $WorkDir "publish-$runtime"

    # restore と publish のパラメータを完全に一致させる (旧 CI の Codex P1 指摘 #3312152665 踏襲)。
    # csproj は <SelfContained>false</SelfContained> なので、publish 時のみ
    # --self-contained true で上書きするが、restore 時にも -p:SelfContained=true を
    # 渡しておかないと runtime packs が assets.json に入らず、--no-restore で
    # NETSDK1112 ("publish parameters are different than what's in the project file") が出る。
    Write-Host "== restore: $runtime ==" -ForegroundColor Cyan
    Invoke-Native "dotnet restore ($runtime)" {
        dotnet restore src/VStoVSC/VStoVSC.csproj -r $runtime --force-evaluate -p:SelfContained=true
    }

    # MSBuild.Locator はユーザー側 Visual Studio の MSBuild を動的解決するため
    # SelfContained でも問題なく動く (アプリ本体は self-contained / MSBuild は外部)
    Write-Host "== publish: $runtime ==" -ForegroundColor Cyan
    Invoke-Native "dotnet publish ($runtime)" {
        dotnet publish src/VStoVSC/VStoVSC.csproj -c Release -r $runtime `
            --self-contained true --no-restore -o $publishDir
    }

    if (-not (Test-Path (Join-Path $publishDir 'VStoVSC.exe'))) {
        throw "VStoVSC.exe が publish 出力にありません ($runtime)"
    }

    Write-Host "== vpk pack + 署名: $runtime ==" -ForegroundColor Cyan
    Invoke-Native "vpk pack ($runtime)" {
        vpk pack `
            --packId VStoVSC `
            --packVersion $version `
            --packTitle 'VStoVSC' `
            --packAuthors 'Kagayoi' `
            --mainExe VStoVSC.exe `
            --icon (Join-Path 'src' 'VStoVSC' 'icon' 'app.ico') `
            --packDir $publishDir `
            --outputDir $ArtifactsDir `
            --channel $config.Channel `
            --shortcuts 'StartMenuRoot,Desktop' `
            --signTemplate $SignTemplate
    }
}

# 署名検証 (Setup.exe が正しく署名されているかリリース前に確認)
Write-Host '== 署名検証 ==' -ForegroundColor Cyan
foreach ($exe in Get-ChildItem $ArtifactsDir -Filter '*.exe') {
    $sig = Get-AuthenticodeSignature $exe.FullName
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike "CN=$CertSubjectName*") {
        throw "署名検証失敗: $($exe.Name) → $($sig.Status)"
    }
    Write-Host "  ✅ $($exe.Name): Valid ($($sig.SignerCertificate.Subject -replace ',.*$'))"
}

if ($SkipUpload) {
    Write-Host "`n✅ -SkipUpload 指定のためここで終了。成果物: $ArtifactsDir" -ForegroundColor Green
    Get-ChildItem $ArtifactsDir | Format-Table Name, @{n='Size(MB)'; e={[math]::Round($_.Length/1MB,1)}}
    return
}

# ---- 2. R2 アップロード ----
# - releases.{channel}.json (manifest) は同 channel の旧版を上書き
# - *.nupkg は put のみ (過去版は cleanup ステップが manifest 基準で削除)
Write-Host '== R2 アップロード ==' -ForegroundColor Cyan
$uploaded = 0
$uploadFiles = @(Get-ChildItem $ArtifactsDir -File)
$ordered = @($uploadFiles | Where-Object { $_.Name -notmatch '^(?:releases\..*\.json|assets\..*\.json|RELEASES(?:-.*)?)$' }) +
    @($uploadFiles | Where-Object { $_.Name -match '^(?:releases\..*\.json|assets\..*\.json|RELEASES(?:-.*)?)$' })
foreach ($f in $ordered) {
    Write-Host "  ↑ $($f.Name)"
    $immutable = $f.Name -match '(?:\.nupkg$|[-_.]\d+\.\d+\.\d+(?:[-_.]|$))'
    $cacheControl = if ($immutable) { 'public, max-age=31536000, immutable' } else { 'no-cache, must-revalidate' }
    $contentType = switch -Regex ($f.Name) { '\.json$' { 'application/json'; break }; '^RELEASES' { 'text/plain'; break }; '\.(zip|nupkg)$' { 'application/zip'; break }; default { 'application/octet-stream' } }
    Publish-R2Object -AccountId $AccountId -Bucket $Bucket -FilePath $f.FullName -Immutable:$immutable -CacheControl $cacheControl -ContentType $contentType | Out-Null
    $uploaded++
}
Write-Host "✅ R2 アップロード完了: $uploaded ファイル"
}

# ---- 2.5 Cloudflare エッジキャッシュのパージ ----
# 固定名ファイル (Setup.exe / Portable.zip / RELEASES / releases.*.json / assets.*.json) は
# 毎リリースで中身が変わるのに URL が不変。CDN エッジが旧版を Cache-Control の max-age 分保持するため、
# パージしないと新規ダウンロード・自動更新が旧バージョンを掴む。アップロード直後に該当 URL をパージして
# キャッシュ削除を要求する。バージョン付き nupkg は URL が一意 (旧キャッシュなし) のためパージ不要。
Write-Host '== Cloudflare キャッシュパージ ==' -ForegroundColor Cyan
$cfHeaders = @{ Authorization = "Bearer $($env:CLOUDFLARE_API_TOKEN)" }
$zoneName = ([uri]$BaseUrl).Host -replace '^[^.]+\.', ''   # <sub>.kagayoi.com → kagayoi.com (apex)
$zoneResp = Invoke-RestMethod -Uri "https://api.cloudflare.com/client/v4/zones?name=$zoneName" -Headers $cfHeaders -TimeoutSec 30
if (-not $zoneResp.success -or @($zoneResp.result).Count -eq 0) { throw "Cloudflare zone '$zoneName' の取得に失敗しました" }
$zoneId = $zoneResp.result[0].id
$purgeUrls = @(Get-ChildItem $ArtifactsDir -File | Where-Object { $_.Name -notlike '*.nupkg' } | ForEach-Object { "$BaseUrl/$($_.Name)" })
if ($purgeUrls.Count -gt 0) {
    $purgeBody = [PSCustomObject]@{ files = @($purgeUrls) } | ConvertTo-Json -Compress
    $purgeResp = Invoke-RestMethod -Method Post -Uri "https://api.cloudflare.com/client/v4/zones/$zoneId/purge_cache" `
        -Headers $cfHeaders -ContentType 'application/json' -Body $purgeBody -TimeoutSec 30
    if (-not $purgeResp.success) { throw "Cloudflare キャッシュパージに失敗しました: $($purgeResp.errors | ConvertTo-Json -Compress)" }
    Write-Host "  ✅ パージ: $($purgeUrls.Count) URL"
    $purgeUrls | ForEach-Object { Write-Host "     $_" }
} else {
    Write-Host '  パージ対象なし'
}

# ---- 3. 旧バージョン nupkg のクリーンアップ (Aggressive 戦略) ----
# ローカル artifacts の manifest (= 今アップロードしたものと同一) から keep set を作り、
# R2 上の「.nupkg かつ manifest 外」だけを削除する。固定ファイル名 (Setup.exe /
# Portable.zip / RELEASES* / assets.*.json / releases.*.json) は対象外なので安全。
Write-Host '== 旧 nupkg クリーンアップ ==' -ForegroundColor Cyan
$keep = @{}
$manifests = Get-ChildItem $ArtifactsDir -Filter 'releases.*.json'
if (-not $manifests) { throw 'artifacts に releases.*.json が見つかりません' }
foreach ($m in $manifests) {
    foreach ($asset in (Get-Content $m.FullName -Raw | ConvertFrom-Json).Assets) {
        if ($asset.FileName) { $keep[$asset.FileName] = $true }
    }
}
$api = "https://api.cloudflare.com/client/v4/accounts/$AccountId/r2/buckets/$Bucket"
$headers = @{ Authorization = "Bearer $($env:CLOUDFLARE_API_TOKEN)" }

if ($VerifyOnly) {
    # 再開時のローカルmanifestは配信中とは限らないため、R2正本の参照先も清掃から保護する。
    $originResponse = Invoke-WebRequest -Uri "$api/objects/releases.win.json" -Headers $headers -TimeoutSec 30 -MaximumRedirection 0
    if ($originResponse.StatusCode -ne 200) { throw '配信中manifestを取得できないためcleanupを中止します' }
    $originContent = $originResponse.Content
    if ($originContent -is [byte[]]) { $originContent = [System.Text.Encoding]::UTF8.GetString($originContent) }
    $originManifest = $originContent | ConvertFrom-Json -ErrorAction Stop
    if (-not $originManifest -or -not $originManifest.PSObject.Properties['Assets'] -or
        $null -eq $originManifest.Assets -or @($originManifest.Assets).Count -eq 0) {
        throw '配信中manifestのAssetsが空または不正なためcleanupを中止します'
    }
    foreach ($asset in $originManifest.Assets) {
        $fileName = $asset.FileName
        if ($fileName -isnot [string] -or [string]::IsNullOrWhiteSpace($fileName) -or
            $fileName -in @('.', '..') -or $fileName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or
            [IO.Path]::GetFileName($fileName) -cne $fileName) {
            throw '配信中manifestのFileNameが不正なためcleanupを中止します'
        }
        $keep[$fileName] = $true
    }
}
Write-Host "  保持対象の参照ファイル: $($keep.Count) 件"

$allKeys = [System.Collections.Generic.List[string]]::new()
$cursor = ''
while ($true) {
    $uri = "$api/objects?per_page=1000" + $(if ($cursor) { "&cursor=$cursor" })
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 30
    foreach ($obj in $resp.result) { $allKeys.Add($obj.key) }
    # 全件 1 ページに収まると result_info が省略される (StrictMode 下では直接参照が throw)
    $info = $resp.PSObject.Properties['result_info']
    if (-not $info -or -not $info.Value) { break }
    $truncated = $info.Value.PSObject.Properties['is_truncated']
    if (-not $truncated -or -not $truncated.Value) { break }
    $cursorProp = $info.Value.PSObject.Properties['cursor']
    $cursor = if ($cursorProp) { $cursorProp.Value } else { '' }
    if (-not $cursor) { break }
}

# 全プロジェクト共通の保持ポリシー: 直近 2 バージョン。
# 旧実装は '*.nupkg' だけを削除対象にしていたため、バージョン付きの配布物
# (zip / deb / rpm / AppImage 等) が R2 に永久に溜まっていた (Ferry で 351 個 7.2GB)。
$KeepVersionCount = 2
$versionPattern = '(\d+\.\d+\.\d+)'
$allVersions = @(
    $allKeys | ForEach-Object {
        $m = [regex]::Match($_, $versionPattern)
        if ($m.Success) { $m.Groups[1].Value }
    } | Sort-Object -Property { [version]$_ } -Unique
)
$keepVersions = @($allVersions | Select-Object -Last $KeepVersionCount)
Write-Host "  保持バージョン: $($keepVersions -join ', ') (全 $($allVersions.Count) 世代)"

$toDelete = $allKeys | Where-Object {
    # manifest が参照するファイルは絶対保持 (消すと自動更新が壊れる)
    if ($keep.ContainsKey($_)) { return $false }
    # 固定ファイル名はバージョン文字列を含まない = 毎リリース上書きなので保持
    $m = [regex]::Match($_, $versionPattern)
    if (-not $m.Success) { return $false }
    return $keepVersions -notcontains $m.Groups[1].Value
}
if (-not $toDelete) {
    Write-Host '  ✅ 削除対象なし'
} else {
    $deleted = 0; $failed = 0
    foreach ($key in $toDelete) {
        $encoded = [uri]::EscapeDataString($key)
        try {
            Invoke-RestMethod -Method Delete -Uri "$api/objects/$encoded" -Headers $headers -TimeoutSec 30 | Out-Null
            Write-Host "  🗑️  $key"
            $deleted++
        } catch {
            Write-Warning "  削除失敗: $key — $($_.Exception.Message)"
            $failed++
        }
    }
    Write-Host "  🧹 クリーンアップ: $deleted 削除 / $failed 失敗"
    # 全件失敗は token 権限等の異常なので fail (一部失敗は次回リリースで再試行される)
    if ($failed -gt 0 -and $deleted -eq 0) { throw '旧 nupkg の削除がすべて失敗しました。API token の権限を確認してください。' }
}

Write-Host "`n🎉 リリース完了: v$version → $BaseUrl" -ForegroundColor Green
