using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APAS.MotionLib.ZMC.Configuration
{
    public class ControlConfig
    {

        /// <summary>
        /// 轴索引
        /// </summary>
        public int Index { get; set; } = -1;

        /// <summary>
        /// 脉冲当量
        /// </summary>
        public float Units { get; set; } = 1;

        /// <summary>
        /// 轴类型
        /// </summary>
        public int AxisType { get; set; } = 7;

        /// <summary>
        /// 脉冲模式设置
        /// </summary>
        public int InvertStep { get; set; } = 7;
    }
}
