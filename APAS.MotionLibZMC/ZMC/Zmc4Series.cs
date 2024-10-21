using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Core;
using APAS.MotionLib.ZMC.Configuration;
using cszmcaux;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using APAS.CoreLib.Charting;

/*
 注意：

 1. 应用该模板时，请注意将命名空间更改为实际名称。
 2. 该类中的所有Childxxx()方法中，请勿阻塞式调用实际的运动控制器库函数，因为在APAS主程序中，可能会同时调用多个轴同步移动。
 3. 请保证所有的Childxxx()方法为线程安全。

*/

namespace APAS.MotionLib.ZMC
{
    public class Zmc4Series : MotionControllerBase
    {
        #region Variables
        
        private IntPtr _hMc;
        private readonly string _configFileAxis = "Zmc4SeriesConf.json";
        private readonly McConfig _mcConfig;
        private int _maxAxis;
        private int _maxAO;
        private int _maxAI;

        #endregion

        #region Constructors

        /// <summary>
        /// 注意：类名应为 “MotionController",请勿更改。
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public Zmc4Series(string portName, int baudRate, string config, ILog logger = null) 
            : base(portName, baudRate,config, logger)
        {
            var configs = config.Split(',');
            if (configs.Length == 1 || configs.Length == 2)
            {
                _configFileAxis = configs[0];
            }
            else
            {
                throw new ArgumentException($"unable to parse the config string {configs}.", nameof(config));
            }

            // 加载自定义配置
            var paramPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _configFileAxis);
            ReadParamFile(paramPath, ref _mcConfig);

            //TODO 此处初始化控制器参数；如果下列参数动态读取，则可在InitImpl()函数中赋值。
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

            /* var rtn = zmcaux.ZAux_SearchEth(PortName, 100);
             CommandRtnCheck(rtn, "ZAux_SearchEth");*/

