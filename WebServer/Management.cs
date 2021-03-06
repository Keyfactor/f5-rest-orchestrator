using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using System;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.WebServer
{
    public class Management : ManagementBase
    {

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            if (config.OperationType != CertStoreOperationType.Add)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}' Management job expecting 'Add' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                base.ParseJobProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(JobConfig.CertificateStoreDetails, JobConfig.ServerUsername, JobConfig.ServerPassword, JobConfig.UseSSL, JobConfig.JobCertificate.PrivateKeyPassword, JobConfig.LastInventory)
                {
                    PrimaryNode = base.PrimaryNode,
                    IgnoreSSLWarning = base.IgnoreSSLWarning
                };

                LogHandlerCommon.Trace(logger, config.CertificateStoreDetails, "Replacing F5 web server certificate");
                f5.ReplaceWebServerCrt(JobConfig.JobCertificate.Contents);

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }
    }
}
