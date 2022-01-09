namespace APAS.MotionLib.ZMC.Configuration
{
	public class HomingConfig
	{
		/// <summary>
		/// 回零模式，默认16，负方向脉冲+编码器，Z向信号回零
		/// </summary>
		public int Mode { get; set; } = 16;

		/// <summary>
		/// 原点sensor接入的输入口序号
		/// </summary>
		public int OrgIo { get; set; } = -1;
		/// <summary>
		/// 原点sensor信号是否反转，一般常开输入需要反转
		/// </summary>
		public bool OrgIoInv { get; set; } = false;
		/// <summary>
		/// 正限位sensor接入的输入口序号
		/// </summary>
		public int PelIo { get; set; } = -1;
		/// <summary>
		/// 正限位sensor信号是否反转，一般常开输入需要反转
		/// </summary>
		public bool PelIoInv { get; set; } = false;
		/// <summary>
		/// 负限位sensor接入的输入口序号
		/// </summary>
		public int NelIo { get; set; } = -1;
		/// <summary>
		/// 负限位sensor信号是否反转，一般常开输入需要反转
		/// </summary>
		public bool NelIoInv { get; set; } = false;

		/// <summary>
		/// Home时的加速度
		/// </summary>
		public float Acc { get; set; }

		/// <summary>
		/// Home时的减速度
		/// </summary>
		public float Dec { get; set; }
	}
}
