using ACS.SPiiPlusNET;
using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Exceptions;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APAS.CoreLib.Charting;
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

        private readonly string _configFileMc, _configFileAxis;

        private AcsApi _acs;
        private AcsAxis[] _axisArr; 

        private CancellationTokenSource _cts;

        #endregion
        
        #region Constructors

        /// <summary>
        /// 注意：类名应为 “MotionController",请勿更改。
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public ACS(string portName, int baudRate, string config, ILog logger = null) : base(portName, baudRate, logger)
        {
            var configs = config.Split(',');
            if (configs.Length == 2)
            {
                _configFileMc = configs[0];
                _configFileAxis = configs[1];
            }

            //// 加载自定义配置
            //string paramPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _configFileAxis);
            //ReadParamFile(paramPath, ref _mcConfig);

            _acs = new AcsApi();
            
            //TODO 此处初始化控制器参数；如果下列参数动态读取，则可在ChildInit()函数中赋值。
            AxisCount = 3; // 最大轴数
            MaxAnalogInputChannels = 4; // 最大模拟量输入通道数
            MaxAnalogOutputChannels = 0; // 最大模拟量输出通道数
            MaxDigitalInputChannels = 0; // 最大数字量输入通道数
            MaxDigitalOutputChannels = 0; // 最大数字量输出通道数
        }

        #endregion

        #region Overrided Methods

        /// <summary>
        /// 初始化指定轴卡。
        /// </summary>
        protected override void ChildInit()
        {
            //TODO 1.初始化运动控制器对象，例如凌华轴卡、固高轴卡等。
            // 例如：初始化固高轴卡：gts.mc.GT_Open(portName, 1);

            //TODO 2.读取控制器固件版本，并赋值到属性 FwVersion

            //TODO 3.读取每个轴的信息，构建 InnerAxisInfoCollection，包括轴号和固件版本号。
            // 注意：InnerAxisInfoCollection 已在基类的构造函数中初始化
            // 例如： InnerAxisInfoCollection.Add(new AxisInfo(1, new Version(1, 0, 0)));

            //TODO 4.需要完成函数 ChildUpdateStatus()，否则会报NotImplementException异常。

            /* var rtn = zmcaux.ZAux_SearchEth(PortName, 100);
             CommandRtnCheck(rtn, "ZAux_SearchEth");*/

            // Connect to the controller
            _acs.OpenCommEthernetTCP(PortName, BaudRate);

            // Get the total number of the axes in the current configuration
            //AxisCount = (int)_acs.GetAxesCount();
            //TODO 如何知道有几个轴？
            AxisCount = 3;
            _axisArr = new AcsAxis[AxisCount + 1];
            for (int i = 0; i < AxisCount; i++)
			{
				_axisArr[i] = (AcsAxis)i;
			}
            _axisArr[AxisCount] = AcsAxis.ACSC_NONE;



            // 如果上次程序没有退出，可能还有正在运动的轴，
            // 先执行一次急停，确保左右轴均处于停止状态；否则下面的ChildServoOn异常。
            ChildEmergencyStop();

            //ApplyConfig(_hMc, _mcConfig);

            // Servo On 所有轴
            for (var i = 0; i < AxisCount; i++)
                ChildServoOn(i);

            StartBackgroundTask();
        }

        /// <summary>
        /// 设置指定轴的加速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="acc">加速度值</param>
        protected override void ChildSetAcceleration(int axis, double acc)
        {
            AxisMovePreparation(axis);

            _acs.SetAcceleration((AcsAxis)axis, acc);
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void ChildSetDeceleration(int axis, double dec)
        {
            AxisMovePreparation(axis);

            _acs.SetDeceleration((AcsAxis)axis, dec);
        }

        /// <summary>
        /// 设置指定轴的急停减速度。
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dec"></param>
		protected override void ChildSetEsDeceleration(int axis, double dec)
		{
            AxisMovePreparation(axis);

            _acs.SetKillDeceleration((AcsAxis)axis, dec);
        }


		/// <summary>
		/// 指定轴回机械零点。
		/// </summary>
		/// <param name="axis">轴号</param>
		/// <param name="hiSpeed">快速找机械原点的速度值。如不适用请忽略。</param>
		/// <param name="creepSpeed">找到机械原点后返回零位的爬行速度。如不适用请忽略。</param>
		protected override void ChildHome(int axis, double hiSpeed, double creepSpeed)
        {
            /*
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
             */
            int rtn;
            //int axisMoveStatus = 0;
            //var homeParam = FindAxisConfig(axis, ref _mcConfig).Home;

            AxisMovePreparation(axis);


            ProgramBuffer homeProgBuf = ProgramBuffer.ACSC_BUFFER_1;

			{
                var sta = _acs.GetProgramState(homeProgBuf);
                if ((sta & ProgramStates.ACSC_PST_COMPILED) == 0)
                    throw new Exception("the homing program is not compiled.");
			}

            _acs.RunBuffer(homeProgBuf, $"HOME_AXIS_{axis}");
            Thread.Sleep(100); // delay to be sure that the program has been run.

            while (true)
			{
                var sta = _acs.GetProgramState(homeProgBuf);
                if ((sta & ProgramStates.ACSC_PST_RUN) > 0)
                    Thread.Sleep(100);
                else 
                    break;

                var pos = ChildUpdateAbsPosition(axis);
                RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, pos));
            }

            _acs.WaitProgramEnd(homeProgBuf, -1);

            var progErr = _acs.GetProgramError(homeProgBuf);
            if (progErr != 0)
            {
                var progErrStr = _acs.GetErrorString(progErr);
                throw new Exception(progErrStr);
            }

            // Home完成后可能会报 ACSC_SAFETY_PE错误，需要清除；否则Move函数在最后阶段检查Fault时会报错。
            _acs.FaultClear((AcsAxis)axis);

            RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, 0, true));

        }

        /// <summary>
        /// 移动指定轴（相对移动模式）。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="speed">移动速度。该速度根据APAS主程序的配置文件计算得到。计算方法为MaxSpeed * 速度百分比。</param>
        /// <param name="distance">相对移动的距离。该距离已被APAS主程序转换为轴卡对应的实际单位。例如对于脉冲方式，
        /// 该值已转换为步数；对于伺服系统，该值已转换为实际距离。</param>
        /// <param name="fastMoveRequested">是否启用快速移动模式。如不适用请忽略。</param>
        /// <param name="microstepRate">当启用快速移动模式时的驱动器细分比值。如不适用请忽略。</param>
        protected override void ChildMove(int axis, double speed, double distance,
            bool fastMoveRequested = false, double microstepRate = 0)
        {
            /*
             * 耗时操作。当执行操作时，请轮询轴状态，并调用 RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, xxx)); 
             * 以实时刷新UI上的位置。       
            */
      
            AxisMovePreparation(axis);

            _acs.SetVelocity((AcsAxis)axis, speed);
            _acs.ToPoint(MotionFlags.ACSC_AMF_RELATIVE, (AcsAxis)axis, distance);

           // Thread.Sleep(10);
            double currPos = 0;

            do
            {
                //var sta = _acs.GetMotorState((AcsAxis)axis);
                // if ((sta & MotorStates.ACSC_MST_MOVE) == 0 && (sta & MotorStates.ACSC_MST_ACC) == 0)
                //     break;

                //var sta = _acs.GetMotorState((AcsAxis)axis);

                //if ((sta & MotorStates.ACSC_MST_ACC) == 0 
                //    && (sta & MotorStates.ACSC_MST_MOVE) == 0
                //    && (sta & MotorStates.ACSC_MST_INPOS) != 0)
                //    break;
                if (CheckMotorIdle(axis, out var _))
                    break;

                currPos = ChildUpdateAbsPosition(axis);
                // 背景线程中同时也在刷新绝对坐标，此处可以不刷新；
                // 增加该行代码可提高UI上刷新坐标的速度。
                RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, currPos));
                Thread.Sleep(10);
            } while (true);

            // 确保运动完成。
            _acs.WaitMotionEnd((AcsAxis)axis, 1000);

            currPos = ChildUpdateAbsPosition(axis);
            RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, (double)currPos));

            CheckAxisStatus(axis);
        }


        /// <summary>
        /// 移动指定轴到绝对位置（绝对移动模式）。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="speed">移动速度</param>
        /// <param name="position">绝对目标位置</param>
        /// <param name="fastMoveRequested">是否启用快速移动模式。如不适用请忽略。</param>
        /// <param name="microstepRate">当启用快速移动模式时的驱动器细分比值。如不适用请忽略。</param>
        protected override void ChildMoveAbs(int axis, double speed, double position, bool fastMoveRequested = false,
            double microstepRate = 0)
        {
            AxisMovePreparation(axis);

            _acs.SetVelocity((AcsAxis)axis, speed);
            _acs.ToPoint(0, (AcsAxis)axis, position);

            Thread.Sleep(10);

            double currPos = 0;

            do
            {
                //var sta = _acs.GetMotorState((AcsAxis)axis);
                //if ((sta & MotorStates.ACSC_MST_INPOS) > 0)
                //    break;

                //var sta = _acs.GetMotorState((AcsAxis)axis);

                //if ((sta & MotorStates.ACSC_MST_ACC) == 0
                //   && (sta & MotorStates.ACSC_MST_MOVE) == 0
                //   && (sta & MotorStates.ACSC_MST_INPOS) != 0)
                //    break;

                if (CheckMotorIdle(axis, out var _))
                    break;

                currPos = ChildUpdateAbsPosition(axis);
                // 背景线程中同时也在刷新绝对坐标，此处可以不刷新；
                // 增加该行代码可提高UI上刷新坐标的速度。
                RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, (double)currPos));
                Thread.Sleep(10);
            } while (true);

            // 确保运动完成。
            _acs.WaitMotionEnd((AcsAxis)axis, 1000);

            currPos = ChildUpdateAbsPosition(axis);
            RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, (double)currPos));

            CheckAxisStatus(axis);
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildServoOn(int axis)
        {
            AxisMovePreparation(axis, false);
            _acs.Enable((AcsAxis)axis);
            _acs.WaitMotorEnabled((AcsAxis)axis, 1, 5000);
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildServoOff(int axis)
        {
            AxisMovePreparation(axis, false);
            _acs.Disable((AcsAxis)axis);
            _acs.WaitMotorEnabled((AcsAxis)axis, 0, 5000);
        }

        /// <summary>
        /// 读取最新的绝对位置。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <returns>最新绝对位置</returns>
        protected override double ChildUpdateAbsPosition(int axis)
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
        protected override void ChildUpdateStatus(int axis)
        {
            // 注意:
            // 1. 读取完状态后请调用 RaiseAxisStatusUpdatedEvent 函数。
            // 2. 实例化 AxisStatusUpdatedArgs 时请传递所有参数。
            // RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(int.MinValue, double.NaN, false, false));

            AlarmInfo alarm = null;
            var ax = (AcsAxis)axis;
            

            var isHomed = GetIsHomedFlag(axis);
            var absPos = ChildUpdateAbsPosition(axis);
            var motorSta = _acs.GetMotorState(ax);
            var isServoOn = ((motorSta & MotorStates.ACSC_MST_ENABLE) > 0);

            if (!isServoOn)
            {
                var errCode = _acs.GetMotionError(ax);
                if (errCode != 0)
                {
                    var errStr = _acs.GetErrorString(errCode);
                    alarm = new AlarmInfo(errCode, errStr);
                }
            }
            else
            {
                var errCode = _acs.GetMotionError(ax);
                if (errCode != 0)
                {
                    var errStr = _acs.GetErrorString(errCode);
                    alarm = new AlarmInfo(errCode, errStr);
                }
            }
            
            RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(axis, absPos, isHomed, isServoOn, alarm));
        }

        /// <summary>
        /// 更新所有轴状态。
        /// <see cref="ChildUpdateStatus(int)"/>
        /// </summary>
        protected override void ChildUpdateStatus()
        {
            // 注意:
            // 1. 读取完状态后请循环调用 RaiseAxisStatusUpdatedEvent 函数，
            //    例如对于 8 轴轴卡，请调用针对8个轴调用 8 次 RaiseAxisStatusUpdatedEvent 函数。
            // 2. 实例化 AxisStatusUpdatedArgs 时请传递所有参数。
            //// RaiseAxisStatusUpdatedEvent(new AxisStatusUpdatedArgs(int.MinValue, double.NaN, false, false));
            // 检查IsHomed状态
           /* var isHomed = new byte[AxisCount];
            var rtn = zmcaux.ZAux_Modbus_Get0x(_hMc, (ushort)0, (ushort)AxisCount, isHomed);
            CommandRtnCheck(rtn, nameof(zmcaux.ZAux_Modbus_Get0x));*/
            
            for (var i = 0; i < AxisCount; i++)
            {
                ChildUpdateStatus(i);
            }
        }
        
        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildResetFault(int axis)
        {
            _acs.FaultClear((AcsAxis)axis);
        }


        #region IO Controller

        /// <summary>
        /// 设置指定数字输出端口的状态。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="isOn">是否设置为有效电平</param>
        protected override void ChildSetDigitalOutput(int port, bool isOn)
        {
            throw new NotSupportedException();

        }

        /// <summary>
        /// 读取指定数字输出端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ChildReadDigitalOutput(int port)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取所有数字输出端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override IReadOnlyList<bool> ChildReadDigitalOutput()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取指定数字输入端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ChildReadDigitalInput(int port)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取所有数字输入端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override IReadOnlyList<bool> ChildReadDigitalInput()
        {
            throw new NotSupportedException();
        }


        #endregion

        #region Analog Controller

        /// <summary>
        /// 读取所有模拟输入端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override IReadOnlyList<double> ChildReadAnalogInput()
        {
            List<double> values = new List<double>();

            var ret = _acs.ReadVariableAsVector("AIN", from1: 0, to1: MaxAnalogInputChannels - 1);

            if (ret is double[] vArr)
                return vArr;

            throw new Exception($"返回的数据格式错误，{ret}");
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ChildReadAnalogInput(int port)
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

        /// <summary>
        /// 读取所有模拟输出端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override IReadOnlyList<double> ChildReadAnalogOutput()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ChildReadAnalogOutput(int port)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 设置指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="value">电压值</param>
        protected override void ChildSetAnalogOutput(int port, double value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 打开指定模拟输出端口的输出。
        /// </summary>
        /// <param name="port">端口号</param>
        protected override void ChildAnalogOutputOn(int port)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 关闭指定模拟输出端口的输出。
        /// </summary>
        /// <param name="port">端口号</param>
        protected override void ChildAnalogOutputOff(int port)
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
        protected override void ChildAutoTouch(int axis, int analogInputPort, double vth, double distance, double speed)
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
        /// <param name="analogCapture">反馈信号捕获端口</param>
        /// <param name="scanResult">扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void ChildStartFast1D(int axis, double range, double interval, double speed,
            int analogCapture, out IEnumerable<Point2D> scanResult)
        {
           ChildStartFast1D(axis, range, interval, speed, analogCapture, out scanResult, -1, out _);
        }

        /// <summary>
        /// 执行双通道快速线性扫描。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="range">扫描范围</param>
        /// <param name="interval">第1路反馈信号采样间隔</param>
        /// <param name="speed">移动速度</param>
        /// <param name="analogCapture">反馈信号捕获端口</param>
        /// <param name="scanResult">第1路扫描结果列表（X:位置，Y:反馈信号）</param>
        /// <param name="analogCapture2">第2路反馈信号采样间隔</param>
        /// <param name="scanResult2">第2路扫描结果列表（X:位置，Y:反馈信号）</param>
        protected override void ChildStartFast1D(int axis, double range, double interval, double speed,
            int analogCapture, out IEnumerable<Point2D> scanResult, int analogCapture2, out IEnumerable<Point2D> scanResult2)
        {
            scanResult = null;
            scanResult2 = null;

            var varName = "Fast1DDataArray";
            var nVars = 2;  // 采集的数据种类
            var nSamples = 50000;   // 最大采样数

            _acs.ClearVariables();
			
            if (analogCapture2 < 0)
            {
                var mVarName = $"{varName}({nVars})({nSamples})";
                _acs.DeclareVariable(AcsplVariableType.ACSC_REAL_TYPE, mVarName);

                _acs.DataCollectionExt(DataCollectionFlags.ACSC_DCF_WAIT,
                    _axisArr[axis],
                    mVarName,
                    10000,
                    1,
                    $"FPOS({axis})\rAIN({analogCapture})");
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
                    $"FPOS({axis})\rAIN({analogCapture})\rAIN({analogCapture2})");
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

                if(analogCapture2 >= 0)
                    point2Ds2.Add(new Point2D(dcBuff[0, i], dcBuff[2, i]));
            }



            // 合并位置重复的数据点
			var distinctItems1 = point2Ds1.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
			scanResult = distinctItems1;

			if (analogCapture2 >= 0)
			{
				var distinctItems2 = point2Ds2.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
				scanResult2 = distinctItems2;
			}


			//if (!int.TryParse(respon.ToString(), out var pCnt))
			//    throw new Exception("无法获取采样的数据点总数。");

			//var startAin1 = _mcConfig.Scope.Deepth;
			//var startAin2 = _mcConfig.Scope.Deepth * 2;

			//var pBufX = new float[_mcConfig.Scope.Deepth];
			//var pBufY1 = new float[_mcConfig.Scope.Deepth];
			//var pBufY2 = new float[_mcConfig.Scope.Deepth];

			//zmcaux.ZAux_Direct_GetTable(_hMc, 0, pCnt, pBufX);
			//zmcaux.ZAux_Direct_GetTable(_hMc, startAin1, pCnt, pBufY1);

			//if(analogCapture2 >=0)
			//    zmcaux.ZAux_Direct_GetTable(_hMc, startAin2, pCnt, pBufY2);

			// AIN采样值所在的位置

			//for (var i = 0; i < pCnt; i++)
			//{
			//    point2Ds1.Add(new Point2D(pBufX[i], ConvertAdcRawToRealworld(adcParam1, pBufY1[i])));

			//    if (analogCapture2 >= 0)
			//        point2Ds2.Add(new Point2D(pBufX[i], ConvertAdcRawToRealworld(adcParam1, pBufY2[i])));
			//}

			//var distinctItems1 = point2Ds1.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
			//scanResult = distinctItems1;

			//if (analogCapture2 >= 0)
			//{
			//    var distinctItems2 = point2Ds2.GroupBy(p => p.X).Select(p => p.First()).OrderBy(p => p.X);
			//    scanResult2 = distinctItems2;
			//}
		}

        /// <summary>
        /// 执行快速盲扫。
        /// </summary>
        /// <param name="hAxis">水平轴轴号</param>
        /// <param name="vAxis">垂直轴轴号</param>
        /// <param name="range">扫描区域（正方形）的边长</param>
        /// <param name="gap">扫描螺旋线路的间隔</param>
        /// <param name="interval">每条扫描线上反馈信号采样间隔</param>
        /// <param name="hSpeed">水平轴扫描速度</param>
        /// <param name="vSpeed">垂直轴扫描速度</param>
        /// <param name="analogCapture">反馈信号捕获端口</param>
        /// <param name="scanResult">扫描结果列表（X:水平轴坐标，Y:垂直轴坐标，Z:反馈信号）</param>
        protected override void ChildStartBlindSearch(int hAxis, int vAxis, double range, double gap,
            double interval, double hSpeed, double vSpeed, int analogCapture, out IEnumerable<Point3D> scanResult)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 停止所有轴移动。
        /// </summary>
        protected override void ChildStop()
        {
            _acs.HaltM(_axisArr);

            //! 注意此处不要用_axisArr.Lenght循环，因为_axisArr中的最后一个元素不是轴号。
			for (int i = 0; i < AxisCount; i++)
			{
                _acs.WaitMotionEnd(_axisArr[i], 1000);
			}

        }

        protected override void ChildEmergencyStop()
        {
            _acs.KillAll();

            //! 注意此处不要用_axisArr.Lenght循环，因为_axisArr中的最后一个元素不是轴号。
            for (int i = 0; i < AxisCount; i++)
            {
                _acs.WaitMotionEnd(_axisArr[i], 1000);
            }

        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void ChildDispose()
        {
            // kill the background task
            _cts?.Cancel();
            Thread.Sleep(1000);

            // 停止所有轴运动。
            ChildEmergencyStop();

            _acs.CloseComm();
        }

        /// <summary>
        /// 检查移动速度。
        /// <para>如无需检查，请保持该函数为空。</para>
        /// </summary>
        /// <param name="speed">速度</param>
        protected override void CheckSpeed(double speed)
        {

        }

        /// <summary>
        /// 检查控制器状态。
        /// </summary>
        protected override void CheckController()
        {
            base.CheckController(); // 请勿删除该行。
        }


		#endregion

		#region Private Methods

        /// <summary>
        /// 检查轴是否空闲
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="isCheckServoOn"></param>
        private void AxisMovePreparation(int axis, bool isCheckServoOn = true)
        {
            // 检查轴是否正在运动
            if (!CheckMotorIdle(axis, out var motorSta))
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
        /// 检查轴（电机）是否处于停止状态
        /// </summary>
        /// <returns></returns>
        private bool CheckMotorIdle(int axis, out MotorStates states)
		{
            states = _acs.GetMotorState((AcsAxis)axis);
            return (states & MotorStates.ACSC_MST_MOVE) == 0;
        }

        /// <summary>
        /// 检查急停开关是否被按下
        /// </summary>
        /// <returns></returns>
        private bool CheckEmbStatus()
        {
            var respon = _acs.ReadVariable("IsEmbPressed", ProgramBuffer.ACSC_NONE);

            if (int.TryParse(respon.ToString(), out var status))
                return status == 0 ? false : true;
            else
                throw new Exception($"检查急停开关状态时错误，返回值{respon}格式错误。");
        }

        private void StartBackgroundTask()
        {
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            Task.Run(() =>
            {
                Thread.Sleep(1000);

                while (true)
                {
                    UpdateStatus();
                    Thread.Sleep(100);

                    if (ct.IsCancellationRequested)
                        break;
                }
            });
        }

        private bool GetIsHomedFlag(int axis)
		{
            var mflags = _acs.ReadVariable("MFLAGS", from1: axis, to1: axis);

            var isHomed = false;
            if (uint.TryParse(mflags.ToString(), out var ff))
                isHomed = ((ff & (0x1 << 3)) > 0);

            return isHomed;
        }


		#endregion

		#region Unit Test

        public void UnitTestFast1D()
		{
            Init();
            ServoOn(0);
            MoveAbs(0, 100, 0);
            StartFast1D(0, 100, 0.1, 20, 0, out var points, 1, out var points2);
		}

        public void UnitTestAnalog()
		{
            Init();

            var val = ReadAnalogInput();

            var volt = ReadAnalogInput(0);
            volt = ReadAnalogInput(1);
            volt = ReadAnalogInput(2);
            volt = ReadAnalogInput(3);
		}

		public void UnitTestMotion()
		{
            Init();


            ServoOn(0);

            if(GetIsHomedFlag(0) == false)
                Home(0, 0, 0);

            SetEsDeceleration(0, 500);


            SetAcceleration(0, 100);
            SetDeceleration(0, 100);


            MoveAbs(0, 500, 0);

            SetAcceleration(0, 100);
            SetDeceleration(0, 100);
            Move(0, 200, 100);

            SetAcceleration(0, 500);
            SetDeceleration(0, 500);

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
