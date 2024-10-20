using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aerotech.A3200;
using Aerotech.A3200.Commands;
using Aerotech.A3200.DataCollection;
using Aerotech.A3200.Information;
using Aerotech.A3200.Status;
using APAS.CoreLib.Charting;
using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Core;
using log4net;
using A3200 = Aerotech.A3200.Controller;

namespace APAS.McLib.Aerotech
{
    public class AerotechA3200 : MotionControllerBase
    {
        #region Variables

        private A3200 _controller;
        private ControllerDiagPacket _controllerDiagPacket;
        private int _maxAxis;
        private int _maxAI;

        #endregion

        #region Constructors

        public AerotechA3200(string portName, int baudRate, string config, ILog logger) : base(portName,
            baudRate, config, logger)
        {
            _controller = A3200.ConnectedController;
        }

        #endregion

        #region Methods

        public void InitTest()
        {
            InitImpl();
        }

        protected override void InitImpl()
        {
            _controller = A3200.Connect();
            _maxAxis = _controller.Information.Axes.Count;

            Logger?.Debug($"{_maxAxis} axes were found.");

            foreach (var a3200 in _controller.Information.Axes.Where(x => x.AxisType == ComponentType.NdriveMP))
            {
                Logger?.Info($"Initializing the axis {a3200.Name}...");
                Logger?.Debug($"finding the axis configured with the name {a3200.Name}...");
                
                // enable the servo.
                Logger?.Debug($"Enabling the axis {a3200.Name}...");
                _controller.Commands.Axes[a3200.Name].Motion.Enable();

                // sync the current status so we don't need to home the axis if it's homed already.
                Logger?.Debug($"Syncing the Homed State...");
            }

            // 最大可用的AI数量
            _maxAI = _controller.Information.Axes.Count * 4;

            // in the event we got the status of the each axis such as position, error, etc.
            _controller.ControlCenter.Diagnostics.NewDiagPacketArrived += DiagnosticsOnNewDiagPacketArrived;
        }

        protected override double ReadPosImpl(int axis)
        {
            return _controllerDiagPacket[axis].PositionFeedback;
        }

        protected override StatusInfo ReadStatusImpl(int axis)
        {
            var isBusy = !_controllerDiagPacket[axis].AxisStatus.MoveDone;
            var isInp = _controllerDiagPacket[axis].AxisStatus.MoveDone;
            var isHomed = _controllerDiagPacket[axis].AxisStatus.Homed;
            var isServoOn = _controllerDiagPacket[axis].DriveStatus.Enabled;
            var isAlarmed = !_controllerDiagPacket[axis].AxisFault.None;

            AlarmInfo alarm = null;
            if (isAlarmed)
                alarm = new AlarmInfo(1, _controllerDiagPacket[axis].AxisFault.ToString());

            return new StatusInfo(isBusy, isInp, isHomed, isServoOn, [alarm]);
        }

        protected override void SetAccImpl(int axis, double acc)
        {
            
        }

        protected override void SetDecImpl(int axis, double dec)
        {
            
        }

        protected override void SetEsDecImpl(int axis, double dec)
        {
            
        }

        protected override void HomeImpl(int axis, double hiSpeed, double creepSpeed)
        {
            _controller.Commands.Axes[axis].Motion.Home();
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            return !_controllerDiagPacket[axis].AxisStatus.Homing;
        }

        protected override void MoveImpl(int axis, double speed, double distance)
        {
            _controller.Commands.Motion.MoveInc(axis, distance, speed);
        }

        protected override bool CheckMotionDoneImpl(int axis)
        {
            return _controllerDiagPacket[axis].AxisStatus.MoveDone;
        }

        protected override void ServoOnImpl(int axis)
        {
            _controller.Commands.Motion.Enable(axis);
        }

        protected override void ServoOffImpl(int axis)
        {
            _controller.Commands.Motion.Disable(axis);
        }

