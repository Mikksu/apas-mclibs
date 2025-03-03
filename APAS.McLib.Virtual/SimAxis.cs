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
        private const int MAX_HOME_SIM_DURATION_S = 5;

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

        private void PreSim()
        {
            IsBusy = true;
            _cts = new CancellationTokenSource();
        }

        private void PostSim()
        {
            if (_tmrPosSim != null)
            {
                _tmrPosSim.Change(0, Timeout.Infinite);
                _tmrPosSim.Dispose();
                _tmrPosSim = null;

                _cts?.Dispose();
                _cts = null;

                IsBusy = false;
            }
        }

        private void OnTimerHome(object state)
        {
            Debug.Assert(state is double);

            lock (_syncRoot)
            {
                if (_cts == null || _cts.Token.IsCancellationRequested)
                {
                    // 取消
                    PostSim();

                    _homeSimSw.Stop();
                    IsHomed = false;
                }
                else if (_homeSimSw.Elapsed.TotalMilliseconds >= _homeSimDuration)
                {
                    // 结束
                    PostSim();

                    _homeSimSw.Stop();

                    IsHomed = true;
                    Position = 0;
                }
                else
                {
                    Position += (double)state;
                }
            }
        }

        private void OnTimerMove(object state)
        {
            Debug.Assert(state is double);

            lock (_syncRoot)
            {
                if (_cts == null || _cts.Token.IsCancellationRequested)
                {
                    // 取消
                    PostSim();
                   
                    return;
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
                    PostSim();
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
                    PreSim();
                    IsHoming = false;

                    // 随机Home过程需要的时长
                    var r = new Random();
                    _homeSimDuration = r.NextDouble() * MAX_HOME_SIM_DURATION_S * 1000; // 最大5s完成Home
                    _homeSimSw.Start();

                    // 启动位置模拟定时器
                    _tmrPosSim = new Timer(OnTimerHome, -10.0, 0, 10);

                }
                catch
                {
                    IsHomed = false;
                    PostSim();
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
                    PreSim();

                    var step = Math.Abs(speed / 10) * Math.Sign(distance);
                    _distanceMoved = 0.0;
                    _distanceToMove = distance;

                    // 启动位置模拟定时器
                    _tmrPosSim = new Timer(OnTimerMove, step, 0, 10);
                }
                catch
                {
                    PostSim();
                }
            }
        }

        internal void Stop()
        {
            lock (_syncRoot)
            {
                _tmrPosSim.Change(0, Timeout.Infinite);
                _cts?.Cancel();
            }
        }
    }
}
