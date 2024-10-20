using System.Threading;

namespace APAS.McLib.Virtual
{
    public class SimAxis
    {
        public double Acc { get; set; }

        public double Dec { get; set; }

        public double EStopDec { get; set; }

        public double Position { get; set; } = int.MinValue;

        public bool IsHomed { get; set; }

        public bool IsHoming { get; set; }

        public bool IsBusy { get; set; }

        public bool IsServoOn { get; set; }

        public bool IsInp => !IsBusy;

        public CancellationTokenSource Cts { get; set; }
    }
}
