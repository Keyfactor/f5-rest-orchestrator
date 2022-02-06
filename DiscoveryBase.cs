using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public abstract class DiscoveryBase : IDiscoveryJobExtension
    {
        protected ILogger logger;

        protected DiscoveryJobConfiguration JobConfig { get; set; }

        public string ExtensionName => string.Empty;

        public abstract string GetStoreType();

        public abstract JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery);

        protected string DiscoverActiveNode()
        {
            LogHandlerCommon.MethodEntry(logger, new CertificateStore(), "DiscoverActiveNode");
            F5Client f5 = new F5Client(new CertificateStore(), JobConfig.ServerUsername, JobConfig.ServerPassword, JobConfig.UseSSL, string.Empty, new List<PreviousInventoryItem>());
            string activeNode = f5.GetActiveNode();
            LogHandlerCommon.Debug(logger, new CertificateStore(), $"Active node '{activeNode}'");
            LogHandlerCommon.MethodExit(logger, new CertificateStore(), "DiscoverActiveNode");
            return activeNode;
        }
    }
}
