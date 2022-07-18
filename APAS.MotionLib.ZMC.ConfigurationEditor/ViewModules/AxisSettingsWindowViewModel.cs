using System.Collections.Generic;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
using DevExpress.Mvvm.CodeGenerators;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.ViewModules
{
    [GenerateViewModel]
    partial class AxisSettingsWindowViewModel
    {
        #region Variables

        /// <summary>
        /// 轴配置列表。
        /// </summary>
        [GenerateProperty(SetterAccessModifier = AccessModifier.Private)]
        private List<AxisSettings> _settings;

        /// <summary>
        /// 最大轴数。
        /// </summary>
        [GenerateProperty(SetterAccessModifier = AccessModifier.Private)]
        private int _maxAxis;

        #endregion

        #region Constructors

        public AxisSettingsWindowViewModel() : this(12)
        {
            
        }

        public AxisSettingsWindowViewModel(int maxAxis)
        {
            _maxAxis = maxAxis;

            _settings = new List<AxisSettings>(_maxAxis);
            for(int i = 0; i < _maxAxis; i++)
                _settings.Add(new AxisSettings(i));
        }

        #endregion
    }
}
