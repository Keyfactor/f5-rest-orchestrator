using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Delegates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.SSLProfile
{
    public class Discovery : DiscoveryBase
    {
        public override string GetStoreType()
        {
            return "F5-SL-REST";
        }

        public override AnyJobCompleteInfo processJob(AnyJobConfigInfo config, SubmitInventoryUpdate submitInventory, SubmitEnrollmentRequest submitEnrollmentRequest, SubmitDiscoveryResults sdr)
        {
            LogHandler.MethodEntry(Logger, config, "processJob");
            F5Client f5 = new F5Client(config);

            try
            {
                LogHandler.Debug(Logger, config, "Getting partitions");
                List<string> locations = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandler.Debug(Logger, config, $"Submitting {locations?.Count} partitions");
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
