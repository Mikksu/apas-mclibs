
namespace APAS.MotionLib.ZMC.Configuration
{
    public class AnalogInParamConfig
    {
        /// <summary>
        /// AIN通道数，注意该通道起始值为0，而非ZMIO模块的实际通道数。
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// 电压测量上限，单位mV
        /// </summary>
        public double RangeUpperMv { get; set; }

        /// <summary>
        /// 电压测量下限，单位mV
        /// </summary>
        public double RangeLowMv { get; set; }

        /// <summary>
        /// ADC最大刻度值
        /// </summary>
        public double MaxScale { get; set; }
    }
}
