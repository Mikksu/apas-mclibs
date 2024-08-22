
namespace APAS.MotionLib.ZMC.Configuration
{
	public class AxisConfig
	{
		/// <summary>
		/// 系统控制参数。
		/// </summary>
        public ControlConfig Control { get; set; } = new ControlConfig();

		/// <summary>
		/// 数字输入输出配置。
		/// </summary>
        public IoConfig Io { get; set; } = new IoConfig();

		/// <summary>
		/// 运动参数控制。
		/// </summary>
        public MotionConfig Motion { get; set; } = new MotionConfig();

		/// <summary>
		/// 回零参数
		/// </summary>
		public HomingConfig Home { get; set; } = new HomingConfig();
	}
}
