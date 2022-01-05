using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.SSLProfile
{
    public class Discovery : DiscoveryBase
    {
        public override string GetStoreType()
        {
            return "F5-SL-REST";
        }

        public override JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            LogHandler.MethodEntry(logger, new CertificateStore(), "ProcessJob");
            F5Client f5 = new F5Client(config);

            try
            {
                LogHandler.Debug(logger, new CertificateStore(), "Getting partitions");
                List<string> locations = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandler.Debug(logger, new CertificateStore(), $"Submitting {locations?.Count} partitions");
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
