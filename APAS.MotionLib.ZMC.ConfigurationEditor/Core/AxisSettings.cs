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
            _axisIndex = axisId;
        }

        #endregion

        /// <summary>
        /// 轴号
        /// </summary>
        [GenerateProperty]
        [Display(Name = "轴号", GroupName = "Common")]
        private int _axisIndex;


        #region Hardware Connection

        /// <summary>
        /// 原点信号使用的数字输入。
        /// </summary>
        [GenerateProperty] 
        private int _diOrigin;

        /// <summary>
        /// 是否翻转原点信号电平。
        /// </summary>
        [GenerateProperty] private bool _isInverseOrigin;

        /// <summary>
        /// 负限位信号使用的数字输入。
        /// </summary>
        [GenerateProperty] private int _diNel;

        /// <summary>
        /// 是否翻转负限位信号电平。
        /// </summary>
        [GenerateProperty] private bool _isInverseNel;

        /// <summary>
        /// 正限位信号使用的数字输入。
        /// </summary>
        [GenerateProperty] private int _diPel;

        /// <summary>
        /// 是否翻转正限位信号电平。
        /// </summary>
        [GenerateProperty] private bool _isInversePel;

        #endregion

        #region Home Settings

        /// <summary>
        /// Home模式
        /// </summary>
        [GenerateProperty] private int _homeMode;

        /// <summary>
        /// Home时的加速度。
        /// </summary>
        [GenerateProperty] private double _homeAcc;

        /// <summary>
        /// Home时的减速度。
        /// </summary>
        [GenerateProperty] private double _homeDec;

        /// <summary>
        /// Home高速阶段的速度。
        /// </summary>
        [GenerateProperty] private double _homeHiSpeed;

        /// <summary>
        /// Home爬行阶段的速度。
        /// </summary>
        [GenerateProperty] private double _homeCreepSpeed;

        #endregion

        #region Motion

        /// <summary>
        /// 轴类型。
        /// </summary>
        [GenerateProperty]
        private int _axisType;

        /// <summary>
        /// 脉冲模式设置。
        /// </summary>
        [GenerateProperty]
        private int _invertSteps;


        /// <summary>
        /// 脉冲当量。
        /// </summary>
        [GenerateProperty]
        private int _unit;

        #endregion

    }
}
