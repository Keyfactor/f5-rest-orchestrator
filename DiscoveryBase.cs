using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
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
            LogHandler.MethodEntry(logger, new CertificateStore(), "DiscoverActiveNode");
            F5Client f5 = new F5Client(JobConfig);
            string activeNode = f5.GetActiveNode();
            LogHandler.Debug(logger, new CertificateStore(), $"Active node '{activeNode}'");
            LogHandler.MethodExit(logger, new CertificateStore(), "DiscoverActiveNode");
            return activeNode;
        }
    }
}
