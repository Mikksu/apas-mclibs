using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using APAS.CoreLib.Charting;
using APAS.McLib.Sdk;
using log4net;
using StatusInfo = APAS.McLib.Sdk.Core.StatusInfo;


//！ 应用该模板时，请注意将命名空间更改为实际名称。
namespace APAS.McLib.Virtual
{
    public class VirtualMc : MotionControllerBase
    {
        #region Variables

        private const int MAX_SIM_AXIS = 32;
        private const int MAX_SIM_IO = 32;

        /// <summary>
        /// HOME模拟的最大持续时间，单位秒。
        /// </summary>
        private const int MAX_HOME_SIM_DURATION_S = 5;

        private readonly Random _rndAIO = new Random();
        private readonly bool[] _buffDi = new bool[MAX_SIM_IO];
        private readonly bool[] _buffDo = new bool[MAX_SIM_IO];
        private readonly double[] _buffAi = new double[MAX_SIM_IO];
        private readonly double[] _buffAo = new double[MAX_SIM_IO];

        private readonly SimAxis[] _simAxis = new SimAxis[MAX_SIM_AXIS];



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
            //TODO 此处初始化控制器参数；如果下列参数动态读取，则可在InitImpl()函数中赋值。

            for (var i = 0; i < MAX_SIM_AXIS; i++)
            {
                _simAxis[i] = new SimAxis();
            }
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
            
        }

        /// <summary>
        /// 设置指定轴的加速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="acc">加速度值</param>
        protected override void SetAccImpl(int axis, double acc)
        {
            _simAxis[axis].Acc = acc;
        }

        /// <summary>
        /// 设置指定轴的减速度。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="dec">减速度值</param>
        protected override void SetDecImpl(int axis, double dec)
        {
            _simAxis[axis].Dec = dec;
        }

        protected override void SetEsDecImpl(int axis, double dec)
        {
            _simAxis[axis].EStopDec = dec;
        }

