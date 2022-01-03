using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
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

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandler.MethodEntry(logger, config, "processJob");
            if (config.Job.OperationType != CertStoreOperationType.Add
                && config.Job.OperationType != CertStoreOperationType.Remove)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}-{GetStoreType()}'  expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.Job.OperationType)}'");
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
                        LogHandler.Debug(logger, config, $"Add entry '{config.Job.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case CertStoreOperationType.Remove:
                        LogHandler.Trace(logger, config, $"Remove entry '{config.Job.Alias}' from '{config.CertificateStoreDetails.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"{GetStoreType()} expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.Job.OperationType)}'");
                }

                LogHandler.Debug(logger, config, "Job complete");
                return new AnyJobCompleteInfo { Status = 2, Message = "Successful" };
            }
            catch (Exception ex)
            {
                return new AnyJobCompleteInfo { Status = 4, Message = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandler.MethodExit(logger, config, "processJob");
            }
        }

        private void PerformAddJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig, "PerformAddJob");
            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                if (!JobConfig.Job.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(logger, JobConfig, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                f5.ReplaceEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                f5.AddEntry(partition, name);
            }
            LogHandler.MethodExit(logger, JobConfig, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandler.MethodEntry(logger, JobConfig, "PerformRemovalJob");
            string name = JobConfig.Job.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' exists in '{JobConfig.CertificateStoreDetails.StorePath}' and will be removed");
                f5.RemoveEntry(partition, name);
            }
            else
            {
                LogHandler.Debug(logger, JobConfig, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}'");
            }
            LogHandler.MethodExit(logger, JobConfig, "PerformRemovalJob");
        }
    }
}
