using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.Bundle
{
    public class Management : ManagementBase
    {
        public override string GetStoreType()
        {
            return "F5-CA-REST";
        }

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandler.MethodEntry(logger, config, "processJob");

            if (config.OperationType != CertStoreOperationType.Add
                && config.OperationType != CertStoreOperationType.Remove)
            {
                throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.Job.OperationType)}'");
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

                switch (config.Job.OperationType)
                {
                    case CertStoreOperationType.Add:
                        LogHandler.Debug(logger, JobConfig, $"Add CA Bundle entry '{config.Job.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case CertStoreOperationType.Remove:
                        LogHandler.Debug(logger, JobConfig, $"Remove CA Bundle entry '{config.Job.Alias}' from '{config.CertificateStoreDetails.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
                }

                LogHandler.Debug(logger, JobConfig, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId};
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandler.MethodExit(logger, config, "processJob");
            }
        }

        private void PerformAddJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig, "PerformAddJob");

            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            List<CurrentInventoryItem> inventory = f5.GetCABundleInventory();

            if (inventory.Exists(i => i.Alias.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                if (f5.CertificateExists(partition, name)) { LogHandler.Debug(logger, JobConfig, $"The entry '{name}' exists in the SSL certificate store"); }
                if (!JobConfig.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(logger, JobConfig, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                f5.ReplaceEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' does not exist in the bundle '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                f5.AddBundleEntry(JobConfig.CertificateStoreDetails.StorePath, partition, name);
            }

            LogHandler.MethodExit(logger, JobConfig, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig, "PerformRemovalJob");

            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.EntryExistsInBundle())
            {
                if (f5.CertificateExists(partition, name)) { logger.LogDebug($"The entry '{name}' exists in the SSL certificate store"); }

                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' exists in the bundle '{JobConfig.CertificateStoreDetails.StorePath}' and will be removed");
                f5.RemoveBundleEntry(JobConfig.CertificateStoreDetails.StorePath, partition, name);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' does not exist in the bundle '{JobConfig.CertificateStoreDetails.StorePath}'");
            }

            LogHandler.MethodExit(logger, JobConfig, "PerformRemovalJob");
        }
    }
}