        /// <summary>
        /// 指定轴回机械零点。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <param name="hiSpeed">快速找机械原点的速度值。如不适用请忽略。</param>
        /// <param name="creepSpeed">找到机械原点后返回零位的爬行速度。如不适用请忽略。</param>
        protected override void HomeImpl(int axis, double hiSpeed, double creepSpeed)
        {
            var ax = _simAxis[axis];
            ax.Cts = new CancellationTokenSource();
            ax.IsHomed = false;
            ax.IsHoming = true;
            ax.IsBusy = true;
            var posBeforeHome = ax.Position;

            Task.Run(() =>
            {
                try
                {
                    // 随机Home过程需要的时长
                    var r = new Random();
                    var duration = r.NextDouble() * MAX_HOME_SIM_DURATION_S * 60; // 最大5s完成Home

                    var sw = new Stopwatch();
                    sw.Start();
                    while (sw.Elapsed.TotalMilliseconds < duration)
                    {
                        ax.Position = posBeforeHome - 10;

                        Thread.Sleep(100);

                        if (_simAxis[axis].Cts.Token.IsCancellationRequested)
                            break;
                    }

                    ax.Position = 0;
                    ax.IsHomed = true;
                    
                }
                catch
                {
                    // ignored
                }
                finally
                {
                    ax.IsHoming = false;
                    ax.IsBusy = false;
                }
                
            });
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            return !_simAxis[axis].IsHoming;
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
            var ax = _simAxis[axis];
            if (ax.IsBusy)
                throw new Exception("axis is busy.");

            
            var step = Math.Abs(speed / 10) * Math.Sign(distance);
            var distMoved = 0.0;

            Task.Run(() =>
            {
                ax.IsBusy = true;
                try
                {
                    while (true)
                    {
                        if (Math.Abs(distance - distMoved) > Math.Abs(step))
                        {
                            ax.Position += step;
                            distMoved += step;
                        }
                        else
                        {
                            ax.Position += distance - distMoved;
                            break;
                        }

                        if (ax.Cts.Token.IsCancellationRequested)
                            break;

                        Thread.Sleep(10);
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    ax.IsBusy = false;
                }
                
            });
            
        }

        protected override bool CheckMotionDoneImpl(int axis)
        {
            return !_simAxis[axis].IsBusy;
        }

        /// <summary>
        /// 开启励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOnImpl(int axis)
        {
            _simAxis[axis].IsServoOn = true;
        }

        /// <summary>
        /// 关闭励磁。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ServoOffImpl(int axis)
        {
            _simAxis[axis].IsServoOn = false;
        }

        /// <summary>
        /// 读取最新的绝对位置。
        /// </summary>
        /// <param name="axis">轴号</param>
        /// <returns>最新绝对位置</returns>
        protected override double ReadPosImpl(int axis)
        {
            return _simAxis[axis].Position;
        }

        /// <summary>
        /// 更新指定轴状态。
        /// <para>注意：请在该函数调用RaiseAxisStateUpdatedEvent()函数，以通知APAS主程序当前轴的状态已更新。</para>
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override StatusInfo ReadStatusImpl(int axis)
        {
            var ax = _simAxis[axis];
            return new StatusInfo(ax.IsBusy, ax.IsInp, ax.IsHomed, ax.IsServoOn, null);
        }

        /// <summary>
        /// 清除指定轴的错误。
        /// </summary>
        /// <param name="axis">轴号</param>
        protected override void ResetAlarmImpl(int axis)
        {
           
        }


        #region IO Controller

        /// <summary>
        /// 设置指定数字输出端口的状态。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="isOn">是否设置为有效电平</param>
        protected override void SetDOImpl(int port, bool isOn)
        {
            if (port >= 0 && port < MAX_SIM_IO)
            {
                _buffDo[port] = isOn;
            }
        }

        /// <summary>
        /// 读取指定数字输出端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDOImpl(int port)
        {
            if (port >= 0 && port < MAX_SIM_IO)
            {
                return _buffDo[port];
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
        protected override bool[] ReadDOImpl()
        {
            return _buffDo;
        }

        /// <summary>
        /// 读取指定数字输入端口。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口状态。True表示端口输出为有效电平。</returns>
        protected override bool ReadDIImpl(int port)
        {
            if (port >= 0 && port < MAX_SIM_IO)
            {
                return _buffDi[port];
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
        protected override bool[] ReadDIImpl()
        {
            return _buffDi;
        }

        #endregion

        #region Analog Controller

        /// <summary>
        /// 读取所有模拟输入端口的电压值。
        /// </summary>
        /// <returns>电压值列表。</returns>
        protected override double[] ReadAIImpl()
        {
            return _buffAi;
        }

        /// <summary>
        /// 读取指定模拟输入端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAIImpl(int port)
        {
            if (port >= 0 && port < MAX_SIM_IO)
            {
                return _buffAi[port];
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
        protected override double[] ReadAOImpl()
        {
            return _buffAo.Select(x => x + _rndAIO.NextDouble()).ToArray();
        }

        /// <summary>
        /// 读取指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        protected override double ReadAOImpl(int port)
        {
            return _buffAo[port] + _rndAIO.NextDouble();
        }

        /// <summary>
        /// 设置指定模拟输出端口的电压值。
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="value">电压值</param>
        protected override void SetAOImpl(int port, double value)
        {
            _buffAo[port] = value;
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
            throw new NotSupportedException();
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
        /// <param name="capture">反馈信号捕获端口</param>
        /// <param name="scanResult">扫描结果列表（X:水平轴坐标，Y:垂直轴坐标，Z:反馈信号）</param>
        protected override void BlindSearchImpl(int hAxis, int vAxis, double range, double gap,
            double interval, double hSpeed, double vSpeed, int capture, out Point3D[] scanResult)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// 停止所有轴移动。
        /// </summary>
        protected override void StopImpl()
        {
            _simAxis.ToList().ForEach(x => x.Cts?.Cancel());
        }

        protected override void StopImpl(int axis)
        {
            _simAxis[axis].Cts?.Cancel();
        }

        /// <summary>
        /// 紧急停止所有轴。
        /// </summary>
        protected override void EStopImpl()
        {
            StopImpl();
        }

        /// <summary>
        /// 关闭运动控制器，并销毁运动控制器实例。
        /// </summary>
        protected override void DisposeImpl()
        {
            // 结束所有可能正在执行的Move仿真线程。
            StopImpl();
        }

        
        #endregion
    }
}