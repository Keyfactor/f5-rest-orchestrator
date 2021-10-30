using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.Bundle
{
    public class Discovery : DiscoveryBase
    {
        public override string GetStoreType()
        {
            return "F5-CA-REST";
        }

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");

            F5Client f5 = new F5Client(config);

            try
            {
                LogHandler.Debug(Logger, config, "Getting partitions");
                List<string> partitions = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandler.Trace(Logger, config, $"Found {partitions?.Count} partitions");
                List<string> locations = new List<string>();
                foreach (string partition in partitions)
                {
                    LogHandler.Trace(Logger, config, $"Getting CA Bundles for partition '{partition}'");
                    locations.AddRange(f5.GetCABundles(partition, 20).Select(p => p.fullPath).ToList());
                }

                LogHandler.Debug(Logger, config, $"Submitting {locations.Count} locations");
                sdr.Invoke(locations);

                LogHandler.Debug(Logger, config, "Job complete");
                return new AnyJobCompleteInfo { Status = 2, Message = "Successful" };
            }
            catch (Exception ex)
            {
                return new AnyJobCompleteInfo { Status = 4, Message = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the discovery operation. ") };
            }
            finally
            {
                LogHandler.MethodExit(Logger, config, "processJob");
            }
        }
    }
}
