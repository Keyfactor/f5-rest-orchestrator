using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public abstract class DiscoveryBase : F5JobBase, IDiscoveryJobExtension
    {
        protected ILogger logger;

        protected DiscoveryJobConfiguration JobConfig { get; set; }

        public string ExtensionName => string.Empty;

        public abstract JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate submitDiscovery);
    }
}