        protected override void ResetAlarmImpl(int axis)
        {
            _controller.Commands.Motion.FaultAck(axis);
        }

        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int analogCapture, out Point2D[] scanResult)
        {
            // calculate the driver ID according to the port number of the analog input.
            var axisOfAin = analogCapture / 4;

            // calculate the index of the AnalogInput port according to the port number of the analog input.
            var ainId = (AxisDataSignal)ConvertAnalogInputPortToDataSignal(analogCapture);

            // Add position command and position feedback on axis 0 as signals to collect.
            var config = _controller.DataCollection.Configuration;

            // capture position
            config.Axis.Add(AxisDataSignal.PositionFeedback, axis);

            // capture analog input
            config.Axis.Add(ainId, axisOfAin);

            // Collect 1 point of data for the signals every 1 ms
            config.CollectionPeriod = 1.0;

            // Collect 1,000 points of data for each signal
            config.PointsToCollect = 20000;

            // Start the data collection process.
            _controller.DataCollection.Start();
            _controller.Commands.Motion.MoveInc(axis, range, speed);
            _controller.Commands.Motion.WaitForMotionDone(WaitOption.MoveDone, axis);
            _controller.DataCollection.Stop();

            // Retrieve the data points collected
            var results = _controller.DataCollection.GetData(_controller.DataCollection.Status.PointsCollected);
            results = results.ConvertUnits(
                new global::Aerotech.A3200.Units.UnitInformation(global::Aerotech.A3200.Units.DistanceUnit.Primary));

            var dataResults = results.Axis;
            var pointNumber = results.CollectedPoints;
            var scanPoints = new List<Point2D>();
            for (var i = 0; i < pointNumber; i++)
            {
                var position = dataResults[AxisDataSignal.PositionFeedback, axis].Points[i];
                var volt = dataResults[ainId, axisOfAin].Points[i];
                scanPoints.Add(new Point2D(position, volt));
            }


            // convert V to mV
            scanPoints.ForEach(p => p.Y *= 1000);
            scanResult = scanPoints.ToArray();
        }

        protected override void StopImpl()
        {
            _controller.Commands.Motion.Abort(AxisMask.All);
        }

        protected override void StopImpl(int axis)
        {
            _controller.Commands.Motion.Abort(axis);
        }

        protected override void EStopImpl()
        {
            _controller.Commands.Motion.Abort(AxisMask.All);
        }

        protected override double[] ReadAIImpl()
        {
            /*
             * A3200每个驱动器包含4路AI；
             * 多个A3200串联后，AI编号依次累加，例如0号A3200的AI编号为0~3,1号A3200的AI编号为4~7，以此类推；
             * 因此从系统配置中的AI标号转换为A3200 AI编号时：A3200 AI编号 = 系统AI编号 % 4
             */
            var volt = new List<double>();

            if (_controllerDiagPacket != null)
                foreach (var pack in _controllerDiagPacket)
                {
                    volt.Add(pack.AnalogInput0 * 1000);
                    volt.Add(pack.AnalogInput1 * 1000);
                    volt.Add(pack.AnalogInput2 * 1000);
                    volt.Add(pack.AnalogInput3 * 1000);
                }

            return volt.ToArray();
        }

        protected override double ReadAIImpl(int port)
        {
            var volt = double.NaN;

            if (_controllerDiagPacket != null)
            {
                var axisDriver = (int) (port % 4);


                switch (port % 4)
                {
                    case 0:
                        volt = _controllerDiagPacket[axisDriver].AnalogInput0;
                        break;

                    case 1:
                        volt = _controllerDiagPacket[axisDriver].AnalogInput1;
                        break;

                    case 2:
                        volt = _controllerDiagPacket[axisDriver].AnalogInput2;
                        break;

                    case 3:
                        volt = _controllerDiagPacket[axisDriver].AnalogInput3;
                        break;
                }

                volt *= 1000; // convert to mV.
            }

            return volt;
        }

        protected override bool CheckControllerImpl(out string reason)
        {
            reason = "";
            if (_controller == null)
                reason = $"A3200 controller is null, possibly because the controller is disabled in the configuration file.";

            return !string.IsNullOrEmpty(reason);
        }

        #endregion

        #region Private Methods

        private static AxisExtendedDataSignal ConvertAnalogInputPortToDataSignal(int port)
        {
            var tPort = port % 4;

            switch (tPort)
            {
                case 0:
                    return AxisExtendedDataSignal.AnalogInput0;

                case 1:
                    return AxisExtendedDataSignal.AnalogInput1;

                case 2:
                    return AxisExtendedDataSignal.AnalogInput2;

                case 3:
                    return AxisExtendedDataSignal.AnalogInput3;

                default:
                    throw new ArgumentOutOfRangeException(nameof(port),
                        @"the analog port of the A3200 should be 0 to 3");
            }
        }

        #endregion

        #region Events

        private void DiagnosticsOnNewDiagPacketArrived(object sender, NewDiagPacketArrivedEventArgs e)
        {
            _controllerDiagPacket = e.Data;
        }

        #endregion

        #region UnitTest

        public void UnitTestProxyFast1D()
        {
            Init();

            StartFast1D(3, 0.1, 1, 1, 20, out var points1);
            StartFast1D(4, 0.1, 1, 1, 20, out var points2);
            StartFast1D(5, 0.1, 1, 1, 20, out var points3);
        }

        #endregion
    }
}