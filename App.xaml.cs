using System.Windows;
using Velopack;

namespace VS_to_VSC
{
    /// <summary>
    /// App.xamlのロジックを管理するクラス
    /// </summary>
    public partial class App : Application
    {
        private readonly UpdateService _updateService = new();

        /// <summary>
        /// アプリ起動時の処理
        /// </summary>
        protected override async void OnStartup(StartupEventArgs e)
        {
            VelopackApp.Build().Run();

            base.OnStartup(e);

            await _updateService.TryUpdateAsync();
        }
    }
}
