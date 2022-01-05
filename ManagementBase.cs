using Keyfactor.Orchestrators.Extensions;
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

        protected bool PrimaryNodeOnlineRequired { get; set; }
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

            LogHandler.MethodEntry(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());
            PrimaryNodeOnlineRequired = false;
            PrimaryNode = string.Empty;
            PrimaryNodeRetryMax = 3;
            PrimaryNodeRetryWaitSecs = 120;
            bool primaryNodeRequired = false;
            LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, "Attempting to determine if the primary node is required to be active");
            if (properties.PrimaryNodeOnlineRequired == null) { throw new Exception("Missing job property string: PrimaryNodeOnlineRequired"); }
            bool.TryParse(properties.PrimaryNodeOnlineRequired?.ToString(), out primaryNodeRequired);
            PrimaryNodeOnlineRequired = primaryNodeRequired;
            LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node online required '{PrimaryNodeOnlineRequired}'");

            if (PrimaryNodeOnlineRequired)
            {
                if (string.IsNullOrEmpty(properties.PrimaryNode?.ToString())) { throw new Exception("Missing job property string: PrimaryNode"); }
                PrimaryNode = properties.PrimaryNode.ToString();
                LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node '{PrimaryNode}'");

                if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryMax?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryMax"); }
                int primaryNodeRetryMax = 3;
                int.TryParse(properties.PrimaryNodeCheckRetryMax.ToString(), out primaryNodeRetryMax);
                PrimaryNodeRetryMax = primaryNodeRetryMax;
                LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node retry count '{PrimaryNodeRetryMax}'");

                if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryWaitSecs?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryWaitSecs"); }
                int primaryNodeRetryWaitSecs = 120;
                int.TryParse(properties.PrimaryNodeCheckRetryWaitSecs.ToString(), out primaryNodeRetryWaitSecs);
                PrimaryNodeRetryWaitSecs = primaryNodeRetryWaitSecs;
                LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node retry wait seconds '{PrimaryNodeRetryWaitSecs}'");
                LogHandler.MethodExit(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            }
            else
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "The primary node is not required to be active");
            }

            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandler.Trace(logger, JobConfig.CertificateStoreDetails, $"F5 version '{F5Version}'");
        }

        protected void PrimaryNodeActive()
        {
            LogHandler.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PrimaryNodeActive");

            if (PrimaryNodeOnlineRequired)
            {
                F5Client f5 = new F5Client(JobConfig) { PrimaryNode = this.PrimaryNode };
                if (!f5.PrimaryNodeActive())
                {
                    LogHandler.Warn(logger, JobConfig.CertificateStoreDetails, $"The primary node: '{PrimaryNode}' is not active on try '{_primaryNodeRetryCount++}' of '{PrimaryNodeRetryMax}'");
                    if (_primaryNodeRetryCount == PrimaryNodeRetryMax)
                    {
                        throw new Exception($"The primary node: '{PrimaryNode}' is not active and the maximum number of retries '{PrimaryNodeRetryMax}' has been reached");
                    }

                    LogHandler.Warn(logger, JobConfig.CertificateStoreDetails, $"Waiting for '{PrimaryNodeRetryWaitSecs}' seconds before checking if '{PrimaryNode}' is active");
                    Thread.Sleep(PrimaryNodeRetryWaitSecs * 1000);

                    LogHandler.Warn(logger, JobConfig.CertificateStoreDetails, $"Checking '{PrimaryNode}' again");
                    PrimaryNodeActive();
                }
            }
            else
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "The primary node is not required to be active");
            }
            LogHandler.MethodExit(logger, JobConfig.CertificateStoreDetails, "PrimaryNodeActive");
        }
    }
}
