using System;
using System.IO;
using System.Linq;
using APAS.MotionLib.ZMC.Configuration;
using APAS.MotionLib.ZMC.ConfigurationEditor.Core;
using Newtonsoft.Json;

namespace APAS.MotionLib.ZMC.ConfigurationEditor.Services
{
    internal class CreateApasZmcConfigFileService
    {
        /// <summary>
        /// 创建APAS的ZMC.dll使用的配置文件。
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="fileName"></param>
        public void CreateApasZmcConfigJsonFile(AxisSettingsCollection settings, string fileName)
        {
            var mcConfig = new McConfig
            {
                Axes = settings.Select(s => new AxisConfig
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
                    .ToArray(),
                Ain = new AnalogInConfig
                {
                    IndexStart = 16,
                    MaxChannel = 4,
                    Param = new[]
                    {
                        new AnalogInParamConfig(0, 0, 10000, 65536),
                        new AnalogInParamConfig(1, 0, 10000, 65536),
                        new AnalogInParamConfig(2, 0, 10000, 65536),
                        new AnalogInParamConfig(3, 0, 10000, 65536)
                    }
                },
                Scope = new ScopeConfig()
            };

            var json = JsonConvert.SerializeObject(mcConfig, Formatting.Indented);
            File.WriteAllText(fileName, json);
        }

        /// <summary>
        /// 从APAS的ZMC.dll配置文件导入。
        /// </summary>
        /// <param name="fileName"></param>
        public AxisSettingsCollection ImportApasZmcConfigJsonFile(string fileName)
        {
            var json = File.ReadAllText(fileName);
            var mcConfig = JsonConvert.DeserializeObject<McConfig>(json);
            if (mcConfig == null)
                throw new InvalidCastException($"无法将文件{fileName}转换为控制器配置对象。");

            var settings = new AxisSettingsCollection();
            settings.AddRange(mcConfig.Axes.Select(s => new AxisSettings(s.Control.Index)
            {
                DiNel = (DiSource)s.Io.Nel,
                IsInverseNel = s.Io.InvNel,
                DiPel = (DiSource)s.Io.Pel,
                IsInversePel = s.Io.InvPel,
                HomeMode = (HomeModeSource)s.Home.Mode,
                HomeAcc = (int)s.Home.Acc,
                HomeDec = (int)s.Home.Dec,
                HomeHiSpeed = (int)s.Home.HiSpeed,
                HomeCreepSpeed = (int)s.Home.CreepSpeed,
                InvertSteps = (PulsePolaritySource)s.Control.InvertStep,
                DriveAcc = (int)s.Motion.Acc,
                DriveDec = (int)s.Motion.Dec,
                DriveFastDec = (int)s.Motion.FastDec,
                SRampDuration = (int)s.Motion.SRampMs,
                DriveSpeed = (int)s.Motion.Speed
            }));

            return settings;
        }
    }
}
