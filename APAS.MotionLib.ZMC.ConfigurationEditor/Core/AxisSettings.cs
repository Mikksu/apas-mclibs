using System;
using System.ComponentModel.DataAnnotations;
using DevExpress.Mvvm.CodeGenerators;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Core
{
    [GenerateViewModel]
    partial class AxisSettings
    {

        #region Constructors

        public AxisSettings(int axisId)
        {
            if (axisId is < 0 or > 11)
                throw new ArgumentOutOfRangeException(nameof(axisId), "轴号必须为1~12。");

            _axisIndex = axisId;

            _homeSensor = HomeSensorSelectionSource.负限位;
            _diNel = (DiSource)(axisId + 24);
            _diPel = (DiSource)(axisId + 0);

            _homeMode = HomeModeSource.负方向;
            _homeAcc = 3000000;
            _homeDec = 3000000;
            _homeHiSpeed = 100000;
            _homeCreepSpeed = 10000;

            _invertSteps = PulsePolaritySource.正向;
            _driveAcc = 3000000;
            _driveDec = 3000000;
            _driveFastDec = 5000000;
            _sRampDuration = 100;
            _driveSpeed = 300000;

        }

        #endregion

        /// <summary>
        /// 轴号
        /// </summary>
        [GenerateProperty]
        [Display(Order = 1, Name = "轴号", GroupName = "Common")]
        private int _axisIndex;


        #region Hardware Connection

        /// <summary>
        /// 原点信号使用的数字输入。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 2, Name = "Home位置", GroupName = "Common")]
        private HomeSensorSelectionSource _homeSensor;

        /// <summary>
        /// 负限位信号使用的数字输入。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 4, Name = "负限位输入", GroupName = "Common")] 
        private DiSource _diNel;

        /// <summary>
        /// 是否翻转负限位信号电平。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 5, Name = "负限位输入反向", GroupName = "Common")] 
        private bool _isInverseNel;

        /// <summary>
        /// 正限位信号使用的数字输入。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 6, Name = "正限位输入", GroupName = "Common")] 
        private DiSource _diPel;

        /// <summary>
        /// 是否翻转正限位信号电平。
        /// </summary>
        [GenerateProperty] 
        [Display(Order = 7, Name = "正限位输入反向", GroupName = "Common")] 
        private bool _isInversePel;

        #endregion

        #region Home Settings

        /// <summary>
        /// Home模式
        /// </summary>
        [GenerateProperty] 
        [Display(Order = 8, Name = "回原点模式", GroupName = "Common")] 
        private HomeModeSource _homeMode;

        /// <summary>
        /// Home时的加速度。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 9, Name = "回原点加速度", GroupName = "Common")] 
        private int _homeAcc;

        /// <summary>
        /// Home时的减速度。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 10, Name = "回原点减速度", GroupName = "Common")] 
        private int _homeDec;

        /// <summary>
        /// Home高速阶段的速度。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 11, Name = "回原点高速速度", GroupName = "Common")] 
        private int _homeHiSpeed;

        /// <summary>
        /// Home爬行阶段的速度。
        /// </summary>
        [GenerateProperty] 
        [Display(Order = 12, Name = "回原点爬行速度", GroupName = "Common")] 
        private int _homeCreepSpeed;

        #endregion

        #region Motion

        /// <summary>
        /// 脉冲模式设置。
        /// </summary>
        [GenerateProperty]
        [Display(Order = 14, Name = "脉冲方向", GroupName = "Common")]
        private PulsePolaritySource _invertSteps;

        /// <summary>
        /// 移动加速度。
        /// </summary>
        [GenerateProperty]
        private int _driveAcc;

        /// <summary>
        /// 移动减速度。
        /// </summary>
        [GenerateProperty]
        private int _driveDec;

        /// <summary>
        /// 急停减速度。
        /// </summary>
        [GenerateProperty]
        private int _driveFastDec;

        /// <summary>
        /// S曲线jerk时间。
        /// </summary>
        [GenerateProperty]
        private int _sRampDuration;

        /// <summary>
        /// 移动速度。
        /// </summary>
        [GenerateProperty]
        private int _driveSpeed;
        
        #endregion
    }
}
