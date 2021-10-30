using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using Keyfactor.Platform.Extensions.Agents.Enums;
using Keyfactor.Platform.Extensions.Agents.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.WebServer
{
    public class Inventory : InventoryBase
    {
        public override string GetStoreType()
        {
            return "F5-WS-REST";
        }

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");
            if (config.Job.OperationType != AnyJobOperationType.Inventory)
            {
                throw new Exception($"{GetStoreType()} expecting 'Inventory' job - received '{Enum.GetName(typeof(AnyJobOperationType), config.Job.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            List<AgentCertStoreInventoryItem> inventory = new List<AgentCertStoreInventoryItem>();

            try
            {
                LogHandler.Debug(Logger, JobConfig, "Processing job parameters");
                dynamic properties = JsonConvert.DeserializeObject(config.Store.Properties.ToString());

                F5Client f5 = new F5Client(config);

                LogHandler.Debug(Logger, JobConfig, "Getting the F5 web server device inventory");
                inventory = f5.GetWebServerInventory(config);

                LogHandler.Debug(Logger, JobConfig, "Submitting F5 web server inventory");
                submitInventory.Invoke(inventory);

                LogHandler.Debug(Logger, JobConfig, "Job complete");
                return new AnyJobCompleteInfo { Status = 2, Message = "Successful" };
            }
            catch (Exception ex)
            {
                return new AnyJobCompleteInfo { Status = 4, Message = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the inventory operation.") };
            }
            finally
            {
                LogHandler.MethodExit(Logger, config, "processJob");
            }
        }
    }
}
