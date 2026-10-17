using DWIS.RigOS.Common.Worker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DWIS.DAQBridge.BaseStar.Server
{
    public class ConfigurationForBaseStar : ConfigurationForOPCUA
    {
        public TimeSpan ConnectionRetryInterval { get; set; } = TimeSpan.FromSeconds(5);

        public bool EnableDataLogging { get; set; } = true;
        public string DataLogDirectory { get; set; } = "/home";
        public TimeSpan DataLogSegmentDuration { get; set; } = TimeSpan.FromHours(6);
        public long MaximumDataLogStorageBytes { get; set; } = 1024L * 1024 * 1024;
        public bool EnableBlackboardHistory { get; set; } = true;

        public bool InitializeInputOPCUAVariables { get; set; } = false;
    }
}
