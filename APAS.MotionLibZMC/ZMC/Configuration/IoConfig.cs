using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APAS.MotionLib.ZMC.Configuration
{
    public class IoConfig
    {

        /// <summary>
        /// 应用于复位伺服驱动器的输出IO。
        /// </summary>
        public int Reset { get; set; } = -1;

        /// <summary>
        /// 控制伺服驱动器ServoOn信号的输出IO。
        /// </summary>
        public int ServoOn { get; set; } = -1;

        /// <summary>
        /// 伺服驱动器返回的Alarm信号输入。
        /// </summary>
        public int Alarm { get; set; } = -1;

        /// <summary>
        /// 原点sensor接入的输入信号。
        /// </summary>
        public int Org { get; set; } = -1;

        /// <summary>
        /// 原点sensor信号是否反转，一般常开输入需要反转
        /// </summary>
        public bool InvOrg { get; set; } = false;

        /// <summary>
        /// 正限位sensor接入的输入信号。
        /// </summary>
        public int Pel { get; set; } = -1;

        /// <summary>
        /// 正限位sensor信号是否反转，一般常开输入需要反转
        /// </summary>
        public bool InvPel { get; set; } = false;

        /// <summary>
        /// 负限位sensor接入的输入口序号
        /// </summary>
        public int Nel { get; set; } = -1;

        /// <summary>
        /// 负限位sensor信号是否反转，一般常开输入需要反转
        /// </summary>
        public bool InvNel { get; set; } = false;

        /// <summary>
        /// 是否使用负限位作为回零检测信号。
        /// </summary>
        public bool IsNelAsDatum { get; set; } = true;
    }
}
