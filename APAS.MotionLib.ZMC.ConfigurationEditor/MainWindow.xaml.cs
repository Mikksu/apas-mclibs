using System.Windows;
using APAS.MotionLib.ZMC.ConfigurationEditor.ViewModules;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace APAS.MotionLib.ZMC.ConfigurationEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = Ioc.Default.GetRequiredService<MainWindowViewModel>();
        }
    }
}
