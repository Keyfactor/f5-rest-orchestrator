using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.WebServer
{
    public class Management : ManagementBase
    {
        public override string GetStoreType()
        {
            return "F5-WS-REST";
        }

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");

            if (config.Job.OperationType != AnyJobOperationType.Add)
            {
                throw new Exception($"'{config.Store.ClientMachine}-{config.Store.StorePath}-{GetStoreType()}' expecting 'Add' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
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

                LogHandler.Trace(Logger, config, "Replacing F5 web server certificate");
                f5.ReplaceWebServerCrt();

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
    }
}
