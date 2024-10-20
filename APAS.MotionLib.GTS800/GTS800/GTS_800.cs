using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Core;
using GTS_800;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using APAS.CoreLib.Charting;
using static gts.mc;

/*
注意：

1. 应用该模板时，请注意将命名空间更改为实际名称。
2. 该类中的所有Childxxx()方法中，请勿阻塞式调用实际的运动控制器库函数，因为在APAS主程序中，可能会同时调用多个轴同步移动。
3. 请保证所有的Childxxx()方法为线程安全。

*/

namespace APAS__MotionLib_Template
{
    public class GTS_800 : MotionControllerBase
    {
        #region Variables

        private const int MAX_AXIS = 8;

        private readonly short _mCardId;
        private Gts_AxisCfg _gtsAxisCfg;
        private readonly string _configFileGts = "gts800.cfg";
        private readonly string _configFileAxis = "Gts800_AxisCfg.json";

        /// <summary>
        /// GTS800无法记忆Home状态，故采用变量记录。
        /// 该方法的弊端是，程序重启后Home信息丢失，必须重新Home。
        /// </summary>
        private readonly bool[] _buffHome = new bool[MAX_AXIS];

        #endregion

        #region Constructors

        /// <summary>
        /// 注意：类名应为 “MotionController",请勿更改。
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public GTS_800(string portName, int baudRate, string config, ILog logger) 
            : base(portName, baudRate, config, logger)
        {
            //TODO 此处初始化控制器参数；如果下列参数动态读取，则可在InitImpl()函数中赋值。

            if (!short.TryParse(portName, out _mCardId))
                _mCardId = 0;

            var configs = config.Split(',');
            if (configs.Length == 2)
            {
                _configFileGts = configs[0];
                _configFileAxis = configs[1];
            }

            _buffHome = new bool[configs.Length];
        }

        #endregion

        #region Overrided Methods

        /// <summary>
        /// 初始化指定轴卡。
        /// </summary>
        protected override void InitImpl()
        {
            //TODO 1.初始化运动控制器对象，例如凌华轴卡、固高轴卡等。
            // 例如：初始化固高轴卡：gts.mc.GT_Open(portName, 1);

            //TODO 2.读取控制器固件版本，并赋值到属性 FwVersion

            //TODO 3.读取每个轴的信息，构建 InnerAxisInfoCollection，包括轴号和固件版本号。
            // 注意：InnerAxisInfoCollection 已在基类的构造函数中初始化
            // 例如： InnerAxisInfoCollection.Add(new AxisInfo(1, new Version(1, 0, 0)));

            //TODO 4.需要完成函数 ReadStatusImpl()，否则会报NotImplementException异常。
            var rtn = GT_Open((short) _mCardId, 0, 1);
            CommandRtnCheck(rtn, nameof(GT_Open));

            rtn = GT_Reset((short) _mCardId);
            CommandRtnCheck(rtn, nameof(GT_Reset));

            var fullName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _configFileGts);
            if (!File.Exists(fullName))
                throw new FileNotFoundException($"无法找到轴卡配置文件 {fullName}");
            rtn = GT_LoadConfig((short)_mCardId, fullName);
            CommandRtnCheck(rtn, nameof(GT_LoadConfig));

            rtn = GT_ClrSts((short) _mCardId, 1, 8);
            CommandRtnCheck(rtn, nameof(GT_ClrSts));

            LoadAxisConfiguration();

            // 强制自动ServoOn所有轴，不管是否为伺服轴
            var cfg = _gtsAxisCfg.CardAxisCfgs.FirstOrDefault(x => x.CardId == _mCardId);
            if(cfg!=null)
            {
                foreach (var axis in cfg.AxisCfgs)
                {
                    ServoOnImpl(axis.AxisIndex);
                }
            }
        }

