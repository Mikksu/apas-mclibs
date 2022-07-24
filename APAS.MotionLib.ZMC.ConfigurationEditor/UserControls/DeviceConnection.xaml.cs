using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.UserControls
{
    /// <summary>
    /// Interaction logic for DeviceConnection.xaml
    /// </summary>
    public partial class DeviceConnection : UserControl
    {
        public DeviceConnection()
        {
            InitializeComponent();
        }

        #region Command

        public static readonly DependencyProperty IpAddressProperty = DependencyProperty.Register(
            "IpAddress", typeof(string), typeof(DeviceConnection), new PropertyMetadata(default(string)));

        public string IpAddress
        {
            get => (string)GetValue(IpAddressProperty);
            set => SetValue(IpAddressProperty, value);
        }

        public static readonly DependencyProperty ConnectCommandProperty = DependencyProperty.Register(
            "ConnectCommand", typeof(ICommand), typeof(DeviceConnection), new PropertyMetadata(default(ICommand)));

        public ICommand ConnectCommand
        {
            get => (ICommand)GetValue(ConnectCommandProperty);
            set => SetValue(ConnectCommandProperty, value);
        }


        public static readonly DependencyProperty DisconnectCommandProperty = DependencyProperty.Register(
            "DisconnectCommand", typeof(ICommand), typeof(DeviceConnection), new PropertyMetadata(default(ICommand)));

        public ICommand DisconnectCommand
        {
            get => (ICommand)GetValue(DisconnectCommandProperty);
            set => SetValue(DisconnectCommandProperty, value);
        }


        public static readonly DependencyProperty UpdateConfigCommandProperty = DependencyProperty.Register(
            "UpdateConfigCommand", typeof(ICommand), typeof(DeviceConnection), new PropertyMetadata(default(ICommand)));

        public ICommand UpdateConfigCommand
        {
            get => (ICommand)GetValue(UpdateConfigCommandProperty);
            set => SetValue(UpdateConfigCommandProperty, value);
        }

        public static readonly DependencyProperty IsConnectedProperty = DependencyProperty.Register(
            "IsConnected", typeof(bool), typeof(DeviceConnection), new PropertyMetadata(default(bool)));

        public bool IsConnected
        {
            get => (bool)GetValue(IsConnectedProperty);
            set => SetValue(IsConnectedProperty, value);
        }

        #endregion
    }
}
