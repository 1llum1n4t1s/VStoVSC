using System.Windows;

namespace VS_to_VSC
{
    /// <summary>
    /// MainWindowのロジックを管理するクラス
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // ログに初期メッセージを表示
            LogMessage("Visual Studio to VSCode Converter が起動しました。");
        }

        /// <summary>
        /// ファイルパステキストボックスのキーダウンイベントハンドラ
        /// </summary>
        private void FilePathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Enterキーが押された場合、変換処理を開始
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var solutionPath = FilePathTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(solutionPath))
                {
                    StartConversion(solutionPath);
                }
            }
        }

        /// <summary>
        /// 参照ボタンのクリックイベントハンドラ
        /// </summary>
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ファイル選択ダイアログを作成
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Visual Studioソリューションファイルを選択してください",
                    Filter = "ソリューションファイル (*.sln;*.slnx)|*.sln;*.slnx|すべてのファイル (*.*)|*.*",
                    DefaultExt = "sln"
                };

                // ダイアログを表示
                if (openFileDialog.ShowDialog() == true)
                {
                    // 選択されたファイルパスをテキストボックスに設定
                    FilePathTextBox.Text = openFileDialog.FileName;
                    LogMessage($"ファイルが選択されました: {openFileDialog.FileName}");

                    // ファイル選択後、自動的に変換処理を開始
                    LogMessage("自動変換を開始します...");
                    StartConversion(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"ファイル選択中にエラーが発生しました。\n\nエラー内容: {ex.Message}";
                LogMessage($"ファイル選択エラー: {ex.Message}");
                MessageBox.Show(errorMessage, "ファイル選択エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 変換処理を開始する
        /// </summary>
        /// <param name="solutionPath">ソリューションファイルのパス</param>
        private void StartConversion(string solutionPath)
        {
            try
            {
                // ファイルパスの検証
                if (!ValidateSolutionPath(solutionPath))
                {
                    return;
                }

                LogMessage("変換処理を開始します...");

                // VSCodeGeneratorクラスを使用してVSCode設定ファイルを生成
                var generator = new VSCodeGenerator(LogMessage);
                generator.GenerateVSCodeFiles(solutionPath);

                LogMessage("変換が完了しました。");

                // 成功メッセージをダイアログで表示
                var successMessage = $"変換が完了しました。\n\nソリューションファイル: {System.IO.Path.GetFileName(solutionPath)}\n\n.vscodeフォルダに設定ファイルが生成されました。";
                MessageBox.Show(successMessage, "変換完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var errorMessage = $"変換処理中にエラーが発生しました。\n\nエラー内容: {ex.Message}\n\n詳細はログを確認してください。";
                LogMessage($"変換エラー: {ex.Message}");
                MessageBox.Show(errorMessage, "変換エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ソリューションパスの検証を行う
        /// </summary>
        /// <param name="solutionPath">検証するソリューションパス</param>
        /// <returns>検証結果（true: 有効、false: 無効）</returns>
        private bool ValidateSolutionPath(string solutionPath)
        {
            if (string.IsNullOrEmpty(solutionPath))
            {
                var errorMessage = "ファイルパスが入力されていません。\n\nソリューションファイルのパスを入力してください。";
                LogMessage("エラー: ファイルパスが入力されていません。");
                MessageBox.Show(errorMessage, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!System.IO.File.Exists(solutionPath))
            {
                var errorMessage = $"指定されたファイルが存在しません。\n\nパス: {solutionPath}\n\n正しいファイルパスを確認してください。";
                LogMessage("エラー: 指定されたファイルが存在しません。");
                MessageBox.Show(errorMessage, "ファイル不存在エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // ソリューションファイルの拡張子をチェック
            var extension = System.IO.Path.GetExtension(solutionPath).ToLower();
            if (extension != ".sln" && extension != ".slnx")
            {
                var errorMessage = $"サポートされていないファイル形式です。\n\nファイル: {System.IO.Path.GetFileName(solutionPath)}\n拡張子: {extension}\n\n.slnまたは.slnxファイルを選択してください。";
                LogMessage("エラー: サポートされていないファイル形式です。.slnまたは.slnxファイルを選択してください。");
                MessageBox.Show(errorMessage, "ファイル形式エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// ログメッセージを表示する
        /// </summary>
        /// <param name="message">表示するメッセージ</param>
        public void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            // UIスレッドでログを更新
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(logMessage + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }
    }
}