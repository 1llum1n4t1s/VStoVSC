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
        }

        /// <summary>
        /// ドロップエリアがクリックされた時のイベントハンドラ
        /// </summary>
        private void DropArea_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 左クリックの場合のみ処理
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                OpenFileSelection();
            }
        }

        /// <summary>
        /// ファイル選択ダイアログを表示して変換を開始する
        /// </summary>
        private void OpenFileSelection()
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
                    // ファイル選択後、自動的に変換処理を開始
                    StartConversion(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"ファイル選択中にエラーが発生しました。\n\nエラー内容: {ex.Message}";
                MessageBox.Show(errorMessage, "ファイル選択エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// ドロップエリアへのドロップイベントハンドラ
        /// </summary>
        private void DropArea_Drop(object sender, DragEventArgs e)
        {
            ResetDropAreaStyle();

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    StartConversion(files[0]);
                }
            }
        }

        /// <summary>
        /// ドロップエリアへのドラッグオーバーイベントハンドラ
        /// </summary>
        private void DropArea_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                // ドラッグオーバー時に枠線を強調
                DropArea.BorderBrush = SystemColors.HighlightBrush;
                DropArea.BorderThickness = new Thickness(3);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// ドロップエリアからのドラッグリーブイベントハンドラ
        /// </summary>
        private void DropArea_DragLeave(object sender, DragEventArgs e)
        {
            ResetDropAreaStyle();
        }

        /// <summary>
        /// ドロップエリアのスタイルを初期状態に戻す
        /// </summary>
        private void ResetDropAreaStyle()
        {
            // 個別指定を解除し、テーマの設定（XAMLの定義）に従うようにする
            DropArea.ClearValue(BackgroundProperty);
            DropArea.ClearValue(BorderBrushProperty);
            DropArea.ClearValue(BorderThicknessProperty);
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

                // VSCodeGeneratorクラスを使用してVSCode設定ファイルを生成
                // ログ出力は行わない
                var generator = new VSCodeGenerator(_ => { });
                generator.GenerateVSCodeFiles(solutionPath);

                // 成功メッセージをダイアログで表示
                var successMessage = $"変換が完了しました。\n\nソリューションファイル: {System.IO.Path.GetFileName(solutionPath)}\n\n.vscodeフォルダに設定ファイルが生成されました。";
                MessageBox.Show(successMessage, "変換完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                var errorMessage = $"変換処理中にエラーが発生しました。\n\nエラー内容: {ex.Message}";
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
                MessageBox.Show(errorMessage, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!System.IO.File.Exists(solutionPath))
            {
                var errorMessage = $"指定されたファイルが存在しません。\n\nパス: {solutionPath}\n\n正しいファイルパスを確認してください。";
                MessageBox.Show(errorMessage, "ファイル不存在エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // ソリューションファイルの拡張子をチェック
            var extension = System.IO.Path.GetExtension(solutionPath).ToLower();
            if (extension != ".sln" && extension != ".slnx")
            {
                var errorMessage = $"サポートされていないファイル形式です。\n\nファイル: {System.IO.Path.GetFileName(solutionPath)}\n拡張子: {extension}\n\n.slnまたは.slnxファイルを選択してください。";
                MessageBox.Show(errorMessage, "ファイル形式エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }
    }
}
