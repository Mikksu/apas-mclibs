using System;
using System.IO;
using System.Windows;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
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

        #endregion

        #region Constructors

        public MainWindowViewModel() : this(null)
        {
            
        }

        public MainWindowViewModel(IDialogService dialogService) : this(12, dialogService)
        {
            
        }

        public MainWindowViewModel(int maxAxis, IDialogService dialogService)
        {
            _maxAxis = maxAxis;
            _dialogService = dialogService;

            _settings = new AxisSettingsCollection(_maxAxis);
            for(var i = 0; i < _maxAxis; i++)
                _settings.Add(new AxisSettings(i));
        }

        #endregion


        #region Command

        /// <summary>
        /// 从Json文件导入。
        /// </summary>
        public RelayCommand ImportConfigurationFromJson
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
                        _dialogService?.ShowMessageBox(this, $"无法导入配置，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                });
            }
        }


        /// <summary>
        /// 导出为Json文件。
        /// </summary>
        public RelayCommand ExportConfigurationToJson
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
                        _dialogService?.ShowMessageBox(this, $"无法导出配置，{ex.Message}", "错误", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                   
                    
                });
            }
        }


        #endregion

    }
}
