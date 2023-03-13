using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.WebServer
{
    public class Inventory : InventoryBase
    {
        public Inventory(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }
            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            try
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Processing job parameters");
                dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);

                F5Client f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, null, config.LastInventory) { IgnoreSSLWarning = base.IgnoreSSLWarning };

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Getting the F5 web server device inventory");
                inventory = f5.GetWebServerInventory();

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Submitting F5 web server inventory");
                submitInventory.Invoke(inventory);

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the inventory operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }
    }
}
