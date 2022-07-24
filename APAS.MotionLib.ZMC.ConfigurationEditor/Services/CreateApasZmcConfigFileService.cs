using System.IO;
using System.Linq;
using APAS.MotionLib.ZMC.Configuration;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
using Newtonsoft.Json;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Services
{
    internal class CreateApasZmcConfigFileService
    {
        public void CreateApasZmcConfigJsonFile(AxisSettingsCollection settings, string fileName)
        {
            var mcConfig = new McConfig();

            mcConfig.Axes = settings.Select(s => new AxisConfig
                {
                    Control = { AxisType = 7, InvertStep = (int)s.InvertSteps, Index = s.AxisIndex, Units = 1 },
                    Io =
                    {
                        ServoOn = -1,
                        Alarm = -1,
                        Nel = (int)s.DiNel,
                        InvNel = s.IsInverseNel,
                        Pel = (int)s.DiPel,
                        InvPel = s.IsInversePel,
                        Org = -1,
                        IsNelAsDatum = (s.HomeMode == HomeModeSource.负方向)
                    },
                    Home =
                    {
                        Acc = s.HomeAcc,
                        Dec = s.HomeDec,
                        HiSpeed = s.HomeHiSpeed,
                        CreepSpeed = s.HomeCreepSpeed,
                        Mode = (int)s.HomeMode
                    },
                    Motion =
                    {
                        Acc = s.DriveAcc,
                        Dec = s.DriveDec,
                        FastDec = s.DriveFastDec,
                        Speed = s.DriveSpeed,
                        SRampMs = s.SRampDuration
                    }
                })
                .ToArray();

            mcConfig.Ain = new AnalogInConfig
            {
                IndexStart = 32,
                MaxChannel = 4,
                Param = new[]
                {
                    new AnalogInParamConfig(0, 0, 10, 65536),
                    new AnalogInParamConfig(1, 0, 10, 65536),
                    new AnalogInParamConfig(2, 0, 10, 65536),
                    new AnalogInParamConfig(3, 0, 10, 65536)
                }
            };

            mcConfig.Scope = new ScopeConfig();

            var json = JsonConvert.SerializeObject(mcConfig, Formatting.Indented);
            File.WriteAllText(fileName, json);
        }
    }
}
