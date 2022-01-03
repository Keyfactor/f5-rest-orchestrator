using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    public abstract class ManagementBase : IManagementJobExtension
    {
        protected ILogger logger; 
        
        protected ManagementJobConfiguration JobConfig { get; set; }

        protected string PrimaryNode { get; set; }
        protected int PrimaryNodeRetryMax { get; set; }
        protected int PrimaryNodeRetryWaitSecs { get; set; }
        protected int _primaryNodeRetryCount = 0;
        protected string F5Version { get; set; }

        public string ExtensionName => string.Empty;

        public abstract string GetStoreType();

        public abstract JobResult ProcessJob(ManagementJobConfiguration config);

        protected void ParseJobProperties()
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandler.MethodEntry(logger, JobConfig, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());
            if (string.IsNullOrEmpty(properties.PrimaryNode?.ToString())) { throw new Exception("Missing job property string: PrimaryNode"); }
            PrimaryNode = properties.PrimaryNode.ToString();
            LogHandler.Trace(logger, JobConfig, $"Primary node '{PrimaryNode}'");

            if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryMax?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryMax"); }
            int primaryNodeRetryMax = 3;
            int.TryParse(properties.PrimaryNodeCheckRetryMax.ToString(), out primaryNodeRetryMax);
            PrimaryNodeRetryMax = primaryNodeRetryMax;
            LogHandler.Trace(logger, JobConfig, $"Primary node retry count '{PrimaryNodeRetryMax}'");

            if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryWaitSecs?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryWaitSecs"); }
            int primaryNodeRetryWaitSecs = 120;
            int.TryParse(properties.PrimaryNodeCheckRetryWaitSecs.ToString(), out primaryNodeRetryWaitSecs);
            PrimaryNodeRetryWaitSecs = primaryNodeRetryWaitSecs;
            LogHandler.Trace(logger, JobConfig, $"Primary node retry wait seconds '{PrimaryNodeRetryWaitSecs}'");
            LogHandler.MethodExit(logger, JobConfig, "ParseJobProperties");

            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandler.Trace(logger, JobConfig, $"F5 version '{F5Version}'");
        }

        protected void PrimaryNodeActive()
        {
            LogHandler.MethodEntry(logger, JobConfig, "PrimaryNodeActive");
            F5Client f5 = new F5Client(JobConfig) { PrimaryNode = this.PrimaryNode };
            if (!f5.PrimaryNodeActive())
            {
                LogHandler.Warn(logger, JobConfig, $"The primary node: '{PrimaryNode}' is not active on try '{_primaryNodeRetryCount++}' of '{PrimaryNodeRetryMax}'");
                if (_primaryNodeRetryCount == PrimaryNodeRetryMax)
                {
                    throw new Exception($"The primary node: '{PrimaryNode}' is not active and the maximum number of retries '{PrimaryNodeRetryMax}' has been reached");
                }

                LogHandler.Warn(logger, JobConfig, $"Waiting for '{PrimaryNodeRetryWaitSecs}' seconds before checking if '{PrimaryNode}' is active");
                Thread.Sleep(PrimaryNodeRetryWaitSecs * 1000);

                LogHandler.Warn(logger, JobConfig, $"Checking '{PrimaryNode}' again");
                PrimaryNodeActive();
            }
            LogHandler.MethodExit(logger, JobConfig, "PrimaryNodeActive");
        }
    }
}
