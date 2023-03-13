using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Bundle
{
    public class Discovery : DiscoveryBase
    {
        public Discovery(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            CertificateStore certificateStore = new CertificateStore() { ClientMachine = config.ClientMachine };
            LogHandlerCommon.MethodEntry(logger, certificateStore, "ProcessJob");

            try
            {
                LogHandlerCommon.Debug(logger, certificateStore, "Getting partitions");
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);

                F5Client f5 = new F5Client(certificateStore, ServerUserName, ServerPassword, config.UseSSL, string.Empty, new List<PreviousInventoryItem>()) { IgnoreSSLWarning = true };
                List<string> partitions = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandlerCommon.Trace(logger, certificateStore, $"Found {partitions?.Count} partitions");
                List<string> locations = new List<string>();
                foreach (string partition in partitions)
                {
                    LogHandlerCommon.Trace(logger, certificateStore, $"Getting CA Bundles for partition '{partition}'");
                    locations.AddRange(f5.GetCABundles(partition, 20).Select(p => p.fullPath).ToList());
                }

                LogHandlerCommon.Debug(logger, certificateStore, $"Submitting {locations.Count} locations");
                sdr.Invoke(locations);

                LogHandlerCommon.Debug(logger, certificateStore, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the discovery operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, certificateStore, "ProcessJob");
            }
        }
    }
}
