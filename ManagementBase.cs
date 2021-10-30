using CSS.Common.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    public abstract class ManagementBase : IManagementJobExtension
    {
        protected AnyJobConfigInfo JobConfig { get; set; }

        protected string PrimaryNode { get; set; }
        protected int PrimaryNodeRetryMax { get; set; }
        protected int PrimaryNodeRetryWaitSecs { get; set; }
        protected int _primaryNodeRetryCount = 0;
        protected string F5Version { get; set; }

        public string ExtensionName => string.Empty;

        public abstract string GetStoreType();

        public abstract JobResult processJob(ManagementJobConfiguration config);

        protected void ParseJobProperties()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.Store.Properties.ToString());
            if (string.IsNullOrEmpty(properties.PrimaryNode?.ToString())) { throw new Exception("Missing job property string: PrimaryNode"); }
            PrimaryNode = properties.PrimaryNode.ToString();
            LogHandler.Trace(Logger, JobConfig, $"Primary node '{PrimaryNode}'");

            if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryMax?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryMax"); }
            int primaryNodeRetryMax = 3;
            int.TryParse(properties.PrimaryNodeCheckRetryMax.ToString(), out primaryNodeRetryMax);
            PrimaryNodeRetryMax = primaryNodeRetryMax;
            LogHandler.Trace(Logger, JobConfig, $"Primary node retry count '{PrimaryNodeRetryMax}'");

            if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryWaitSecs?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryWaitSecs"); }
            int primaryNodeRetryWaitSecs = 120;
            int.TryParse(properties.PrimaryNodeCheckRetryWaitSecs.ToString(), out primaryNodeRetryWaitSecs);
            PrimaryNodeRetryWaitSecs = primaryNodeRetryWaitSecs;
            LogHandler.Trace(Logger, JobConfig, $"Primary node retry wait seconds '{PrimaryNodeRetryWaitSecs}'");
            LogHandler.MethodExit(Logger, JobConfig, "ParseJobProperties");

            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandler.Trace(Logger, JobConfig, $"F5 version '{F5Version}'");
        }

        protected void PrimaryNodeActive()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "PrimaryNodeActive");
            F5Client f5 = new F5Client(JobConfig) { PrimaryNode = this.PrimaryNode };
            if (!f5.PrimaryNodeActive())
            {
                LogHandler.Warn(Logger, JobConfig, $"The primary node: '{PrimaryNode}' is not active on try '{_primaryNodeRetryCount++}' of '{PrimaryNodeRetryMax}'");
                if (_primaryNodeRetryCount == PrimaryNodeRetryMax)
                {
                    throw new Exception($"The primary node: '{PrimaryNode}' is not active and the maximum number of retries '{PrimaryNodeRetryMax}' has been reached");
                }

                LogHandler.Warn(Logger, JobConfig, $"Waiting for '{PrimaryNodeRetryWaitSecs}' seconds before checking if '{PrimaryNode}' is active");
                Thread.Sleep(PrimaryNodeRetryWaitSecs * 1000);

                LogHandler.Warn(Logger, JobConfig, $"Checking '{PrimaryNode}' again");
                PrimaryNodeActive();
            }
            LogHandler.MethodExit(Logger, JobConfig, "PrimaryNodeActive");
        }
    }
}
