
namespace APAS.MotionLib.ZMC.Configuration
{
	public class ScopeConfig
	{
		/// <summary>
		/// 采样间隔，单位ms
		/// <para>注意最小是为1ms</para>
		/// </summary>
		public int InvtervalMs { get; set; } = 5;

		/// <summary>
		/// 采样深度
		/// </summary>
		public int Deepth { get; set; } = 10000;
	}
}
