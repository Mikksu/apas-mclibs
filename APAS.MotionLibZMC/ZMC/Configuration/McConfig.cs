namespace APAS.MotionLib.ZMC.Configuration
{
    public class McConfig
    {
        /// <summary>
        /// 示波器参数。
        /// </summary>
        public ScopeConfig Scope { get; set; }

        /// <summary>
        /// 轴参数。
        /// </summary>
        public AxisConfig[] Axes { get; set; }

        /// <summary>
        /// 模拟输入设置
        /// </summary>
        public AnalogInConfig Ain { get; set; }
    }
}
