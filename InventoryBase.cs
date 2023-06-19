// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public abstract class InventoryBase : F5JobBase, IInventoryJobExtension
    {
        protected ILogger logger;

        protected InventoryJobConfiguration JobConfig { get; set; }

        protected string F5Version { get; set; }
        protected bool IgnoreSSLWarning { get; set; }

        public string ExtensionName => string.Empty;

        public abstract JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory);

        protected void ParseJobProperties()
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());

            if (string.IsNullOrEmpty(properties.F5Version?.ToString())) { throw new Exception("Missing job property string: F5Version"); }
            F5Version = properties.F5Version.ToString();
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"F5 version '{F5Version}'");

            IgnoreSSLWarning = properties.IgnoreSSLWarning == null || string.IsNullOrEmpty(properties.IgnoreSSLWarning.Value) ? false : bool.Parse(properties.IgnoreSSLWarning.Value);
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Ignore SSL Warnings '{IgnoreSSLWarning.ToString()}'");
        }
    }
}
