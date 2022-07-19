namespace APAS.MotionLib.ZMC.Configuration
{
    public class MotionConfig
    {
        /// <summary>
        /// 移动时的加速度。
        /// </summary>
        public double Acc { get; set; } = 3000000;

        /// <summary>
        /// 移动时的减速度。
        /// </summary>
        public double Dec { get; set; } = 3000000;

        /// <summary>
        /// 急停时的减速度。
        /// </summary>
        public double FastDec { get; set; } = 5000000;

        /// <summary>
        /// 最大移动速度。
        /// </summary>
        public double Speed { get; set; } = 300000;

        /// <summary>
        /// S曲线加加速时间。
        /// </summary>
        public double SRampMs { get; set; } = 100;
    }
}
