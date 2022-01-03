using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using System;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.WebServer
{
    public class Management : ManagementBase
    {
        public override string GetStoreType()
        {
            return "F5-WS-REST";
        }

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandler.MethodEntry(logger, config, "processJob");

            if (config.Job.OperationType != CertStoreOperationType.Add)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}-{GetStoreType()}' expecting 'Add' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.Job.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                base.ParseJobProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(JobConfig = config)
                {
                    PrimaryNode = base.PrimaryNode
                };

                LogHandler.Trace(logger, config, "Replacing F5 web server certificate");
                f5.ReplaceWebServerCrt();

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
    }
}
