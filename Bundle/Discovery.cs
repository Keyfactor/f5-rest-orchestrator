using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.Bundle
{
    public class Discovery : DiscoveryBase
    {
        public override string GetStoreType()
        {
            return "F5-CA-REST";
        }

        public override JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            LogHandler.MethodEntry(logger, new CertificateStore(), "ProcessJob");

            F5Client f5 = new F5Client(new CertificateStore(), config.ServerUsername, config.ServerPassword, config.UseSSL, string.Empty, new List<PreviousInventoryItem>());

            try
            {
                LogHandler.Debug(logger, new CertificateStore(), "Getting partitions");
                List<string> partitions = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandler.Trace(logger, new CertificateStore(), $"Found {partitions?.Count} partitions");
                List<string> locations = new List<string>();
                foreach (string partition in partitions)
                {
                    LogHandler.Trace(logger, new CertificateStore(), $"Getting CA Bundles for partition '{partition}'");
                    locations.AddRange(f5.GetCABundles(partition, 20).Select(p => p.fullPath).ToList());
                }

                LogHandler.Debug(logger, new CertificateStore(), $"Submitting {locations.Count} locations");
                sdr.Invoke(locations);

                LogHandler.Debug(logger, new CertificateStore(), "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the discovery operation.") };
            }
            finally
            {
                LogHandler.MethodExit(logger, new CertificateStore(), "ProcessJob");
            }
        }
    }
}
