using System;
using System.Diagnostics;
using System.Threading;

namespace APAS.McLib.Virtual
{
    public class SimAxis
    {
        #region Variables

        /// <summary>
        /// HOME模拟的最大持续时间，单位秒。
        /// </summary>
        private const int MAX_HOME_SIM_DURATION_S = 10;

        private Timer _tmrPosSim;
        private readonly object _syncRoot = new();
        private CancellationTokenSource _cts;

        private double _distanceToMove;
        private double _distanceMoved;

        private double _homeSimDuration;
        private readonly Stopwatch _homeSimSw = new();

        #endregion

        #region Properties

        public double Acc { get; set; }

        public double Dec { get; set; }

        public double EStopDec { get; set; }

        public double Position { get; set; } = int.MinValue;

        public bool IsHomed { get; set; }

        public bool IsHoming { get; set; }

        public bool IsBusy { get; set; }

        public bool IsServoOn { get; set; }

        public bool IsInp => !IsBusy;

        #endregion

        private void OnTimerHome(object state)
        {
            Debug.Assert(state is double);

            lock (_syncRoot)
            {
                Position += (double)state;

                if (_cts.Token.IsCancellationRequested)
                {
                    // 取消
                    _tmrPosSim.Change(0, Timeout.Infinite);
                    _tmrPosSim.Dispose();
                    _homeSimSw.Stop();


                    IsHomed = false;
                    IsBusy = false;
                    _cts.Dispose();
                }

                if (_homeSimSw.Elapsed.TotalMilliseconds >= _homeSimDuration)
                {
                    // 结束
                    _tmrPosSim.Change(0, Timeout.Infinite);
                    _tmrPosSim.Dispose();
                    _homeSimSw.Stop();

                    IsHomed = true;
                    IsBusy = false;
                    Position = 0;
                    _cts.Dispose();
                }
            }
        }

        private void OnTimerMove(object state)
        {
            Debug.Assert(state is double);

            lock (_syncRoot)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    // 取消
                    _tmrPosSim.Change(0, Timeout.Infinite);
                    _tmrPosSim.Dispose();

                    IsBusy = false;
                    _cts.Dispose();
                }

                var step = (double)state;
                if (Math.Abs(_distanceToMove - _distanceMoved) > Math.Abs(step))
                {
                    Position += step;
                    _distanceMoved += step;
                }
                else
                {
                    Position += _distanceToMove - _distanceMoved;

                    // 结束
                    _tmrPosSim.Change(0, Timeout.Infinite);
                    _tmrPosSim.Dispose();

                    IsBusy = false;
                    _cts.Dispose();
                }
            }
        }

        internal void StartHomeSim()
        {
            lock (_syncRoot)
            {
                if (IsBusy)
                    throw new Exception("The axis is busy");

                try
                {
                    // 随机Home过程需要的时长
                    var r = new Random();
                    _homeSimDuration = r.NextDouble() * MAX_HOME_SIM_DURATION_S * 1000; // 最大5s完成Home
                    _homeSimSw.Start();

                    // 启动位置模拟定时器
                    _tmrPosSim = new Timer(OnTimerHome, -10.0, 0, 10);

                }
                catch
                {
                    // ignored
                }
                finally
                {
                    _cts = new();
                    IsHoming = false;
                    IsBusy = true;
                }
            }
        }

        internal void StartMoveSim(double speed, double distance)
        {
            lock (_syncRoot)
            {
                if (IsBusy)
                    throw new Exception("The axis is busy");

                try
                {
                    var step = Math.Abs(speed / 10) * Math.Sign(distance);
                    _distanceMoved = 0.0;
                    _distanceToMove = distance;

                    // 启动位置模拟定时器
                    _tmrPosSim = new Timer(OnTimerMove, step, 0, 10);
                }
                finally
                {
                    _cts = new CancellationTokenSource();
                    IsBusy = true;
                }
            }
        }

        internal void Stop()
        {
            lock (_syncRoot)
            {
                _cts?.Cancel();
            }
        }
    }
}