        /// <summary>
        /// 设置指定轴的加速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="acc">加速度值</param>
        protected override void SetAccImpl(int axis, double acc)
        {
            var rtn = GT_GetTrapPrm(_mCardId, (short) axis, out var trapPrm);
            CommandRtnCheck(rtn, "GT_GetTrapPrm  in SetAccImpl");
            trapPrm.acc = acc;
            rtn = GT_SetTrapPrm(_mCardId, (short) axis, ref trapPrm);
            CommandRtnCheck(rtn, "GT_SetTrapPrm  in SetAccImpl");
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void SetDecImpl(int axis, double dec)
        {
            var rtn = GT_GetTrapPrm(_mCardId, (short) axis, out var trapPrm);
            CommandRtnCheck(rtn, nameof(GT_GetTrapPrm));
            trapPrm.acc = dec;
            rtn = GT_SetTrapPrm(_mCardId, (short) axis, ref trapPrm);
            CommandRtnCheck(rtn, nameof(GT_SetTrapPrm));
        }

        protected override void SetEsDecImpl(int axis, double dec)
        {
            var rtn = GT_SetStopDec(_mCardId, (short)axis, dec, dec);
            CommandRtnCheck(rtn, nameof(GT_SetStopDec));
        }

        /// <summary>
        /// 指定轴回机械零点。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="hiSpeed">快速找机械原点的速度值。如不适用请忽略。</param>
        /// <param name="creepSpeed">找到机械原点后返回零位的爬行速度。如不适用请忽略。</param>
        protected override void HomeImpl(int axis, double hiSpeed, double creepSpeed)
        {
            /*
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
             */

            THomeStatus homeStatus;
            var homeParam = CreateAxisParam((short) axis);

            homeParam.acc = 5;
            homeParam.dec = 5;
            homeParam.velHigh = hiSpeed;
            homeParam.velLow = 1;
            _buffHome[axis] = false;
            var rtn = GT_GoHome(_mCardId, (short) axis, ref homeParam); //启动回零
            CommandRtnCheck(rtn, nameof(GT_GoHome));
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            var rtn = GT_GetHomeStatus(_mCardId, (short)axis, out var homeStatus);
            CommandRtnCheck(rtn, nameof(GT_GetHomeStatus));
            var isHomeDone = homeStatus.run == 0;
            var isHomeSucceed = homeStatus.error == 0;

            if (isHomeDone)
            {
                if (isHomeSucceed)
                {
                    // 如果Home结束，将轴卡位置清零。
                    rtn = GT_ZeroPos(_mCardId, (short)axis, 1);
                    CommandRtnCheck(rtn, nameof(GT_ZeroPos));

                    rtn = GT_ClrSts(_mCardId, (short)axis, 1);
                    CommandRtnCheck(rtn, nameof(GT_ClrSts));

                    // 标记为Home状态
                    _buffHome[axis] = true;
                }
            }

            return isHomeDone;
        }

        /// <summary>
        /// 移动指定轴（相对移动模式）。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="speed">移动速度。该速度根据APAS主程序的配置文件计算得到。计算方法为MaxSpeed * 速度百分比。</param>
        /// <param name="distance">相对移动的距离。该距离已被APAS主程序转换为轴卡对应的实际单位。例如对于脉冲方式，
        /// 该值已转换为步数；对于伺服系统，该值已转换为实际距离。</param>
        protected override void MoveImpl( int axis, double speed, double distance)
        {
            /*
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
            */

            // restrict the distance to the range of -999999999 to 999999999.
            if (distance < -999999990)
                distance = -999999990;

            if (distance > 999999990)
                distance = 999999990;

            // clear the status before moving since the end limit might be triggered while moving. 
            var rtn = GT_ClrSts(_mCardId, (short)axis, 1);
            CommandRtnCheck(rtn, nameof(GT_ClrSts));

            rtn = GT_PrfTrap(_mCardId, (short) axis);
            CommandRtnCheck(rtn, nameof(GT_PrfTrap));

            rtn = GT_SetVel(_mCardId, (short) axis, speed);
            CommandRtnCheck(rtn, nameof(GT_SetVel));

            var pos = new double[1];
            rtn = GT_GetAxisEncPos(_mCardId, (short) axis, pos, 1, out var clk);
            CommandRtnCheck(rtn, nameof(GT_GetAxisEncPos));

            rtn = GT_SetPos(_mCardId, (short) axis, (int)pos[0] + (int) distance);
            CommandRtnCheck(rtn, nameof(GT_SetPos));

            rtn = GT_Update(_mCardId, 1 << (axis - 1));
            CommandRtnCheck(rtn, nameof(GT_Update));
        }


        protected override bool CheckMotionDoneImpl(int axis)
        {
            var rtn = GT_GetSts(_mCardId, (short)axis, out var status, 1, out var pClock);
            CommandRtnCheck(rtn, nameof(GT_GetSts));

            // 等待0x400位清零。
            return (status & 0x400) == 0;
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOnImpl(int axis)
        {
            var rtn = GT_AxisOn(_mCardId, (short) axis);
            CommandRtnCheck(rtn, nameof(GT_AxisOn));

            //TODO Check the status of the axis to report the errors;
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOffImpl(int axis)
        {
            var rtn = GT_AxisOff(_mCardId, (short) axis);
            CommandRtnCheck(rtn, nameof(GT_AxisOff));
        }

        /// <summary>
        /// 读取最新的绝对位置。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <returns>最新绝对位置</returns>
        protected override double ReadPosImpl(int axis)
        {
            //pClock 读取控制器时钟，默认值为：NULL，即不用读取控制器时钟
            //count  读取的轴数，默认为 1。正整数。
            var pos = new double[1];
            var rtn = GT_GetAxisEncPos(_mCardId, (short) axis, pos, 1, out var pClock);
            CommandRtnCheck(rtn, nameof(GT_GetAxisEncPos));
            return pos[0];
        }

        /// <summary>
        /// 更新指定轴状态。
        /// <para>注意：请在该函数调用RaiseAxisStateUpdatedEvent()函数，以通知APAS主程序当前轴的状态已更新。</para>
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override StatusInfo ReadStatusImpl(int axis)
        {
            var rtn = GT_GetSts(_mCardId, (short)axis, out var pSts, 1, out var _);
            CommandRtnCheck(rtn, nameof(GT_GetSts));
            
            var isBusy = (pSts & 0x400) != 0;
            var isInp = !isBusy;
            var isHomed = _buffHome[axis];
            var isServoOn = (pSts & 0x200) == 1;
            var isAlarm = ParseAlarm(pSts, out var alarm);
            return new StatusInfo(isBusy, isInp, isHomed, isServoOn, [alarm]);
        }

        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ResetAlarmImpl(int axis)
        {
            var rtn = GT_ClrSts((short) _mCardId, (short) axis, 1);
            CommandRtnCheck(rtn, "GT_ClrSts");

            //TODO Check the status of the axis to report the errors
        }


        #region IO Controller

        /// <summary>
        /// 设置指定数字输出端口的状态。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="isOn">是否设置为有效电平</param>
        protected override void SetDOImpl(int port, bool isOn)
        {
            //MC_ENABLE(该宏定义为 10)：驱动器使能。
            //MC_CLEAR(该宏定义为 11)：报警清除。
            //MC_GPO(该宏定义为 12)：通用输出。

            var rtn = GT_SetDoBit(_mCardId, MC_GPO, (short) (port + 1), isOn ? (short) 0 : (short) 1);

            CommandRtnCheck(rtn, "GT_SetDoBit");
        }

        /// <summary>
        /// 读取指定数字输出端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDOImpl(int port)
        {
            //MC_ENABLE(该宏定义为 10)：驱动器使能。
            //MC_CLEAR(该宏定义为 11)：报警清除。
            //MC_GPO(该宏定义为 12)：通用输出。
            var rtn = GT_GetDo(_mCardId, MC_GPO, out var pValue);
            CommandRtnCheck(rtn, "GT_GetDo");
            return (pValue & (1 << port)) == 0;
        }

        /// <summary>
        /// 读取所有数字输出端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override bool[] ReadDOImpl()
        {
            var rtn = GT_GetDo(_mCardId, MC_GPO, out var pValue);
            CommandRtnCheck(rtn, "GT_GetDo");
            var states = new bool[16];
            for (var i = 0; i < 16; i++)
                states[i] = (pValue & (1 << i)) == 0;

            return states;
        }

        /// <summary>
        /// 读取指定数字输入端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDIImpl(int port)
        {
            var rtn = GT_GetDi(_mCardId, MC_GPI, out var pValue);
            CommandRtnCheck(rtn, "GT_GetDi");
            return (pValue & (1 << port)) == 0;
        }

        /// <summary>
        /// 读取所有数字输入端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override bool[] ReadDIImpl()
        {
            var rtn = GT_GetDi(_mCardId, MC_GPI, out var pValue);
            CommandRtnCheck(rtn, "GT_GetDi");
            var states = new bool[16];
            for (var i = 0; i < 16; i++)
                states[i] = (pValue & (1 << i)) == 0;

            return states;
        }

        #endregion

        #region Analog Controller

        /// <summary>
        /// 读取所有模拟输入端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override double[] ReadAIImpl()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAIImpl(int port)
        {
            var rtn = GT_GetAdc(_mCardId, (short) port, out var pValue, 1, out var pClock);
            CommandRtnCheck(rtn, nameof(GT_GetAdc));
            return pValue;
        }

        /// <summary>
        /// 读取所有模拟输出端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override double[] ReadAOImpl()
        {
            var rtn = GT_GetDac(_mCardId, 0, out var value, 1, out _);
            CommandRtnCheck(rtn, nameof(GT_GetDac));
            return new double[] { value };
        }

        /// <summary>
        /// 读取指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAOImpl(int port)
        {
            var rtn = GT_GetDac(_mCardId, (short) port, out var value, 1, out _);
            CommandRtnCheck(rtn, nameof(GT_GetDac));
            return value;
        }

        /// <summary>
        /// 设置指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="value">电压值</param>
        protected override void SetAOImpl(int port, double value)
        {
            var data = (short) value;

            var rtn = GT_SetDac(_mCardId, (short) port, ref data, 1);
            CommandRtnCheck(rtn, nameof(GT_SetDac));
        }


        #endregion

        /// <summary>
        /// 停止所有轴移动。
        /// </summary>
        protected override void StopImpl()
        {
            GT_Stop(_mCardId, 0xff, 0);
        }

        protected override void StopImpl(int axis)
        {
            GT_Stop(_mCardId, axis, 0);
        }

        protected override void EStopImpl()
        {
            GT_Stop(_mCardId, 0xff, 0xff);
        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void DisposeImpl()
        {
        }

        #endregion

        private AxisCfg FindAxisConfig(int cardId, int axisId)
        {
            var cfgs = _gtsAxisCfg.CardAxisCfgs.FirstOrDefault(x => x.CardId == cardId);
            if (cfgs == null)
                throw new NullReferenceException($"未在轴运动配置文件 {_configFileGts} 中找到轴卡 {_mCardId}。");

            var axisCfg = cfgs.AxisCfgs.FirstOrDefault(x => x.AxisIndex == axisId);
            if (axisCfg == null)
                throw new NullReferenceException($"未在轴运动配置文件 {_configFileGts} 中找到轴卡 {_mCardId}的{axisId}号轴。");

            return axisCfg;
        }

        private void CommandRtnCheck(short rtn, string commandName)
        {
            var conStr = $"cardNum : {_mCardId};";
            var errorInfo = string.Empty;
            switch (rtn)
            {
                case 0:
                    break;

                case 1:
                    errorInfo = $"{commandName} 指令执行错误";
                    break;

                case 2:
                    errorInfo = $"{commandName} 指令license不支持";
                    break;

                case 7:
                    errorInfo = $"{commandName} 指令参数错误";
                    break;

                case 8:
                    errorInfo = $"{commandName} 指令DSP固件不支持";
                    break;

                case -1:
                case -2:
                case -3:
                case -4:
                case -5:
                    errorInfo = $"{commandName} 指令与控制卡通讯失败";
                    break;

                case -6:
                    errorInfo = $"打开控制器失败";
                    break;

                case -7:
                    errorInfo = $"运动控制器没有相应";
                    break;

                case -8:
                    errorInfo = $"{commandName} 指令多线程资源忙";
                    break;

                default:
                    errorInfo = $"{commandName} 指令返回未知错误";

                    break;
            }

            if (!string.IsNullOrEmpty(errorInfo))
                throw new Exception(conStr + errorInfo);
        }


        private THomePrm CreateAxisParam(short axisIndex)
        {
            var rtn = GT_GetHomePrm(_mCardId, axisIndex, out var homeParam);

            var cfg = _gtsAxisCfg.CardAxisCfgs.FirstOrDefault(x => x.CardId == _mCardId)
                ?.AxisCfgs
                .FirstOrDefault(x => x.AxisIndex == axisIndex);

            if (cfg == null)
                throw new Exception($"找不到轴卡({_mCardId})的轴({axisIndex})的配置文件");


            cfg.Validate();

            homeParam.mode = (short) cfg.HomeMode; //回零方式
            homeParam.moveDir = (short) cfg.HomeDir; //回零方向

            homeParam.searchHomeDistance = cfg.SearchHomeDistance; //搜搜距离
            homeParam.homeOffset = cfg.HomeOffset; //偏移距离
            homeParam.escapeStep = cfg.EscapeStep;
            homeParam.pad2_1 = (short) cfg.Pad2_1; //此参数表示如果回零时sensor处于原点位置上，也会再继续回原点动作，否者会异常

            return homeParam;
        }

        private void LoadAxisConfiguration()
        {
            var fileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _configFileAxis);
            if (!File.Exists(fileName))
                return;

            var jsonInfo = File.ReadAllText(fileName);
            try
            {
                _gtsAxisCfg = JsonConvert.DeserializeObject<Gts_AxisCfg>(jsonInfo);
            }
            catch (Exception ex)
            {
                throw new Exception($"无法加载轴参数配置文件 {fileName}, {ex.Message}");
            }
        }

        /// <summary>
        /// 从Int类型状态数值中解析报警信息。
        /// </summary>
        /// <param name="pSts"></param>
        /// <param name="alarm"></param>
        /// <returns></returns>
        private bool ParseAlarm(int pSts, out AlarmInfo alarm)
        {
            alarm = null;
            var code = 0;
            var message = "";

            if ((pSts & 0x2) != 0)
            {
                code = 0x2;
                message = "伺服报警";
            }
            else if ((pSts & 0x10) != 0)
            {
                code = 0x10;
                message = "跟随误差越线";
            }
            else if ((pSts & 0x20) != 0)
            {
                code = 0x20;
                message = "正限位触发";
            }
            else if ((pSts & 0x40) != 0)
            {
                code = 0x40;
                message = "负限位触发";
            }
            //if ((pSts & 0x80) != 0)
            //    throw new Exception($"第{_mCardId}号卡，第{axis}个轴 平滑停止");
            else if ((pSts & 0x100) != 0)
            {
                code = 0x100;
                message = "紧急停止状态";
            }

            if (code == 0)
                return false;

            alarm = new AlarmInfo(code, message);
            return true;

        }
    }
}