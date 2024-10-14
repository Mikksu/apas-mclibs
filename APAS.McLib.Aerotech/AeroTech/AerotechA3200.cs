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
using ApasAxisInfo = APAS.McLib.Sdk.AxisInfo;

namespace APAS.McLib.Aerotech
{
    public class AerotechA3200 : MotionControllerBase
    {
        #region Variables

        private A3200 _controller;
        private ControllerDiagPacket _controllerDiagPacket;

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
            ChildInit();
        }

        protected override void ChildInit()
        {
            _controller = A3200.Connect();

            AxisCount = _controller.Information.Axes.Count;

            Logger?.Debug($"{AxisCount} axes were found.");

            foreach (var aerotechAxis in _controller.Information.Axes)
            {
                Logger?.Info($"Initializing the axis {aerotechAxis.Name}...");
                if (aerotechAxis.AxisType == ComponentType.NdriveMP)
                {
                    Logger?.Debug($"finding the axis configured with the name {aerotechAxis.Name}...");

                    InnerAxisInfoCollection.Add(new ApasAxisInfo(aerotechAxis.Number, aerotechAxis.FirmwareVersion));

                    // enable the servo.
                    Logger?.Debug($"Enabling the axis {aerotechAxis.Name}...");
                    _controller.Commands.Axes[aerotechAxis.Name].Motion.Enable();

                    // sync the current status so we don't need to home the axis if it's homed already.
                    Logger?.Debug($"Syncing the Homed State...");
                    var isHomed =
                        _controller.Commands.Status.AxisStatus(aerotechAxis.Number, AxisStatusSignal.HomeState) != 0;

                    Logger?.Debug($"Syncing the position...");
                    var absPos = ChildUpdateAbsPosition(aerotechAxis.Number);

                    RaiseAxisStateUpdatedEvent(new AxisStatusArgs(aerotechAxis.Number, absPos, isHomed));
                }
                else
                {
                    Logger?.Warn(
                        $"the type {aerotechAxis.AxisType} of the axis {aerotechAxis.Name} does not match the type of {ComponentType.NdriveMP}.");
                }
            }

            MaxAnalogInputChannels = InnerAxisInfoCollection.Count * 4;

            // in the event we got the status of the each axis such as position, error, etc.
            _controller.ControlCenter.Diagnostics.NewDiagPacketArrived += DiagnosticsOnNewDiagPacketArrived;
        }

        protected override double ChildUpdateAbsPosition(int axis)
        {
            return _controller.Commands.Status.AxisStatus(axis, AxisStatusSignal.PositionFeedback);
        }

        protected override void ChildUpdateStatus(int axis)
        {
            // Ignored since the status is updated in the DiagnosticsOnNewDiagPacketArrived event.
        }

        protected override void ChildUpdateStatus()
        {
            // Ignored since the status is updated in the DiagnosticsOnNewDiagPacketArrived event.
        }

        protected override void ChildSetAcceleration(int axis, double acc)
        {
            
        }

        protected override void ChildSetDeceleration(int axis, double dec)
        {
            
        }

        protected override void ChildSetEsDeceleration(int axis, double dec)
        {
            
        }

        protected override void ChildHome(int axis, double hiSpeed, double creepSpeed)
        {
            _controller.Commands.Axes[axis].Motion.Home();

            // To make the AbsPosition equal to 0 after the HOME process, the delay MUST BE added.
            Thread.Sleep(3000);
        }

        protected override void ChildMove(int axis, double speed, double distance, bool fastMoveRequested = false,
            double microstepRate = 0)
        {
            _controller.Commands.Motion.MoveInc(axis, distance, speed);
            _controller.Commands.Motion.WaitForMotionDone(WaitOption.MoveDone, axis);
        }

        protected override void ChildServoOn(int axis)
        {
            _controller.Commands.Motion.Enable(axis);
        }

        protected override void ChildServoOff(int axis)
        {
            _controller.Commands.Motion.Disable(axis);
        }

        protected override void ChildResetFault(int axis)
        {
            _controller.Commands.Motion.FaultAck(axis);
        }

        protected override void ChildStartFast1D(int axis, double range, double interval, double speed,
            int analogCapture, out IEnumerable<Point2D> scanResult)
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
            scanResult = scanPoints;
        }


        protected override void ChildStop()
        {
            _controller.Commands.Motion.Abort(AxisMask.All);
        }

        protected override void ChildEmergencyStop()
        {
            _controller.Commands.Motion.Abort(AxisMask.All);
        }

        protected override IReadOnlyList<double> ChildReadAnalogInput()
        {
            var volt = new List<double>();

            if (_controllerDiagPacket != null)
                foreach (var pack in _controllerDiagPacket)
                {
                    volt.Add(pack.AnalogInput0 * 1000);
                    volt.Add(pack.AnalogInput1 * 1000);
                    volt.Add(pack.AnalogInput2 * 1000);
                    volt.Add(pack.AnalogInput3 * 1000);
                }

            return volt;
        }

        protected override double ChildReadAnalogInput(int port)
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

        protected override void CheckController()
        {
            
            if (_controller == null)
                throw new NullReferenceException($"A3200 controller is null, possibly because the controller is disabled in the configuration file.");

            base.CheckController();

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

            for (var i = 0; i < e.Data.Count; i++)
            {
                RaiseAxisStateUpdatedEvent(
                    new AxisStatusArgs(i, e.Data[i].PositionFeedback, e.Data[i].AxisStatus.Homed));

                if (!e.Data[i].AxisFault.None)
                    RaiseAxisFaultEvent(new MotionFaultArgs(i, e.Data[i].AxisFault.ToString()));
            }
        }

        #endregion

        #region UnitTest

        public void UtFast1D()
        {
            Init();

            StartFast1D(3, 0.1, 1, 1, 20, out var points1);
            StartFast1D(4, 0.1, 1, 1, 20, out var points2);
            StartFast1D(5, 0.1, 1, 1, 20, out var points3);
        }

        #endregion
    }
}