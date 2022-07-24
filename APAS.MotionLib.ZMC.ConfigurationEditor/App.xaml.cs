using System.Windows;
using APAS.MotionLib.ZMC.ConfigurationEditor.Services;
using APAS.MotionLib.ZMC.ConfigurationEditor.ViewModules;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using MvvmDialogs;

namespace APAS.MotionLib.ZMC.ConfigurationEditor
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Ioc.Default.ConfigureServices(
                new ServiceCollection().AddSingleton<IDialogService, DialogService>()
                    .AddSingleton<CreateApasZmcConfigFileService>()
                    .AddTransient<MainWindowViewModel>()
                    .BuildServiceProvider());
        }
    }


}
