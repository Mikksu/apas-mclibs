using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
using APAS.MotionLib.ZMC.ConfigurationEditor.ViewModules;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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

            Ioc.Default.ConfigureServices(new ServiceCollection().AddTransient<AxisSettingsWindowViewModel>()
                .BuildServiceProvider());
        }
    }


}
