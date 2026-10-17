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

        public bool InitializeInputOPCUAVariables { get; set; } = false;
    }
}