            var rtn = zmcaux.ZAux_OpenEth(PortName, out _hMc);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_OpenEth));

            // 检查轴卡型号并更新轴总数
            var model = SendBasicCommand("?control");
            if (model.StartsWith("406"))
            {
                _maxAxis = 6;
                _maxAO = 2;
                _maxAI = 2;
            }
            else if (model.StartsWith("412"))
            {
                _maxAxis = 12;
                _maxAO = 2;
                _maxAI = 2;
            }

            // 如果上次程序没有退出，可能还有正在运动的轴，
            // 先执行一次急停，确保左右轴均处于停止状态；否则下面的ChildServoOn异常。
            EStopImpl();

            //ApplyConfig(_hMc, _mcConfig);

            // Servo On 所有轴
            for (var i = 0; i < _maxAxis; i++)
                ServoOnImpl(i);
        }

        /// <summary>
        /// 设置指定轴的加速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="acc">加速度值</param>
        protected override void SetAccImpl(int axis, double acc)
        {
            PreMove(axis);

            var rtn = zmcaux.ZAux_Direct_SetAccel(_hMc, axis, (float)acc);
            CommandRtnCheck(rtn, "ZAux_Direct_SetAccel  in SetAccImpl");
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void SetDecImpl(int axis, double dec)
        {
            PreMove(axis);

            var rtn = zmcaux.ZAux_Direct_SetDecel(_hMc, axis, (float)dec);
            CommandRtnCheck(rtn, "ZAux_Direct_SetDecel  in SetAccImpl");
        }

        protected override void SetEsDecImpl(int axis, double dec)
        {
            PreMove(axis);

            var rtn = zmcaux.ZAux_Direct_SetDecel(_hMc, axis, (float)dec);
            CommandRtnCheck(rtn, "ZAux_Direct_SetDecel  in SetAccImpl");
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
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
             */
            int rtn;
            var axisMoveStatus = 0;
            var homeParam = FindAxisConfig(axis, _mcConfig)?.Home;
            var movParam = FindAxisConfig(axis, _mcConfig)?.Motion;

            if (homeParam == null || movParam == null)
                throw new NullReferenceException($"unable to find the config of the axis ({axis}).");

            PreMove(axis);

            // 将该轴标记为未Home
            rtn = zmcaux.ZAux_Modbus_Set0x(_hMc, (ushort)axis, 1, new byte[] { 0 });
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Modbus_Set0x));

            rtn = zmcaux.ZAux_Direct_SetCreep(_hMc, axis, (float)creepSpeed);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetCreep));

            rtn = zmcaux.ZAux_Direct_SetAccel(_hMc, axis, homeParam.Acc);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetAccel));

            rtn = zmcaux.ZAux_Direct_SetDecel(_hMc, axis, homeParam.Dec);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetDecel));

            rtn = zmcaux.ZAux_Direct_SetFastDec(_hMc, axis, (float)movParam.FastDec);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetFastDec));

            rtn = zmcaux.ZAux_Direct_SetSpeed(_hMc, axis, (float)hiSpeed);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetSpeed));

            rtn = zmcaux.ZAux_Direct_Single_Datum(_hMc, axis, homeParam.Mode);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_Single_Datum));
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            var status = 0;
            var rtn = zmcaux.ZAux_Direct_GetIfIdle(_hMc, axis, ref status);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetIfIdle));

            var isHomeDone = status == 0;
            if (isHomeDone)
            {
                var isAlarm = CheckAlarm(axis, out var alarm);
                if (!isAlarm)
                {
                    var movParam = FindAxisConfig(axis, _mcConfig)?.Motion;
                    if (movParam == null)
                        throw new NullReferenceException($"unable to find the config of the axis ({axis}).");

                    // 清空位置
                    rtn = zmcaux.ZAux_Direct_SetMpos(_hMc, axis, 0);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetMpos));

                    rtn = zmcaux.ZAux_Direct_SetDpos(_hMc, axis, 0);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetDpos));

                    // 将该轴标记为已Home
                    rtn = zmcaux.ZAux_Modbus_Set0x(_hMc, (ushort)axis, 1, new byte[] { 1 });
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Modbus_Set0x));

                    // 将加速度设置为Move使用的加速度
                    rtn = zmcaux.ZAux_Direct_SetAccel(_hMc, axis, (float)movParam.Acc);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetAccel));

                    rtn = zmcaux.ZAux_Direct_SetDecel(_hMc, axis, (float)movParam.Dec);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetDecel));

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 移动指定轴（相对移动模式）。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="speed">移动速度。该速度根据APAS主程序的配置文件计算得到。计算方法为MaxSpeed * 速度百分比。</param>
        /// <param name="distance">相对移动的距离。该距离已被APAS主程序转换为轴卡对应的实际单位。例如对于脉冲方式，
        /// 该值已转换为步数；对于伺服系统，该值已转换为实际距离。</param>
        protected override void MoveImpl(int axis, double speed, double distance)
        {
            /*
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
            */
            var axisMoveStatus = 0;

            PreMove(axis);

            var rtn = zmcaux.ZAux_Direct_SetSpeed(_hMc, axis, (float)speed);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetSpeed));

            rtn = zmcaux.ZAux_Direct_Single_Move(_hMc, axis, (float)distance);

            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_Single_Move));
        }

        protected override bool CheckMotionDoneImpl(int axis)
        {
            var idleFlag = 0;

            var rtn = zmcaux.ZAux_Direct_GetIfIdle(_hMc, axis, ref idleFlag);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetIfIdle));
            return idleFlag == 1;
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOnImpl(int axis)
        {
            if (_mcConfig.Axes[axis].Io.ServoOn <= -1) 
                return;

            PreMove(axis, false);
            SetDOImpl(_mcConfig.Axes[axis].Io.ServoOn, true);
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOffImpl(int axis)
        {
            if (_mcConfig.Axes[axis].Io.ServoOn <= -1)
                return;

            PreMove(axis, false);
            SetDOImpl(_mcConfig.Axes[axis].Io.ServoOn, false);
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
            float pos = 0;
            var rtn = zmcaux.ZAux_Direct_GetMpos(_hMc, axis, ref pos);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetMpos));
            return pos;
        }

        protected override StatusInfo ReadStatusImpl(int axis)
        {
            // 检查轴是否正忙
            var busyFlag = 0;
            var rtn = zmcaux.ZAux_Direct_GetIfIdle(_hMc, axis, ref busyFlag);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetIfIdle));
            var isBusy = busyFlag == 0;

            var isInp = !isBusy;

            // 检查是否已回零
            var homeFlag = new byte[1];
            rtn = zmcaux.ZAux_Modbus_Get0x(_hMc, (ushort)axis, 1, homeFlag);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Modbus_Get0x));
            var isHomed = homeFlag[0] == 1;

            // 查询使能状态
            var servoFlag = 0;
            rtn = zmcaux.ZAux_Direct_GetAxisEnable(_hMc, axis, ref servoFlag);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetAxisEnable));
            var isServoOn = servoFlag == 1;

            // 查询是否报警
            var isAlarm = CheckAlarm(axis, out var alarm);
            return new StatusInfo(isBusy, isInp, isHomed, isServoOn, [alarm]);
        }

        private bool CheckAlarm(int axis, out AlarmInfo alarm)
        {
            alarm = null;

            // 检查急停
            if (CheckEmbStatus())
                throw new Exception($"紧急停止");

            // 若非急停，检查轴状态
            var errCode = 0;
            var errInfo = string.Empty;

            //int rtn = zmcaux.ZAux_Direct_GetAxisStatus(_hMc, axisIndex, ref reason);
            var rtn = zmcaux.ZAux_Direct_GetAxisStopReason(_hMc, axis, ref errCode);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetAxisStopReason));

            if (errCode == 0)
                return false;
            else if ((errCode & 0x2) > 0)
                errInfo = "随动误差超限报警";
            else if ((errCode & 0x4) > 0)
                errInfo = "与远程轴通讯错误";
            else if ((errCode & 0x8) > 0)
                errInfo = "远程驱动器报错";
            else if ((errCode & 0x10) > 0)
                errInfo = "正向硬限位";
            else if ((errCode & 0x20) > 0)
                errInfo = "反向硬限位";
            else if ((errCode & 0x40) > 0)
                errInfo = "回原点中";
            else if ((errCode & 0x80) > 0)
                errInfo = "随动误差超限报警";
            else if ((errCode & 0x100) > 0)
                errInfo = "随动误差超限出错";
            else if ((errCode & 0x200) > 0)
                errInfo = "超过正向软限位";
            else if ((errCode & 0x400) > 0)
                errInfo = "超过负向软限位";
            //else if ((errCode & 0x800) > 0)
                // 人为停止
            else if ((errCode & 0x1000) > 0)
                errInfo = "脉冲频率超过MAX_SPEED限制";
            else if ((errCode & 0x4000) > 0)
                errInfo = "机械手指令坐标错误";
            else if ((errCode & 0x40000) > 0)
                errInfo = "电源异常";
            else if ((errCode & 0x200000) > 0)
                errInfo = "运动中触发特殊运动指令失败";
            else if ((errCode & 0x400000) > 0)
                errInfo = "报警信号输入";
            else if ((errCode & 0x800000) > 0)
                errInfo = "轴进入暂停状态";

            alarm = new AlarmInfo(errCode, errInfo);
            return true;
        }


        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ResetAlarmImpl(int axis)
        {
            var axisConf = FindAxisConfig(axis, _mcConfig);

            if (axisConf.Io.Reset < 0)
                return;

            // 复位伺服驱动器
            zmcaux.ZAux_Direct_SetOp(_hMc, axisConf.Io.Reset, 1);
            Thread.Sleep(100);
            zmcaux.ZAux_Direct_SetOp(_hMc, axisConf.Io.Reset, 0);
        }


        #region IO Controller

        /// <summary>
        /// 设置指定数字输出端口的状态。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="isOn">是否设置为有效电平</param>
        protected override void SetDOImpl(int port, bool isOn)
        {
            var rtn = zmcaux.ZAux_Direct_SetOp(_hMc, port, isOn ? (uint)1 : (uint)0);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetOp));
        }

        /// <summary>
        /// 读取指定数字输出端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDOImpl(int port)
        {
            // 如果端口号为-1，则始终给true信号。
            if (port < 0)
                return true;

            uint piValue = 0;
            var rtn = zmcaux.ZAux_Direct_GetOp(_hMc, port, ref piValue);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetOp));
            return piValue != 0;

        }

        /// <summary>
        /// 读取所有数字输出端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override bool[] ReadDOImpl()
        {
            var outputStatus = new uint[1];
            var rtn = zmcaux.ZAux_Direct_GetOutMulti(_hMc, 0, 15, outputStatus);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetOutMulti));
            var states = new bool[16];
            for (var i = 0; i < 16; i++)
            {
                states[i] = (outputStatus[0] & (1 << i)) != 0;
            }

            return states;
        }

        /// <summary>
        /// 读取指定数字输入端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDIImpl(int port)
        {
            uint pivalue = 0;
            var rtn = zmcaux.ZAux_Direct_GetIn(_hMc, port, ref pivalue);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetIn));
            return pivalue != 0;

        }

        /// <summary>
        /// 读取所有数字输入端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override bool[] ReadDIImpl()
        {
            var outputStatus = new int[1];
            var rtn = zmcaux.ZAux_Direct_GetInMulti(_hMc, 0, 15, outputStatus);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetInMulti));
            var states = new bool[16];
            for (var i = 0; i < 16; i++)
            {
                states[i] = (outputStatus[0] & (1 << i)) != 0;
            }

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
            var values = new List<double>();

            for (var i = 0; i < _maxAI; i++)
            {
                var val = 0.0f;
                zmcaux.ZAux_Direct_GetAD(_hMc, _mcConfig.Ain.IndexStart + i, ref val);

                var param = _mcConfig.Ain.Param.FirstOrDefault(x => x.Channel == i);
                values.Add(ConvertAdcRawToRealworld(param, val));
            }

            return values.ToArray();
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAIImpl(int port)
        {
            var val = 0.0f;
            zmcaux.ZAux_Direct_GetAD(_hMc, _mcConfig.Ain.IndexStart + port, ref val);
            var param = _mcConfig.Ain.Param.FirstOrDefault(x => x.Channel == port);
            return ConvertAdcRawToRealworld(param, val);
        }

        /// <summary>
        /// 读取所有模拟输出端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override double[] ReadAOImpl()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAOImpl(int port)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 设置指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="value">电压值</param>
        protected override void SetAOImpl(int port, double value)
        {
            throw new NotSupportedException();
        }

        #endregion

        /// <summary>
        /// 在指定轴上执行自动接触检测功能。
        /// <para>该功能适用于Irixi M12控制器。</para>
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="analogInputPort">模拟输入端口号</param>
        /// <param name="vth">阈值电压</param>
        /// <param name="distance">最大移动距离</param>
        /// <param name="speed">移动速度</param>
        protected override void AutoTouchImpl(int axis, int analogInputPort, double vth, double distance, double speed)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 执行快速线性扫描。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="range">扫描范围</param>
        /// <param name="interval">反馈信号采样间隔</param>
        /// <param name="speed">移动速度</param>
        /// <param name="capture">反馈信号捕获端口</param>
        /// <param name="scanResult">扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int capture, out Point2D[] scanResult)
        {
            Fast1DImpl(axis, range, interval, speed, capture, out scanResult, -1, out _);
        }

        /// <summary>
        /// 执行双通道快速线性扫描。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="range">扫描范围</param>
        /// <param name="interval">第1路反馈信号采样间隔</param>
        /// <param name="speed">移动速度</param>
        /// <param name="capture">反馈信号捕获端口</param>
        /// <param name="scanResult">第1路扫描结果列表（X:位置，Y:反馈信号）</param>
        /// <param name="capture2">第2路反馈信号采样间隔</param>
        /// <param name="scanResult2">第2路扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int capture, out Point2D[] scanResult, int capture2, out Point2D[] scanResult2)
        {
            scanResult = null;
            scanResult2 = null;

            var point2Ds1 = new List<Point2D>();
            var point2Ds2 = new List<Point2D>();

            var respon = new StringBuilder();

            // 从配置文件中读取参数，以将ADC回读值转换为真实值
            var adcParam1 = _mcConfig.Ain.Param.FirstOrDefault(x => x.Channel == capture);
            var adcParam2 = _mcConfig.Ain.Param.FirstOrDefault(x => x.Channel == capture2);

            // 总采样点数，注意此值必须为2的倍数
            var totalSamplingPoints = _mcConfig.Scope.Depth * (capture2 < 0 ? 2 : 3);
            /*if (totalSamplingPoints < 30000)
                totalSamplingPoints = 30000;*/

            // 采样间隔，单位ms
            // 注意：此值和上述值共同决定了能够采样的最大时间，例如1000(点) * 5ms = 5000ms
            var samplingIntervalMs = _mcConfig.Scope.SamplingIntervalMs;
            if (samplingIntervalMs < 1)
                samplingIntervalMs = 1;

            var command = "";
            if (capture2 < 0)
            {
                command =
                    $"SCOPE(ON,{samplingIntervalMs},0,{totalSamplingPoints},MPOS({axis})," +
                    $"AIN({capture + _mcConfig.Ain.IndexStart}))";
            }
            else
            {
                command =
                    $"SCOPE(ON,{samplingIntervalMs},0,{totalSamplingPoints},MPOS({axis})," +
                    $"AIN({capture + _mcConfig.Ain.IndexStart})," +
                    $"AIN({capture2 + _mcConfig.Ain.IndexStart}))";

            }

            zmcaux.ZAux_Execute(_hMc, command, respon, 100);
            zmcaux.ZAux_Trigger(_hMc);
            Move(axis, speed, range);
            zmcaux.ZAux_Execute(_hMc, $"SCOPE_POS(OFF)", respon, 100);
            zmcaux.ZAux_Execute(_hMc, $"?SCOPE_POS", respon, 100);

            if (!int.TryParse(respon.ToString(), out var pCnt))
                throw new Exception("无法获取采样的数据点总数。");

            var startAin1 = _mcConfig.Scope.Depth;
            var startAin2 = _mcConfig.Scope.Depth * 2;

            var pBufX = new float[_mcConfig.Scope.Depth];
            var pBufY1 = new float[_mcConfig.Scope.Depth];
            var pBufY2 = new float[_mcConfig.Scope.Depth];

            zmcaux.ZAux_Direct_GetTable(_hMc, 0, pCnt, pBufX);
            zmcaux.ZAux_Direct_GetTable(_hMc, startAin1, pCnt, pBufY1);

            if (capture2 >= 0)
                zmcaux.ZAux_Direct_GetTable(_hMc, startAin2, pCnt, pBufY2);

            // AIN采样值所在的位置

            for (var i = 0; i < pCnt; i++)
            {
                point2Ds1.Add(new Point2D(pBufX[i], ConvertAdcRawToRealworld(adcParam1, pBufY1[i])));

                if (capture2 >= 0)
                    point2Ds2.Add(new Point2D(pBufX[i], ConvertAdcRawToRealworld(adcParam1, pBufY2[i])));
            }

            var distinctItems1 = point2Ds1.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
            scanResult = distinctItems1.ToArray();

            if (capture2 >= 0)
            {
                var distinctItems2 = point2Ds2.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
                scanResult2 = distinctItems2.ToArray();
            }
        }


        /// <summary>
        /// 停止所有轴移动。
        /// </summary>
        protected override void StopImpl()
        {
            int rtn;
            for (var i = 0; i < _maxAxis; i++)
            {
                rtn = zmcaux.ZAux_Direct_Single_Cancel(_hMc, i, 0);
                //CommandRtnCheck(rtn, "ZAux_Direct_Single_Cancel");
            }

        }

        protected override void EStopImpl()
        {
            var rtn = zmcaux.ZAux_Direct_Rapidstop(_hMc, 2);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_Rapidstop));
        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void DisposeImpl()
        {
            // 停止所有轴运动。
            EStopImpl();

            var rtn = zmcaux.ZAux_Close(_hMc);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Close));
        }


        #endregion

        private void CommandRtnCheck(int rtnValue, [CallerMemberName] string funcName = "")
        {
            var errorInfo = string.Empty;
            switch (rtnValue)
            {
                case 0:
                    return;
                case 217:
                    errorInfo = "控制器不支持或禁止的功能";
                    break;
                case 218:
                    errorInfo = "调用传递的参数错误";
                    break;
                case 272:
                    errorInfo = "子卡不存在";
                    break;
                case 282:
                    errorInfo = "不支持的功能";
                    break;
                case 1008:
                    errorInfo = "运动模块输入参数错误";
                    break;
                case 1009:
                    errorInfo = "运动中，无法操作";
                    break;
                case 1010:
                    errorInfo = "暂停等重复操作";
                    break;
                case 1012:
                    errorInfo = "当前运动不支持暂停";
                    break;
                case 1014:
                    errorInfo = "ATYPE不支持";
                    break;
                case 1015:
                    errorInfo = "ZCAN的ATYPE冲突";
                    break;
                case 2023:
                    errorInfo = "试图修改只读状态参数";
                    break;
                case 2024:
                    errorInfo = "数组越界";
                    break;
                case 2025:
                    errorInfo = "变量数操过控制器规格";
                    break;
                case 2026:
                    errorInfo = "数组数操过控制器规格";
                    break;
                case 2027:
                    errorInfo = "数组空间操过控制器规格";
                    break;
                case 6025:
                    errorInfo = "轴数超过";
                    break;
                case 20007:
                    errorInfo = "串口打开失败";
                    break;
                case 20008:
                    errorInfo = "网络打开失败";
                    break;
                default:
                    errorInfo = "请查看指令返回值详细说明";
                    break;
            }

            if (string.IsNullOrEmpty(errorInfo))
            {
                return;
            }

            throw new Exception($"{funcName} 功能异常，错误码： {rtnValue}, 异常信息：{errorInfo}");
        }


        private void ReadParamFile(string filePath, ref McConfig cardParam)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"unable to find the config file {filePath}");

            var jsonInfo = File.ReadAllText(filePath);
            try
            {
                cardParam = JsonConvert.DeserializeObject<McConfig>(jsonInfo);
            }
            catch (FormatException)
            {
                throw new Exception($"{filePath} ,配置文件加载异常");
            }

        }

        /// <summary>
        /// 应用配置文件对轴卡进行配置
        /// </summary>
        /// <param name="cardHandle"></param>
        /// <param name="cardParam"></param>
        private void ApplyConfig(IntPtr cardHandle, McConfig cardParam)
        {
            int rtn;
            foreach (var cfg in cardParam.Axes)
            {
                if (cfg.Control.Index < 0)
                {
                    break;
                }

                if (cfg.Control.AxisType > 0)
                {
                    rtn = zmcaux.ZAux_Direct_SetAtype(cardHandle, cfg.Control.Index, cfg.Control.AxisType);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetAtype in LoadParam Function");
                }

                if (cfg.Control.AxisType > -1)
                {
                    rtn = zmcaux.ZAux_Direct_SetInvertStep(cardHandle, cfg.Control.Index, cfg.Control.AxisType);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetInvertStep));
                }

                if (cfg.Control.Units > 0)
                {
                    rtn = zmcaux.ZAux_Direct_SetUnits(cardHandle, cfg.Control.Index, cfg.Control.Units);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetUnits in LoadParam Function");
                }

                if (cfg.Io.Org > -1)
                {
                    rtn = zmcaux.ZAux_Direct_SetDatumIn(cardHandle, cfg.Control.Index, cfg.Io.Org);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetDatumIn));

                    rtn = zmcaux.ZAux_Direct_SetInvertIn(cardHandle, cfg.Io.Org, cfg.Io.InvOrg ? 1 : 0);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetInvertIn));
                }

                if (cfg.Io.Pel > -1)
                {
                    rtn = zmcaux.ZAux_Direct_SetFwdIn(cardHandle, cfg.Control.Index, cfg.Io.Pel);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetFwdIn in LoadParam Function");

                    rtn = zmcaux.ZAux_Direct_SetInvertIn(cardHandle, cfg.Io.Pel, cfg.Io.InvPel ? 1 : 0);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetInvertIn));
                }

                if (cfg.Io.Nel > -1)
                {
                    rtn = zmcaux.ZAux_Direct_SetRevIn(cardHandle, cfg.Control.Index, cfg.Io.Nel);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetFwdIn in LoadParam Function");

                    rtn = zmcaux.ZAux_Direct_SetInvertIn(cardHandle, cfg.Io.Nel, cfg.Io.InvNel ? 1 : 0);
                    CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_SetInvertIn));
                }

                if (cfg.Io.IsNelAsDatum)
                {
                    rtn = zmcaux.ZAux_Direct_SetDatumIn(cardHandle, cfg.Control.Index, cfg.Io.Nel);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetDatumIn in LoadParam Function");
                }
                else
                {
                    rtn = zmcaux.ZAux_Direct_SetDatumIn(cardHandle, cfg.Control.Index, cfg.Io.Pel);
                    CommandRtnCheck(rtn, "ZAux_Direct_SetDatumIn in LoadParam Function");
                }
            }
        }

        private string SendBasicCommand(string command)
        {
            var resp = new StringBuilder();
            zmcaux.ZAux_Execute(_hMc, command, resp, 100);

            return resp.ToString();
        }

        /// <summary>
        /// 检查轴是否空闲
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="isCheckServoOn"></param>
        private void PreMove(int axis, bool isCheckServoOn = true)
        {
            var axisMoveStatus = 0;
            var rtn = zmcaux.ZAux_Direct_GetIfIdle(_hMc, axis, ref axisMoveStatus);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Direct_GetIfIdle));

            if (axisMoveStatus == 0)
            {
                throw new Exception($"轴[{axis}]正在运动。");
            }

            // 检查Servo-On是否打开
            if (isCheckServoOn)
            {
                var sta = ReadDOImpl(_mcConfig.Axes[axis].Io.ServoOn);

                if (sta == false)
                    throw new Exception($"轴[{axis}]未使能。");
            }

            // 检查急停开关是否被按下
            if (CheckEmbStatus())
                throw new Exception($"急停开关未释放。");

            // 清除AXIS_STOPREASON
            SendBasicCommand($"AXIS_STOPREASON({axis})=0");
        }

        private AxisConfig FindAxisConfig(int axis, McConfig cardParam)
        {
            var param = cardParam.Axes.FirstOrDefault(x => x.Control.Index == axis);
            if (param == null)
                throw new NullReferenceException($"无法在文件{_configFileAxis}中找到轴[{axis}]的配置参数。");

            return param;
        }

        /// <summary>
        /// 检查急停开关是否被按下
        /// </summary>
        /// <returns></returns>
        private bool CheckEmbStatus()
        {
            var respon = new StringBuilder();
            var ret = zmcaux.ZAux_Execute(_hMc, "?IsEMBPressed", respon, 100);
            CommandRtnCheck(ret, nameof(CheckEmbStatus));

            if (int.TryParse(respon.ToString(), out var status))
                return status == 0 ? false : true;
            else
                throw new Exception($"检查急停开关状态时错误，返回值{respon}格式错误。");
        }

        private double ConvertAdcRawToRealworld(AnalogInParamConfig param, double adcRaw)
        {
            if (param == null)
                return adcRaw;

            if (param.RangeLowMv >= param.RangeUpperMv)
                return adcRaw;

            if (param.MaxScale <= 0)
                return adcRaw;

            return (adcRaw / param.MaxScale) * (param.RangeUpperMv - param.RangeLowMv);
        }


        public void UnitTest(int targetAxis)
        {
            Init();
            ResetAlarm(targetAxis);
            ServoOn(targetAxis);
            SetAcceleration(targetAxis, _mcConfig.Axes[targetAxis].Home.Acc);
            SetDeceleration(targetAxis, _mcConfig.Axes[targetAxis].Home.Dec);
            Home(targetAxis, 
                _mcConfig.Axes[targetAxis].Home.HiSpeed, 
                _mcConfig.Axes[targetAxis].Home.CreepSpeed);

            SetAcceleration(targetAxis, _mcConfig.Axes[targetAxis].Motion.Acc);
            SetDeceleration(targetAxis, _mcConfig.Axes[targetAxis].Motion.Dec);
            Move(targetAxis, _mcConfig.Axes[targetAxis].Motion.Speed, 100000);
            Move(targetAxis, _mcConfig.Axes[targetAxis].Motion.Speed, 200000);

            StartFast1D(targetAxis, 400, 1, 50000, 0, out var pBuf);

            var sb = new StringBuilder();
            pBuf.ToList().ForEach(p => { sb.AppendLine($"{p.X}\t{p.Y}"); });

            var ss = sb.ToString();


            Move(targetAxis, _mcConfig.Axes[targetAxis].Motion.Speed, -100000);

            StartFast1D(
                axis: targetAxis,
                range: 100000,
                interval: 10,
                speed: 50000,
                capture: 1,
                scanResult: out var pBuf1,
                capture2: 0,
                scanResult2: out var pBuf2);


            var sb1 = new StringBuilder();
            var sb2 = new StringBuilder();
            pBuf1.ToList().ForEach(p => { sb1.AppendLine($"{p.X}\t{p.Y}"); });
            pBuf2.ToList().ForEach(p => { sb2.AppendLine($"{p.X}\t{p.Y}"); });

            var ss1 = sb1.ToString();
            var ss2 = sb2.ToString();


            ServoOff(targetAxis);

            for (var i = 0; i < 10; i++)
            {
                var val = ReadAI(i);
            }
        }
    }
}
