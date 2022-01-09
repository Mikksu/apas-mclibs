namespace APAS.MotionLib.ZMC.Configuration
{
	public class McConfig
	{
		
		public ScopeConfig Scope { get; set; }

		public AxisConfig[] Axes { get; set; }

		/// <summary>
		/// 模拟输入设置
		/// </summary>
		public AnalogInConfig Ain { get; set; }
	}
}
