using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Core;
using APAS.McLib.Sdk.Exceptions;
using log4net;


//！ 应用该模板时，请注意将命名空间更改为实际名称。
namespace APAS.McLib.Virtual
{
    public class VirtualMc : MotionControllerBase
    {
        #region Variables

        private bool isStopRequested = false;

        private readonly bool[] _fakeDi, _fakeDo;
        private readonly double[] _fakeAi, _fakeAo;

        private readonly double[] _fakeAcc, _fakeDec;
        private readonly double[] _fakeAbsPosition, _fakeAbsPositionJittered;
        private readonly bool[] _fakeIsHomed;

        private CancellationTokenSource _cts;
        private readonly object _myLocker = new object();

        private CancellationTokenSource _ctsJog;

        #endregion

        #region Constructors

        /// <summary>
        /// 注意：类名应为 “MotionController",请勿更改。
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        public VirtualMc(string portName, int baudRate, string config, ILog logger) : base(portName,
            baudRate, config, logger)
        {
            //TODO 此处初始化控制器参数；如果下列参数动态读取，则可在ChildInit()函数中赋值。
            AxisCount = 32; // 最大轴数
            MaxAnalogInputChannels = 32; // 最大模拟量输入通道数
            MaxAnalogOutputChannels = 32; // 最大模拟量输出通道数
            MaxDigitalInputChannels = 32; // 最大数字量输入通道数
            MaxDigitalOutputChannels = 32; // 最大数字量输出通道数

            _fakeDi = new bool[MaxDigitalInputChannels];
            _fakeDo = new bool[MaxDigitalOutputChannels];
            _fakeAi = new double[MaxAnalogInputChannels];
            _fakeAo = new double[MaxAnalogOutputChannels];

            _fakeAcc = new double[AxisCount];
            _fakeDec = new double[AxisCount];

            _fakeAbsPosition = new double[AxisCount];
            _fakeAbsPositionJittered = new double[AxisCount];
            _fakeIsHomed = new bool[AxisCount];
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
            for (var i = 0; i < AxisCount; i++)
            {
                _fakeAbsPosition[i] = int.MinValue;
                InnerAxisInfoCollection.Add(new AxisInfo(i, new Version(0, 0, 1)));
            }

            StartBackgroundTask();
        }

        /// <summary>
        /// 设置指定轴的加速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="acc">加速度值</param>
        protected override void ChildSetAcceleration(int axis, double acc)
        {
            _fakeAcc[axis] = acc;
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void ChildSetDeceleration(int axis, double dec)
        {
            _fakeDec[axis] = dec;
        }

        protected override void ChildSetEsDeceleration(int axis, double dec)
        {
           // ignore
        }

        /// <summary>
        /// 指定轴回机械零点。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="hiSpeed">快速找机械原点的速度值。如不适用请忽略。</param>
        /// <param name="creepSpeed">找到机械原点后返回零位的爬行速度。如不适用请忽略。</param>
        protected override void ChildHome(int axis, double hiSpeed, double creepSpeed)
        {
            isStopRequested = false;
            _fakeIsHomed[axis] = false;

            // 随机Home过程需要的时长
            var r = new Random();
            var duration = r.NextDouble() * 3000; // 最大5s完成Home
            var timeFly = 0;
            while (timeFly < duration)
            {
                _fakeAbsPosition[axis] -= r.NextDouble() * 100;
                timeFly += 10;
                RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPosition[axis], false, true));

                Thread.Sleep(10);

                if (isStopRequested)
                    throw new StoppedByUserException();
            }

            _fakeAbsPosition[axis] = 0;
            _fakeIsHomed[axis] = true;

            RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPosition[axis], true, true));
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
            isStopRequested = false;
            var step = Math.Abs(speed / 10) * Math.Sign(distance);
            var distMoved = 0.0;

            while (true)
            {
                if (Math.Abs(distance - distMoved) > Math.Abs(step))
                {
                    _fakeAbsPosition[axis] += step;
                    distMoved += step;
                    RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPosition[axis]));
                }
                else
                {
                    _fakeAbsPosition[axis] += distance - distMoved;
                    RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPosition[axis]));
                    break;
                }

                if (isStopRequested)
                    throw new StoppedByUserException();

                Thread.Sleep(10);
            }
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
            throw new NotImplementedException();
        }

        /// <summary>
        /// 以Jog模式运动指定的轴。
        /// </summary>
        /// <param name="axis"></param>
        /// <param name="dir"></param>
        /// <param name="speed"></param>
        /// <param name="acc"></param>
        /// <param name="dec"></param>
        
        protected override void ChildJogStart(int axis, JogDir dir, double speed, double? acc = null, double? dec = null)
        {
            lock (_myLocker)
            {
                if(_ctsJog == null)
                    _ctsJog = new CancellationTokenSource();
            }

            var ct = _ctsJog.Token;

            var step = Math.Abs(speed / 10) * (dir == JogDir.NEGATIVE ? -1.0d : 1.0d);

            Task.Run(() =>
            {
                while (true)
                {
                    _fakeAbsPosition[axis] += step;
                    RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPosition[axis]));

                    Thread.Sleep(10);

                    if (ct.IsCancellationRequested)
                        throw new StoppedByUserException();
                }
            }, ct);
        }

        protected override void ChildJogStop(int axis)
        {
            lock (_myLocker)
            {
                _ctsJog?.Cancel();
            }
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildServoOn(int axis)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildServoOff(int axis)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 读取最新的绝对位置。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <returns>最新绝对位置</returns>
        protected override double ChildUpdateAbsPosition(int axis)
        {
            RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPositionJittered[axis]));
            return _fakeAbsPositionJittered[axis];
        }

        /// <summary>
        /// 更新指定轴状态。
        /// <para>注意：请在该函数调用RaiseAxisStateUpdatedEvent()函数，以通知APAS主程序当前轴的状态已更新。</para>
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildUpdateStatus(int axis)
        {
            // 注意:
            // 1. 读取完状态后请调用 RaiseAxisStateUpdatedEvent 函数。
            // 2. 实例化 AxisStatusArgs 时请传递所有参数。
            //// RaiseAxisStateUpdatedEvent(new AxisStatusArgs(int.MinValue, double.NaN, false, false));

            RaiseAxisStateUpdatedEvent(new AxisStatusArgs(axis, _fakeAbsPositionJittered[axis], _fakeIsHomed[axis]));
        }

        /// <summary>
        /// 更新所有轴状态。
        /// <see cref="ChildUpdateStatus(int)"/>
        /// </summary>
        protected override void ChildUpdateStatus()
        {
            // 注意:
            // 1. 读取完状态后请循环调用 RaiseAxisStateUpdatedEvent 函数，
            //    例如对于 8 轴轴卡，请调用针对8个轴调用 8 次 RaiseAxisStateUpdatedEvent 函数。
            // 2. 实例化 AxisStatusArgs 时请传递所有参数。
            //// RaiseAxisStateUpdatedEvent(new AxisStatusArgs(int.MinValue, double.NaN, false, false));

            for (var i = 0; i < AxisCount; i++)
                RaiseAxisStateUpdatedEvent(new AxisStatusArgs(i, _fakeAbsPosition[i]));
        }


        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ChildResetFault(int axis)
        {
           
        }


        #region IO Controller

        /// <summary>
        /// 设置指定数字输出端口的状态。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="isOn">是否设置为有效电平</param>
        protected override void ChildSetDigitalOutput(int port, bool isOn)
        {
            if (port >= 0 && port < MaxDigitalOutputChannels)
            {
                _fakeDo[port] = isOn;
            }
        }

        /// <summary>
        /// 读取指定数字输出端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ChildReadDigitalOutput(int port)
        {
            if (port >= 0 && port < MaxDigitalOutputChannels)
            {
                return _fakeDo[port];
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 读取所有数字输出端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override IReadOnlyList<bool> ChildReadDigitalOutput()
        {
            return _fakeDo;
        }

        /// <summary>
        /// 读取指定数字输入端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ChildReadDigitalInput(int port)
        {
            if (port >= 0 && port < MaxDigitalInputChannels)
            {
                return _fakeDi[port];
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 读取所有数字输入端口。
        /// </summary>
        /// <returns>端口状态列表。True表示端口输出为有效电平。</returns>
        protected override IReadOnlyList<bool> ChildReadDigitalInput()
        {
            return _fakeDi;
        }

        #endregion

        #region Analog Controller

        /// <summary>
        /// 读取所有模拟输入端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override IReadOnlyList<double> ChildReadAnalogInput()
        {
            return _fakeAi;
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ChildReadAnalogInput(int port)
        {
            if (port >= 0 && port < MaxAnalogInputChannels)
            {
                return _fakeAi[port];
            }
            else
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// 读取所有模拟输出端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override IReadOnlyList<double> ChildReadAnalogOutput()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 读取指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ChildReadAnalogOutput(int port)
        {
            throw new NotImplementedException();
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
            throw new NotSupportedException();
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
            int analogCapture,
            out IEnumerable<Point2D> scanResult, int analogCapture2, out IEnumerable<Point2D> scanResult2)
        {
            throw new NotSupportedException();
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
            isStopRequested = true;
        }

        /// <summary>
        /// 紧急停止所有轴。
        /// </summary>
        protected override void ChildEmergencyStop()
        {
            isStopRequested = true;
        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void ChildDispose()
        {
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
            // 请勿删除该行。
            base.CheckController();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Start a background task to simulate the random IO status, servo position feedback jitter, etc..
        /// </summary>
        private void StartBackgroundTask()
        {
            Logger.Debug("Starting the background task of the VirtualMotionController...");

            _cts = new CancellationTokenSource();
            var _ct = _cts.Token;
            var random = new Random();

            #region Random Digital Io Status

            Task.Run(() =>
            {
                while (true)
                {
                    if (_ct.IsCancellationRequested)
                        break;

                    lock (_myLocker)
                    {
                        for (var i = 0; i < MaxDigitalInputChannels; i++)
                        {
                            _fakeDi[i] = !(random.NextDouble() < 0.5);
                        }

                        for (var i = 0; i < MaxDigitalInputChannels; i++)
                        {
                            _fakeDo[i] = !(random.NextDouble() < 0.5);
                        }
                    }

                    Thread.Sleep(2000);
                }
            }, _ct);

            #endregion

            #region Random Analog Input

            Task.Run(() =>
            {
                
                while (true)
                {
                    if (_ct.IsCancellationRequested)
                        break;

                    for (var i = 0; i < MaxAnalogInputChannels; i++)
                    {
                        _fakeAi[i] = random.NextDouble() * 1000;
                    }

                    Thread.Sleep(200);
                }
            }, _ct);

            #region Random Servo Position Feedback Jitter

            Task.Run(() =>
            {
                while (true)
                {
                    if (_ct.IsCancellationRequested)
                        break;

                    for (var i = 0; i < AxisCount; i++)
                    {
                        _fakeAbsPositionJittered[i] = _fakeAbsPosition[i] + random.Next(-10, 10);
                        UpdateAbsPosition(i);
                    }

                    Thread.Sleep(100);
                }
            }, _ct);

            #endregion

            #endregion
        }

        #endregion
    }
}