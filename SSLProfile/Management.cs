using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.SSLProfile
{
    public class Management : ManagementBase
    {
        public override string GetStoreType()
        {
            return "F5-SL-REST";
        }

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandler.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");
            if (config.OperationType != CertStoreOperationType.Add
                && config.OperationType != CertStoreOperationType.Remove)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}-{GetStoreType()}'  expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }
            
            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                base.ParseJobProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(JobConfig = config)
                {
                    PrimaryNode = base.PrimaryNode,
                    F5Version = base.F5Version
                };

                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        LogHandler.Debug(logger, config.CertificateStoreDetails, $"Add entry '{config.JobCertificate.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case CertStoreOperationType.Remove:
                        LogHandler.Trace(logger, config.CertificateStoreDetails, $"Remove entry '{config.JobCertificate.Alias}' from '{config.CertificateStoreDetails.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
                }

                LogHandler.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandler.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }

        private void PerformAddJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                if (!JobConfig.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                f5.ReplaceEntry(partition, name, JobConfig.JobCertificate.Contents);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                f5.AddEntry(partition, name, JobConfig.JobCertificate.Contents);
            }
            LogHandler.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");
            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' exists in '{JobConfig.CertificateStoreDetails.StorePath}' and will be removed");
                f5.RemoveEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}'");
            }
            LogHandler.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");
        }
    }
}
