using System;
using System.IO;
using System.Net;
using System.Windows;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
using APAS.MotionLib.ZMC.ConfigurationEditor.Services;
using CommunityToolkit.Mvvm.Input;
using DevExpress.Mvvm.CodeGenerators;
using MvvmDialogs;
using MvvmDialogs.FrameworkDialogs.OpenFile;
using MvvmDialogs.FrameworkDialogs.SaveFile;
using Newtonsoft.Json;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.ViewModules
{
    [GenerateViewModel]
    partial class MainWindowViewModel
    {
        #region Variables

        private readonly IDialogService _dialogService;
        private readonly CreateApasZmcConfigFileService _apasService;


        /// <summary>
        /// 轴配置列表。
        /// </summary>
        [GenerateProperty(SetterAccessModifier = AccessModifier.Private)]
        private AxisSettingsCollection _settings;

        /// <summary>
        /// 最大轴数。
        /// </summary>
        [GenerateProperty(SetterAccessModifier = AccessModifier.Private)]
        private int _maxAxis;

        /// <summary>
        /// 控制器IP地址。
        /// </summary>
        [GenerateProperty]
        private string _ipAddress;

        /// <summary>
        /// 是否已连接到控制器。
        /// </summary>
        [GenerateProperty(SetterAccessModifier = AccessModifier.Private)]
        private bool _isConnected;

        /// <summary>
        /// ZMC轴卡对象。
        /// </summary>
        private Zmc4Series _zmc4;

        #endregion

        #region Constructors

        public MainWindowViewModel() : this(null, null)
        {
            
        }

        public MainWindowViewModel(IDialogService dialogService, CreateApasZmcConfigFileService apasService) : this(12, dialogService, apasService)
        {
            
        }

        public MainWindowViewModel(int maxAxis, IDialogService dialogService, CreateApasZmcConfigFileService apasService)
        {
            _maxAxis = maxAxis;
            _dialogService = dialogService;
            _apasService = apasService;

            _settings = new AxisSettingsCollection(_maxAxis);
            for(var i = 0; i < _maxAxis; i++)
                _settings.Add(new AxisSettings(i));

            _ipAddress = "192.168.0.11";
        }

        #endregion


        #region Command

        /// <summary>
        /// 从Json文件导入。
        /// </summary>
        public RelayCommand ReadProjectCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
                        var dialogSettings = new OpenFileDialogSettings
                        {
                            Filter = "JSON|*.json",
                            Title = "打开配置文件",
                            Multiselect = false
                        };

                        var ret = _dialogService?.ShowOpenFileDialog(this, dialogSettings);
                        if (!ret.HasValue || !ret.Value) 
                            return;

                        // 导入Json文件
                        var fileName = dialogSettings.FileName;
                        var json = File.ReadAllText(fileName);
                        Settings = JsonConvert.DeserializeObject<AxisSettingsCollection>(json);
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法打开工程，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
        }


        /// <summary>
        /// 导出为Json文件。
        /// </summary>
        public RelayCommand SaveProjectCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
                        var settings = new SaveFileDialogSettings();
                        settings.Filter = "JSON|*.json";
                        settings.Title = "保存配置";
                        var ret = _dialogService?.ShowSaveFileDialog(this, settings);
                        if (ret.HasValue && ret.Value)
                        {
                            var str = JsonConvert.SerializeObject(Settings);
                            File.WriteAllText(settings.FileName, str);
                        }
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法保存工程，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 导入APAS配置文件
        /// </summary>
        public RelayCommand ImportFromApasConfigFile
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {


                        var dialogSettings = new OpenFileDialogSettings
                        {
                            Filter = "JSON|*.json",
                            Title = "打开APAS配置文件",
                            Multiselect = false
                        };

                        var ret = _dialogService?.ShowOpenFileDialog(this, dialogSettings);
                        if (!ret.HasValue || !ret.Value)
                            return;

                        // 导入Json文件
                        var fileName = dialogSettings.FileName;
                        var settings = _apasService.ImportApasZmcConfigJsonFile(fileName);
                        Settings = settings;
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法导入APAS配置，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 导出APAS配置文件
        /// </summary>
        public RelayCommand ExportToApasConfigFile
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
                        var settings = new SaveFileDialogSettings
                        {
                            Filter = "JSON|*.json",
                            Title = "保存APAS配置文件"
                        };
                        var ret = _dialogService?.ShowSaveFileDialog(this, settings);
                        if (ret.HasValue && ret.Value)
                        {
                            _apasService.CreateApasZmcConfigJsonFile(Settings, settings.FileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法导出APAS配置，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
        }

        /// <summary>
        /// 连接到控制器
        /// </summary>
        public RelayCommand ConnectCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(IpAddress) || IPAddress.TryParse(IpAddress, out var ip) == false)
                            throw new InvalidCastException("输入的IP地址格式错误。");

#if !IGNORE_ZMC
                        _zmc4 = new Zmc4Series(IpAddress, -1, "Zmc4SeriesConf.json");
                        _zmc4.Init();
#endif
                        IsConnected = true;

                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法连接到控制器，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }

                });
            }
        }

        public RelayCommand DisconnectCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
#if !IGNORE_ZMC
                        _zmc4?.Dispose();
#endif
                        IsConnected = false;

                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法连接到控制器，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                    finally
                    {
                        _zmc4 = null;
                    }

                });
            }
        }

        public RelayCommand UpdateConfigCommand
        {
            get
            {
                return new RelayCommand(() =>
                {
                    try
                    {
#if !IGNORE_ZMC
                        // 如果ZMC已经打开，先关闭
                        if (_zmc4 is { IsInitialized: true })
                            _zmc4.Dispose();
#endif
                        // 生成新的配置文件
                        _apasService?.CreateApasZmcConfigJsonFile(_settings, "Zmc4SeriesConf.json");

#if !IGNORE_ZMC
                        // 重新打开ZMC
                        _zmc4 = new Zmc4Series(IpAddress, -1, "Zmc4SeriesConf.json");
                        _zmc4.Init();
#endif
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowMessageBox(this, $"无法连接到控制器，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        IsConnected = false;
                    }
                });
            }
        }

        #endregion

    }
}
