using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public class F5JobBase
    {
        protected string ServerUserName { get; set; }

        protected string ServerPassword { get; set; }

        public IPAMSecretResolver _resolver;

        internal void SetPAMSecrets(string serverUserName, string serverPassword, ILogger logger)
        {
            ServerUserName = PAMUtilities.ResolvePAMField(_resolver, logger, "Server User Name", serverUserName);
            ServerPassword = PAMUtilities.ResolvePAMField(_resolver, logger, "Server Password", serverPassword);
        }
    }
}
