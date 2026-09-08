# VStoVSC の設計

## 目的と境界

Windows 上で Visual Studio の `.sln` / `.slnx` を読み、ソリューションと同じディレクトリに VS Code のビルド・デバッグ設定を生成する Avalonia デスクトップアプリ。変換処理はローカルで完結し、アプリ自身が対象ソリューションをビルドするのではなく、検出した MSBuild を実行するタスクを出力する。利用方法は [README.md](README.md)、開発・検証手順は [AGENTS.md](AGENTS.md) を参照する。

## 主要コンポーネント

| コンポーネント | 責務 |
| --- | --- |
| `Program` / `App` | Velopack を UI より先に初期化し、ウィンドウ・テーマ・ロケール・更新ダイアログを管理する。Windows のショートカット移行も起動経路に含む。 |
| `View/MainWindow` / `MainWindowViewModel` | ドロップとファイル選択を変換要求へ統合し、入力検証・再入防止・確認と結果表示を担う。破棄時に静的更新イベントを解除する。 |
| `IFilePickerService` / `FilePickerService` | Avalonia の StorageProvider を介して単一のローカルソリューションを選ぶ。キャンセルは null、ローカルパスを取得できない選択は例外になる。 |
| `Util/VSCodeGenerator` | MSBuild 検出、ソリューション解析、プロジェクト評価、JSON 生成とファイル反映を担う。結果は `VSCodeGenerationResult` の書き込み状態・警告で返す。 |
| `Settings` / `SettingsManager` / `Logger` | `%LOCALAPPDATA%/VStoVSC` の設定とログを扱う。設定は排他付き変更とスナップショット読み取り、保存は一時ファイルからの置換を使う。 |
| `UpdateChecker` / `App.Check4Update` | 更新マネージャーを組み立てる。UI の更新経路は共通の Velopack 更新ダイアログへ委譲し、多重確認を抑止する。 |
| `../vps-web/lp/vs2vsc/` / `scripts/release-local.ps1` | Web は案内 HTML と更新配信の振り分け、スクリプトは署名付き配布物の作成・R2 公開・照合を担う。 |

## 変換のデータフロー

1. ViewModel が存在と拡張子を検証し、Interlocked によって一度に一件だけ変換する。
2. Generator が MSBuild 実行ファイルの存在を確認する。既存 `.vscode` の置換確認とローカライズ文字列取得は呼び出し元で済ませ、解析と書き込みは `Task.Run` へ移す。
3. `.sln` に同名 `.slnx` がなければ `dotnet sln <対象> migrate` を一度試行する。待機は 60 秒で打ち切り、生成されなければ元の `.sln` を解析する。元ファイルは保持する。
4. Debug / Release ビルド、Clean、Rebuild の四タスクを生成する。`Exe` / `WinExe` のプロジェクトについて Debug 用の起動構成を生成する。
5. 同階層の一時ディレクトリで生成を終えてから `.vscode` へ反映する。警告付き成功と例外を ViewModel が区別して表示する。

## 不変条件と採用済みの判断

- **既存設定の保護**：保持を選んだ場合は tasks.json を更新し、既存 launch.json と他のファイルを残す。置換の場合は旧ディレクトリを一時移動して新ディレクトリを反映し、反映失敗時に元へ戻す。生成前半の失敗から既存設定を守る設計であり、保持モードの複数ファイル反映は一括トランザクションではない。
- **部分成功の明示**：実行可能プロジェクトがない場合などは tasks.json を生成しつつ警告を返す。旧ディレクトリの後始末失敗も残存パスを警告する。
- **実際の出力先を優先**：MSBuild の Debug 評価で OutputType、AssemblyName、TargetDir、TargetFramework を取得する。複数 TFM は先頭を選び、必要ならその TFM で再評価する。失敗時は XML と既定の出力レイアウトへフォールバックするため、SDK 不足でも生成を試せる一方、パスの正確性には限界がある。評価成功・失敗は一変換内でキャッシュし、次の変換で破棄する。
- **インストール済み MSBuild の利用**：Locator と実環境の MSBuild を使い、Microsoft.Build の runtime 同梱と Native AOT を避ける。アプリの自己完結配布だけでは、変換対象を扱うビルド環境までは提供しない。
- **更新元の固定**：Settings の更新 base URL は `https://vs2vsc.kagayoi.com` 固定で JSON 設定から差し替えられない。既定チャンネルは `win`。開発実行ではインストール状態の確認により更新をスキップする。
- **案内と更新ファイルの分離**：Worker は `/` と `/index.html` に HTML を返し、その他は元のリクエストを `fetch(request)` へ委譲する。新旧ドメインのルートを設定し、更新ファイルのレスポンスを加工しない。
- **ローカル署名と独立した Web 公開**：SimplySign の対話的な接続を必要とするため、アプリ配布はローカルスクリプトで行い、Web のみ GitHub Actions から公開する。配信照合は単一パート ETag の MD5 とサイズを使い、不明な ETag とバージョン付き配布物の不一致は停止する。`-VerifyOnly` は既存成果物のバージョンを確認して公開後処理を再開する。

## 製品ページの配信先

製品ページの配信HTMLは `../vps-web/lp/vs2vsc/`（編集元は `../vps-web/tools/lp/templates/`）、公開実体はVPSの `/srv/www/lp/vs2vsc/`。
Cloudflare側の中継設定は `../vps-web/deploy/lp-gateways/vs2vsc/` に置く。
公開URLと既存のR2・ライセンス通信を維持し、配信は `vps-web/deploy/deploy-lp.ps1` へ統一する。
