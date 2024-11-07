// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
using Keyfactor.Orchestrators.Extensions;
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

        protected string StorePassword { get; set; }

        public IPAMSecretResolver _resolver;

        internal void SetPAMSecrets(string serverUserName, string serverPassword, ILogger logger)
        {
            ServerUserName = PAMUtilities.ResolvePAMField(_resolver, logger, "Server User Name", serverUserName);
            ServerPassword = PAMUtilities.ResolvePAMField(_resolver, logger, "Server Password", serverPassword);
        }

        internal void SetPAMSecrets(string serverUserName, string serverPassword, string storePassword, ILogger logger)
        {
            ServerUserName = PAMUtilities.ResolvePAMField(_resolver, logger, "Server User Name", serverUserName);
            ServerPassword = PAMUtilities.ResolvePAMField(_resolver, logger, "Server Password", serverPassword);
            StorePassword = PAMUtilities.ResolvePAMField(_resolver, logger, "Store Password", storePassword);
        }

        internal void ValidateF5Release(ILogger logger, CertificateStore certificateStore, F5Client f5Client)
        {
            LogHandlerCommon.MethodEntry(logger, certificateStore, "ValidateF5Release");

            f5Client.ValidateF5Version();

            LogHandlerCommon.MethodExit(logger, certificateStore, "ValidateF5Release");
        }
    }
}
