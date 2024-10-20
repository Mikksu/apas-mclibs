using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using M12;
using M12.Commands.Alignment;
using M12.Definitions;
using M12.Exceptions;
using APAS.CoreLib.Charting;
using APAS.McLib.Sdk;
using APAS.McLib.Sdk.Core;

namespace APAS.McLib.Irixi
{
    public class IrixiM12 : MotionControllerBase
    {
        #region Variables

        private readonly Controller _m12;
        private StatusInfo[] _buffStatus = new StatusInfo[12];
        private double[] _buffPos = new double[12];

        #endregion

        #region Constructors

        public IrixiM12(string portName, int baudRate, string config, ILog logger) 
            : base(portName, baudRate, config, logger)
        {
            _m12 = new Controller(portName, baudRate);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Set the interrupt threshold of the contact sensor in mV.
        /// </summary>
        /// <param name="analogPort">The analog input channel which is used for the CSS. It should start from 0.</param>
        /// <param name="lowerVth">The lower voltage threshold in mV.</param>
        /// <param name="upperVth">The upper voltage threshold in mV.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the analog channel is out of range, or the
        /// lower voltage threshold is not less then the upper voltage threshold.</exception>
        private void SetCssThreshold(int analogPort, ushort lowerVth, ushort upperVth)
        {
            if (analogPort < 0 || analogPort > 1)
                throw new ArgumentOutOfRangeException(nameof(analogPort),
                    "only analog input 0 and 1 can be used for the css.");

            if (lowerVth >= upperVth)
                throw new ArgumentOutOfRangeException(nameof(lowerVth),
                    "the lower voltage threshold must be less than the upper voltage threshold.");

            var cssCh = (CSSCH) ((int) CSSCH.CH1 + analogPort);

            _m12.SetCssThreshold(cssCh, lowerVth, upperVth);
        }

        /// <summary>
        /// Enable or disable the interrupt of the contact sensor.
        /// </summary>
        /// <param name="analogPort">The analog input channel which is used for the CSS. It should start from 0.</param>
        /// <param name="isEnabled">Set to true to enable the interrupt.</param>
        /// <exception cref="ArgumentOutOfRangeException">The analog channel is out of range.</exception>
        private void SetCssInterruptEnabled(int analogPort, bool isEnabled)
        {
            if (analogPort < 0 || analogPort > 1)
                throw new ArgumentOutOfRangeException(nameof(analogPort),
                    "only analog input 0 and 1 can be used for the css.");

            var cssCh = (CSSCH) ((int) CSSCH.CH1 + analogPort);

            _m12.SetCssEnable(cssCh, isEnabled);
        }

        #endregion
        
        #region Override Methods

        protected override void InitImpl()
        {
            M12.Base.SystemInformation info;
            try
            {
                Logger?.Debug($"connecting to the M12({PortName}/{BaudRate}) ...");
                _m12.Open();

                Logger?.Debug("reading the system information ...");
                info = _m12.GetSystemInfo();
            }
            catch (Exception ex)
            {
                Logger?.Debug(ex.Message, ex);
                Logger?.Error("unable to connect to the M12.");
                throw new InvalidOperationException($"it's failed to connect the M12, {ex.Message}");
            }

            FwVersion = info.FirmwareVersion;
        }

        protected override void SetAccImpl(int axis, double acceleration)
        {
            var unitId = AxisIndexToUnitId(axis);

            _m12.SetAccelerationSteps(unitId, (ushort)acceleration);

            Thread.Sleep(5);
        }

        protected override void SetDecImpl(int axis, double dec)
        {
            // ignore
        }

        protected override void SetEsDecImpl(int axis, double dec)
        {
            // ignore
        }

        protected override void HomeImpl(int axis, double hiSpeed, double creepSpeed)
        {
            if (hiSpeed <= 1 || hiSpeed > 100)
                throw new ArgumentOutOfRangeException(nameof(hiSpeed), "the speed1 must be 1 ~ 100");

            if (creepSpeed <= 1 || creepSpeed > 100)
                throw new ArgumentOutOfRangeException(nameof(creepSpeed), "the speed2 must be 1 ~ 100");

            var unitId = AxisIndexToUnitId(axis);


            _m12.Home(unitId, (byte) creepSpeed, (byte) hiSpeed);

            Thread.Sleep(50);
        }

        protected override bool CheckHomeDoneImpl(int axis)
        {
            var stat = _m12.GetUnitState(AxisIndexToUnitId(axis));
            return !stat.IsBusy;
        }

        protected override void MoveImpl(int axis, double speed, double distance)
        {
            var unitId = AxisIndexToUnitId(axis);

            var mcDistance = (int)distance;
            var mcSpeed = (byte)speed;

            // /SetAcceleration(ax, ax.AccelerationSteps);
            _m12.Move(unitId, mcDistance, mcSpeed);
        }

        protected override bool CheckMotionDoneImpl(int axis)
        {
            var stat = _m12.GetUnitState(AxisIndexToUnitId(axis));
            return !stat.IsBusy;
        }

        protected override double ReadPosImpl(int axis)
        {
            var unitId = AxisIndexToUnitId(axis);
            var sta = _m12.GetUnitState(unitId);
            if (sta != null)
                return sta.AbsPosition;

            throw new InvalidOperationException($"unable to get the status of the {unitId} from the IrixiM12.");
        }

        protected override StatusInfo ReadStatusImpl(int axis)
        {
            var unitId = AxisIndexToUnitId(axis);
            var us = _m12.GetUnitState(unitId);
            return new StatusInfo(us.IsBusy, !us.IsBusy, us.IsHomed, true, null);
        }


        /// <summary>
        /// Stop all units immediately.
        /// </summary>
        protected override void StopImpl()
        {
            _m12.Stop(UnitID.U1);
            _m12.Stop(UnitID.U2);
            _m12.Stop(UnitID.U3);
            _m12.Stop(UnitID.U4);
            _m12.Stop(UnitID.U5);
            _m12.Stop(UnitID.U6);
            _m12.Stop(UnitID.U7);
            _m12.Stop(UnitID.U8);
            _m12.Stop(UnitID.U9);
            _m12.Stop(UnitID.U10);
            _m12.Stop(UnitID.U11);
            _m12.Stop(UnitID.U12);
        }

        protected override void StopImpl(int axis)
        {
            _m12.Stop(AxisIndexToUnitId(axis));
        }

        protected override void EStopImpl()
        {
            StopImpl();
        }

        private static void CheckDoChannel(int channel)
        {
            if (channel < 0 || channel >= (int) DigitalOutput.DOUT8)
                throw new ArgumentOutOfRangeException(nameof(channel),
                    @"the channel of the DO is out of range, it should be 0 ~ 7.");
        }

        /// <summary>
        /// Set the status of the specified digital output.
        /// </summary>
        /// <param name="port">The number of the channel to control, it should be 0 ~ 7.</param>
        /// <param name="isOn"></param>
        protected override void SetDOImpl(int port, bool isOn)
        {
            CheckDoChannel(port);

            var m12State = isOn ? DigitalIOStatus.ON : DigitalIOStatus.OFF;

            _m12.SetDout(DigitalOutput.DOUT1 + port, m12State);
        }

        /// <summary>
        /// Read the state of all outputs.
        /// </summary>
        /// <returns></returns>
        protected override bool[] ReadDOImpl()
        {
            var doValues = _m12.ReadDout();
            return doValues.Integrated
                .ToList()
                .Select(x => x == DigitalIOStatus.ON)
                .ToArray();
        }

        protected override bool ReadDOImpl(int port)
        {
            CheckDoChannel(port);
            var m12State = _m12.ReadDout((DigitalOutput) ((int) DigitalOutput.DOUT1 + port));
            return m12State == DigitalIOStatus.ON;
        }

        protected override bool[] ReadDIImpl()
        {
            var diValues = _m12.ReadDin();
            return diValues.Integrated
                .ToList()
                .Select(x => x == DigitalIOStatus.ON)
                .ToArray();
        }

        protected override bool ReadDIImpl(int port)
        {
            var diValues = _m12.ReadDin();
            return diValues.Integrated[port] == DigitalIOStatus.ON;
        }

        protected override double[] ReadAIImpl()
        {
            return _m12.ReadAdc(ADCChannels.CH1 | ADCChannels.CH2 | ADCChannels.CH3 | ADCChannels.CH4 |
                                ADCChannels.CH5 |
                                ADCChannels.CH6 | ADCChannels.CH7 | ADCChannels.CH8);
        }

        protected override double ReadAIImpl(int port)
        {
            var ch = ConvertChannelToAdcChannelsEnum(port);
            return _m12.ReadAdc(ch)[0];
        }

        protected override void AutoTouchImpl(int axis, int port, double vth, double distance, double speed)
        {
            var unitId = AxisIndexToUnitId(axis);
            var currPressure = ReadAIImpl(port);

            SetCssThreshold(port, (ushort) (currPressure - vth), (ushort) (currPressure + vth));

            try
            {
                SetCssInterruptEnabled(port, true);
                _m12.Move(unitId, (int) distance, (byte) speed);
            }
            catch (Exception ex)
            {
                if (ex is UnitErrorException uee)
                {
                    // Mask the exceptions of CSS trigger events.
                    if (uee.Error == Errors.ERR_SYS_CSS1_Triggered || uee.Error == Errors.ERR_SYS_CSS2_Triggered)
                        return;
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                SetCssInterruptEnabled(port, false);
            }

            //TODO check if it goes here without exceptions while moving.
            // goes here if nothing was touched.
            throw new Exception("unable to touch the target in the specified travel distance.");
        }

        protected override void Fast1DImpl(int axis, double range, double interval, double speed, int capture, out Point2D[] scanResult)
        {
            Fast1DImpl(axis, range, interval, speed, capture, out scanResult, -1, out _);
        }


        protected override void Fast1DImpl(int axis, double range, double interval, double speed,
            int capture, out Point2D[] scanResult, int capture2, out Point2D[] scanResult2)
        {
            //TODO 解决多台M12模拟量级联的问题
            //if (analogCapture.Parent is IrixiM12BasedAnalogController acM12Based
            //    && acM12Based.MotionController == this)
            {
                var unitId = AxisIndexToUnitId(axis);

                List<M12.Base.Point2D> scanPoints = null, scanPoints2 = null;
                if (capture2 < 0)
                    _m12.StartFast1D(unitId, (int) range, (ushort) interval, (byte) speed,
                        ConvertChannelToAdcChannelsEnum(capture),
                        out scanPoints);
                else
                    _m12.StartFast1D(unitId, (int) range, (ushort) interval, (byte) speed,
                        ConvertChannelToAdcChannelsEnum(capture),
                        out scanPoints,
                        ConvertChannelToAdcChannelsEnum(capture),
                        out scanPoints2);

                scanResult = scanPoints?.Select(p => new Point2D(p.X, p.Y)).ToArray();
                scanResult2 = scanPoints2?.Select(p => new Point2D(p.X, p.Y)).ToArray();
            }
        }


        protected override void BlindSearchImpl(int hAxis, int vAxis, double range, double gap,
            double interval, double hSpeed, double vSpeed, int analogCapture, out Point3D[] scanResult)
        {
            var hUnitId = AxisIndexToUnitId(hAxis);
            var vUnitId = AxisIndexToUnitId(vAxis);

            var hArgs = new BlindSearchArgs(
                hUnitId, (uint) range, (uint) gap, (ushort) interval, (byte) hSpeed);

            var vArgs = new BlindSearchArgs(
                vUnitId, (uint) range, (uint) gap, (ushort) interval, (byte) vSpeed);

            _m12.StartBlindSearch(hArgs, vArgs,
                ConvertChannelToAdcChannelsEnum(analogCapture), out var scanPoints);

            scanResult = scanPoints?.Select(p => new Point3D(p.X, p.Y, p.Z)).ToArray();
        }

        protected override void DisposeImpl()
        {
            if (_m12 != null && _m12.IsOpened)
                _m12.Close();
        }

        protected override bool CheckSpeedImpl(double speed, out string reason)
        {
            reason = "";

            if (speed < 0 || speed > 100)
                reason = "速度必须为0-100。";

            return !string.IsNullOrEmpty(reason);
        }

        protected override bool CheckControllerImpl(out string reason)
        {
            reason = "";

            if (_m12 == null)
                reason = "the object of the M12 controller is null.";
            else if (!_m12.IsOpened)
                reason = "the M12 is not connected.";

            return !string.IsNullOrEmpty(reason);
        }

        #endregion

        #region Private Methods


        private static int UnitIdToAxisIndex(UnitID unitId)
        {
            return (int) unitId;
        }

        private static UnitID AxisIndexToUnitId(int axisIndex)
        {
            if ((axisIndex < 1) | (axisIndex > 12))
                throw new ArgumentOutOfRangeException(nameof(axisIndex), "the axis id of the M12 should be 1 - 12.");

            if (Enum.TryParse($"U{axisIndex}", out UnitID unitId))
                return unitId;

            throw new ArgumentException($"unrecognized axis index {axisIndex}.", nameof(axisIndex));
        }


        private static ADCChannels ConvertChannelToAdcChannelsEnum(int channel)
        {
            switch (channel)
            {
                case 0:
                    return ADCChannels.CH1;
                case 1:
                    return ADCChannels.CH2;
                case 2:
                    return ADCChannels.CH3;
                case 3:
                    return ADCChannels.CH4;
                case 4:
                    return ADCChannels.CH5;
                case 5:
                    return ADCChannels.CH6;
                case 6:
                    return ADCChannels.CH7;
                case 7:
                    return ADCChannels.CH8;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel),
                        @"the range of the analog input ports of the M12 is 0 to 7.");
            }
        }

        #endregion

        #region UnitTeste

        public void UtMotion()
        {
            Init();

            Thread.Sleep(5000);

            Home(1, 100, 5);

            Move(1, 100, 100000);
        }

        #endregion
    }
}