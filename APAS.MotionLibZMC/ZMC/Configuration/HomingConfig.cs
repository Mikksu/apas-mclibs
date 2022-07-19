namespace APAS.MotionLib.ZMC.Configuration
{
	public class HomingConfig
	{
		/// <summary>
		/// 回零模式，默认16，负方向脉冲+编码器，Z向信号回零
		/// 回零模式，14，负方向脉冲+编码器
		/// </summary>
		public int Mode { get; set; } = 14;

		/// <summary>
		/// Home时的加速度
		/// </summary>
		public float Acc { get; set; }

		/// <summary>
		/// Home时的减速度
		/// </summary>
		public float Dec { get; set; }

		/// <summary>
		/// 回零时高速段速度。
		/// </summary>
		public float HiSpeed { get; set; } = 100000;

		/// <summary>
		/// 回零时爬行段速度。
		/// </summary>
		public float CreepSpeed { get; set; } = 2000;
	}
}
