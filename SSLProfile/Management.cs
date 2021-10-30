using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.SSLProfile
{
    public class Management : ManagementBase
    {
        public override string GetStoreType()
        {
            return "F5-SL-REST";
        }

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");
            if (config.Job.OperationType != AnyJobOperationType.Add
                && config.Job.OperationType != AnyJobOperationType.Remove)
            {
                throw new Exception($"'{config.Store.ClientMachine}-{config.Store.StorePath}-{GetStoreType()}'  expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
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
                        LogHandler.Debug(Logger, config, $"Add entry '{config.Job.Alias}' to '{config.Store.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case AnyJobOperationType.Remove:
                        LogHandler.Trace(Logger, config, $"Remove entry '{config.Job.Alias}' from '{config.Store.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
                }

                LogHandler.Debug(Logger, config, "Job complete");
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

            if (f5.CertificateExists(partition, name))
            {
                if (!JobConfig.Job.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(Logger, JobConfig, $"Replace entry '{name}' in '{JobConfig.Store.StorePath}'");
                f5.ReplaceEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' does not exist in '{JobConfig.Store.StorePath}' and will be added");
                f5.AddEntry(partition, name);
            }
            LogHandler.MethodExit(Logger, JobConfig, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "PerformRemovalJob");
            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' exists in '{JobConfig.Store.StorePath}' and will be removed");
                f5.RemoveEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(Logger, JobConfig, $"The entry '{name}' does not exist in '{JobConfig.Store.StorePath}'");
            }
            LogHandler.MethodExit(Logger, JobConfig, "PerformRemovalJob");
        }
    }
}
