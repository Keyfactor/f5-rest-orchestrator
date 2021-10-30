using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Enums;
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

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");

            if (config.Job.OperationType != AnyJobOperationType.Add
                && config.Job.OperationType != AnyJobOperationType.Remove)
            {
                throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
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
                    case AnyJobOperationType.Add:
                        LogHandler.Debug(Logger, JobConfig, $"Add CA Bundle entry '{config.Job.Alias}' to '{config.Store.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case AnyJobOperationType.Remove:
                        LogHandler.Debug(Logger, JobConfig, $"Remove CA Bundle entry '{config.Job.Alias}' from '{config.Store.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
                }

                LogHandler.Debug(Logger, JobConfig, "Job complete");
                return new AnyJobCompleteInfo { Status = 2, Message = "Successful" };
            }
            catch (Exception ex)
            {
                return new AnyJobCompleteInfo { Status = 4, Message = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandler.MethodExit(Logger, config, "processJob");
            }
        }

        private void PerformAddJob(F5Client f5)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "PerformAddJob");

            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            List<AgentCertStoreInventoryItem> inventory = f5.GetCABundleInventory();

            if (inventory.Exists(i => i.Alias.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                if (f5.CertificateExists(partition, name)) { LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' exists in the SSL certificate store"); }
                if (!JobConfig.Job.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(Logger, JobConfig, $"Replace entry '{name}' in '{JobConfig.Store.StorePath}'");
                f5.ReplaceEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' does not exist in the bundle '{JobConfig.Store.StorePath}' and will be added");
                f5.AddBundleEntry(JobConfig.Store.StorePath, partition, name);
            }

            LogHandler.MethodExit(Logger, JobConfig, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "PerformRemovalJob");

            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.EntryExistsInBundle())
            {
                if (f5.CertificateExists(partition, name)) { Logger.Debug($"The entry '{name}' exists in the SSL certificate store"); }

                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' exists in the bundle '{JobConfig.Store.StorePath}' and will be removed");
                f5.RemoveBundleEntry(JobConfig.Store.StorePath, partition, name);
            }
            else
            {
                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' does not exist in the bundle '{JobConfig.Store.StorePath}'");
            }

            LogHandler.MethodExit(Logger, JobConfig, "PerformRemovalJob");
        }
    }
}
