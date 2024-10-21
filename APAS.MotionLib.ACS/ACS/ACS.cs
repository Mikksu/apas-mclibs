using ACS.SPiiPlusNET;
using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Exceptions;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using APAS.CoreLib.Charting;
using APAS.McLib.Sdk.Core;
using AcsApi = ACS.SPiiPlusNET.Api;
using AcsAxis = ACS.SPiiPlusNET.Axis;

/*
 注意：

 1. 应用该模板时，请注意将命名空间更改为实际名称。
 2. 该类中的所有Childxxx()方法中，请勿阻塞式调用实际的运动控制器库函数，因为在APAS主程序中，可能会同时调用多个轴同步移动。
 3. 请保证所有的Childxxx()方法为线程安全。

*/

namespace APAS.MotionLib.ACS

{
    public class ACS : MotionControllerBase
    {
        #region Variables

        private const int MAX_AI_COUNT = 4;
        private readonly AcsApi _acs;
        private AcsAxis[] _axisArr = new AcsAxis[3]; 


        #endregion
        
        #region Constructors

        /// <summary>
        /// 注意：类名应为 “MotionController",请勿更改。
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public ACS(string portName, int baudRate, string config = "", ILog logger = null) 
            : base(portName, baudRate, config, logger)
        {
            _acs = new AcsApi();
            
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

            // Connect to the controller
            if(PortName == "SIMULATOR")
                _acs.OpenCommSimulator();
            else if(IPAddress.TryParse(PortName, out var ip))
                _acs.OpenCommEthernetTCP(ip.ToString(), BaudRate);
            else
                throw new ArgumentException($"IP地址格式错误。", nameof(PortName));

            //TODO 如何知道有几个轴？
            // Get the total number of the axes in the current configuration
            var axisCount = (int)_acs.GetAxesCount();
            _axisArr = new AcsAxis[axisCount + 1];
            for (var i = 0; i < axisCount; i++)
            {
                _axisArr[i] = (AcsAxis)i;
            }
            _axisArr[axisCount] = AcsAxis.ACSC_NONE;



            // 如果上次程序没有退出，可能还有正在运动的轴，
            // 先执行一次急停，确保左右轴均处于停止状态；否则下面的ChildServoOn异常。
            EStopImpl();

            //ApplyConfig(_hMc, _mcConfig);

            // Servo On 所有轴
            for (var i = 0; i < axisCount; i++)
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
            _acs.SetAcceleration((AcsAxis)axis, acc);
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void SetDecImpl(int axis, double dec)
        {
            PreMove(axis);
            _acs.SetDeceleration((AcsAxis)axis, dec);
        }

        /// <summary>
        /// 设置指定轴的急停减速度。
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dec"></param>
        protected override void SetEsDecImpl(int axis, double dec)
        {
            PreMove(axis);
            _acs.SetKillDeceleration((AcsAxis)axis, dec);
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
            //int axisMoveStatus = 0;
            //var homeParam = FindAxisConfig(axis, ref _mcConfig).Home;

            PreMove(axis);
            var homeProgBuf = ProgramBuffer.ACSC_BUFFER_1;

            var sta = _acs.GetProgramState(homeProgBuf);
            if ((sta & ProgramStates.ACSC_PST_COMPILED) == 0)
                throw new Exception("the homing program is not compiled.");

            _acs.RunBuffer(homeProgBuf, $"HOME_AXIS_{axis}");
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            var homeProgBuf = ProgramBuffer.ACSC_BUFFER_1;

            var sta = _acs.GetProgramState(homeProgBuf);
            var isHoming = (sta & ProgramStates.ACSC_PST_RUN) > 0;


            if (!isHoming)
            {
                // 确保程序执行完成
                _acs.WaitProgramEnd(homeProgBuf, -1);
                var isProgErr = _acs.GetProgramError(homeProgBuf);
                if (isProgErr == 0)
                {
                    // Home完成后可能会报 ACSC_SAFETY_PE错误，需要清除；否则Move函数在最后阶段检查Fault时会报错。
                    _acs.FaultClear((AcsAxis)axis);
                }
            }

            return !isHoming;
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
      
            PreMove(axis);

            _acs.SetVelocity((AcsAxis)axis, speed);
            _acs.ToPoint(MotionFlags.ACSC_AMF_RELATIVE, (AcsAxis)axis, distance);
        }

        protected override bool CheckMotionDoneImpl(int axis)
        {
            var isIdle = GetIfIdle(axis, out _);

            if (isIdle)
            {
                // 确保运动完成。
                _acs.WaitMotionEnd((AcsAxis)axis, 1000);
            }

            return isIdle;
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOnImpl(int axis)
        {
            PreMove(axis, false);
            _acs.Enable((AcsAxis)axis);
            _acs.WaitMotorEnabled((AcsAxis)axis, 1, 5000);
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOffImpl(int axis)
        {
            PreMove(axis, false);
            _acs.Disable((AcsAxis)axis);
            _acs.WaitMotorEnabled((AcsAxis)axis, 0, 5000);
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
            var pos = _acs.GetFPosition((AcsAxis)axis);
            return pos;
        }

        /// <summary>
        /// 更新指定轴状态。
        /// <para>注意：请在该函数调用RaiseAxisStateUpdatedEvent()函数，以通知APAS主程序当前轴的状态已更新。</para>
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override StatusInfo ReadStatusImpl(int axis)
        {
            // 注意:
            // 1. 读取完状态后请调用 RaiseAxisStatusUpdatedEvent 函数。
            // 2. 实例化 AxisStatusUpdatedArgs 时请传递所有参数。
            // RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(int.MinValue, double.NaN, false, false));

            List<AlarmInfo> alarms = new();
            var ax = (AcsAxis)axis;

            var isBusy = GetIfIdle(axis, out MotorStates state);
            var isHomed = GetIsHomedFlag(axis);
            var isInp = (state & MotorStates.ACSC_MST_INPOS) != 0;
            var isServoOn = (state & MotorStates.ACSC_MST_ENABLE) != 0;

            // 读取报警
            var motionErr = _acs.GetMotionError(ax);
            var errProg = _acs.GetProgramError(ProgramBuffer.ACSC_BUFFER_ALL);
            if (motionErr != 0)
            {
                var errStr = _acs.GetErrorString(motionErr);
                alarms.Add(new AlarmInfo(motionErr, errStr));
            }

            if (errProg != 0)
            {
                var errStr = _acs.GetErrorString(motionErr);
                alarms.Add(new AlarmInfo(errProg, errStr));
            }

            return new StatusInfo(isBusy, isInp, isHomed, isServoOn, alarms.ToArray());

        }

        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ResetAlarmImpl(int axis)
        {
            _acs.FaultClear((AcsAxis)axis);
        }


        #region Analog Controller

        /// <summary>
        /// 读取所有模拟输入端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override double[] ReadAIImpl()
        {
            var values = new List<double>();
            var ret = _acs.ReadVariableAsVector("AIN", from1: 0, to1: MAX_AI_COUNT - 1);

            if (ret is double[] vArr)
                return vArr;

            throw new Exception($"返回的数据格式错误，{ret}");
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAIImpl(int port)
        {
            //float val = 0.0f;
            //zmcaux.ZAux_Direct_GetAD(_hMc, _mcConfig.Ain.IndexStart + port, ref val);
            //var param = _mcConfig.Ain.Param.FirstOrDefault(x => x.Channel == port);
            //return ConvertAdcRawToRealworld(param, val);

            var ret = _acs.ReadVariable($"AIN{port}");
            if (ret is double volt)
                return volt;

            throw new Exception($"返回的数据格式错误，{ret}");
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
        /// <param name="catpure">反馈信号捕获端口</param>
        /// <param name="scanResult">扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int catpure, out Point2D[] scanResult)
        {
            Fast1DImpl(axis, range, interval, speed, catpure, out scanResult, -1, out _);
        }

        /// <summary>
        /// 执行双通道快速线性扫描。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="range">扫描范围</param>
        /// <param name="interval">第1路反馈信号采样间隔</param>
        /// <param name="speed">移动速度</param>
        /// <param name="catpure">反馈信号捕获端口</param>
        /// <param name="scanResult">第1路扫描结果列表（X:位置，Y:反馈信号）</param>
        /// <param name="catpure2">第2路反馈信号采样间隔</param>
        /// <param name="scanResult2">第2路扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int catpure, out Point2D[] scanResult, int catpure2, out Point2D[] scanResult2)
        {
            scanResult = null;
            scanResult2 = null;

            var varName = "Fast1DDataArray";
            var nVars = 2;  // 采集的数据种类
            var nSamples = 50000;   // 最大采样数

            _acs.ClearVariables();
            
            if (catpure2 < 0)
            {
                var mVarName = $"{varName}({nVars})({nSamples})";
                _acs.DeclareVariable(AcsplVariableType.ACSC_REAL_TYPE, mVarName);

                _acs.DataCollectionExt(DataCollectionFlags.ACSC_DCF_WAIT,
                    _axisArr[axis],
                    mVarName,
                    10000,
                    1,
                    $"FPOS({axis})\rAIN({catpure})");
            }
            else
            {
                nVars = 3;
                nSamples = 33000;
                var mVarName = $"{varName}({nVars})({nSamples})";
                
                _acs.DeclareVariable(AcsplVariableType.ACSC_REAL_TYPE, mVarName);

                _acs.DataCollectionExt(DataCollectionFlags.ACSC_DCF_WAIT,
                    _axisArr[axis],
                    mVarName,
                    10000,
                    1,
                    $"FPOS({axis})\rAIN({catpure})\rAIN({catpure2})");
            }

            
            Move(axis, speed, range);

            _acs.StopCollect();

            var point2Ds1 = new List<Point2D>();
            var point2Ds2 = new List<Point2D>();

            // 采样点的总数
            var sdcnRet = _acs.ReadVariable("S_DCN");
            if((double.TryParse(sdcnRet.ToString(), out var nBuffered)) == false)
                throw new Exception($"DataCollection返回的数组长度错误，{sdcnRet}");

            // 读 DC Buffer
            var ret = _acs.ReadVariableAsMatrix(varName);

            if ((ret is double[,] dcBuff) == false)
                throw new Exception($"DataCollection返回的数组错误，{ret.GetType()}");

            for (int i = 0; i < nBuffered; i++)
            {
                point2Ds1.Add(new Point2D(dcBuff[0, i], dcBuff[1, i]));

                if(catpure2 >= 0)
                    point2Ds2.Add(new Point2D(dcBuff[0, i], dcBuff[2, i]));
            }



            // 合并位置重复的数据点
            var distinctItems1 = point2Ds1.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
            scanResult = distinctItems1.ToArray();

            if (catpure2 >= 0)
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
            _acs.HaltM(_axisArr);

            foreach (var ax in _axisArr)
            {
                if (ax != AcsAxis.ACSC_NONE) 
                    _acs.WaitMotionEnd(ax, 1000);
            }
        }

        protected override void EStopImpl()
        {
            _acs.KillAll();

            foreach (var ax in _axisArr)
            {
                if (ax != AcsAxis.ACSC_NONE)
                    _acs.WaitMotionEnd(ax, 1000);
            }
        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void DisposeImpl()
        {
            // 停止所有轴运动。
            EStopImpl();
            _acs.CloseComm();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 检查轴是否空闲
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="isCheckServoOn"></param>
        private void PreMove(int axis, bool isCheckServoOn = true)
        {
            // 检查轴是否正在运动
            if (!GetIfIdle(axis, out var motorSta))
                throw new Exception($"轴[{axis}]正在运动。");

            // 检查轴是否使能
            if (isCheckServoOn)
            {
                if ((motorSta & MotorStates.ACSC_MST_ENABLE) == 0)
                {
                    throw new Exception($"轴[{axis}]未使能。");
                }
            }

            // 检查急停开关是否被按下
            if (CheckEmbStatus())
                throw new Exception($"急停开关未释放。");

        }

        /// <summary>
        /// 检查轴是否完成移动。
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="states"></param>
        /// <returns></returns>
        private bool GetIfIdle(int axis, out MotorStates states)
        {
            states = _acs.GetMotorState((AcsAxis)axis);
            return (states & MotorStates.ACSC_MST_MOVE) == 0;
        }

        private void GetAxisError(int axis)
        {
            var fault = _acs.GetMotorError((AcsAxis)axis);
            if (fault != 0)
                throw new Exception($"轴[{axis}]电机异常，{_acs.GetErrorString(fault)}。");

            fault = _acs.GetMotionError((AcsAxis)axis);

            // For the ACSPL+ Motion Termination Errors see page 862
            switch (fault)
            {
                case 5001: // The motion came to the final point and was successfully completed.
                    fault = 0;
                    break;

                case 5000:
                case 5002:
                case 5003:
                case 5004:
                case 5005:
                case 5006:
                case 5007:
                case 5008:
                    throw new StoppedByUserException($"运动终止，ErrCode {fault}");
            }

            if (fault != 0)
                throw new Exception($"轴[{axis}]运行异常，{_acs.GetErrorString(fault)}。");
        }

        private void CheckAxisStatus(int axis)
        {
            // 检查急停
            if (CheckEmbStatus())
                throw new Exception($"紧急停止");

            // 若非急停，检查轴状态
            GetAxisError(axis);
        }

        /// <summary>
        /// 检查急停开关是否被按下
        /// </summary>
        /// <returns></returns>
        private bool CheckEmbStatus()
        {
            var respon = _acs.ReadVariable("IsEMO", ProgramBuffer.ACSC_BUFFER_5);

            if (int.TryParse(respon.ToString(), out var status))
                return status != 0;
            else
                throw new Exception($"检查急停开关状态时错误，返回值{respon}格式错误。");
        }

        /// <summary>
        /// 检查指定的轴是否完成Home。
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        private bool GetIsHomedFlag(int axis)
        {
            // 系统变量MFLAGS(axis).#HOME指示Home完成状态。
            // 参考
            
            var homeBit = _acs.ReadVariable($"MFLAGS({axis}).#HOME");
            if(homeBit is int val)
                return val == 1;

            return false;
        }


        #endregion

        #region Unit Test Proxy

        public void UnitTestFast1D()
        {
            Init();
            ServoOn(0);
            StartFast1D(0, 100, 0.1, 20, 0, out var points, 1, out var points2);
        }

        public void UnitTestAnalog()
        {
            Init();

            var val = ReadAIImpl();

            var volt = ReadAIImpl(0);
            volt = ReadAIImpl(1);
            volt = ReadAIImpl(2);
            volt = ReadAIImpl(3);
        }

        public void UnitTestMotion(int axis)
        {
            Init();


            ServoOn(axis);

            if(GetIsHomedFlag(axis) == false)
                Home(axis, 0, 0);

            SetEsDeceleration(axis, 500);


            SetAcceleration(axis, 100);
            SetDeceleration(axis, 100);

            SetAcceleration(axis, 100);
            SetDeceleration(axis, 100);
            Move(axis, 200, 100);

            SetAcceleration(axis, 500);
            SetDeceleration(axis, 500);

            Task.Run(() =>
            {
                try
                {
                    Move(0, 100, -100);
                }
                catch (StoppedByUserException ex)
                {
                    Debug.WriteLine($"Stopped by user, {ex.Message}");
                }
            });


            Thread.Sleep(200);

            Stop();


            for (int i = 0; i < 100; i++)
            {
                Move(0, 30, 0.0001);
            }
        }

        #endregion
    }
}
