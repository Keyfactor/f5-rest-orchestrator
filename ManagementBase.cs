// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    public abstract class ManagementBase : F5JobBase, IManagementJobExtension
    {
        protected ILogger logger; 
        
        protected ManagementJobConfiguration JobConfig { get; set; }

        protected bool PrimaryNodeOnlineRequired { get; set; }
        protected string PrimaryNode { get; set; }
        protected int PrimaryNodeRetryMax { get; set; }
        protected int PrimaryNodeRetryWaitSecs { get; set; }
        protected int _primaryNodeRetryCount = 0;
        protected bool IgnoreSSLWarning { get; set; }
        protected bool UseTokenAuth { get; set; }

        public string ExtensionName => "Keyfactor.Extensions.Orchestrator.F5Orchestrator.Management";

        public abstract JobResult ProcessJob(ManagementJobConfiguration config);

        protected void ParseJobProperties()
        {
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }

            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            dynamic properties = JsonConvert.DeserializeObject(JobConfig.CertificateStoreDetails.Properties.ToString());
            PrimaryNodeOnlineRequired = false;
            PrimaryNode = string.Empty;
            PrimaryNodeRetryMax = 3;
            PrimaryNodeRetryWaitSecs = 120;
            bool primaryNodeRequired = false;
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, "Attempting to determine if the primary node is required to be active");
            //if (properties.PrimaryNodeOnlineRequired == null) { throw new Exception("Missing job property string: PrimaryNodeOnlineRequired"); }
            bool.TryParse(properties.PrimaryNodeOnlineRequired?.ToString(), out primaryNodeRequired);
            PrimaryNodeOnlineRequired = primaryNodeRequired;
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node online required '{PrimaryNodeOnlineRequired}'");

            if (PrimaryNodeOnlineRequired)
            {
                if (string.IsNullOrEmpty(properties.PrimaryNode?.ToString())) { throw new Exception("Missing job property string: PrimaryNode"); }
                PrimaryNode = properties.PrimaryNode.ToString();
                LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node '{PrimaryNode}'");

                if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryMax?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryMax"); }
                int primaryNodeRetryMax = 3;
                int.TryParse(properties.PrimaryNodeCheckRetryMax.ToString(), out primaryNodeRetryMax);
                PrimaryNodeRetryMax = primaryNodeRetryMax;
                LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node retry count '{PrimaryNodeRetryMax}'");

                if (string.IsNullOrEmpty(properties.PrimaryNodeCheckRetryWaitSecs?.ToString())) { throw new Exception("Missing job property string: PrimaryNodeCheckRetryWaitSecs"); }
                int primaryNodeRetryWaitSecs = 120;
                int.TryParse(properties.PrimaryNodeCheckRetryWaitSecs.ToString(), out primaryNodeRetryWaitSecs);
                PrimaryNodeRetryWaitSecs = primaryNodeRetryWaitSecs;
                LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Primary node retry wait seconds '{PrimaryNodeRetryWaitSecs}'");
                LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "ParseJobProperties");
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "The primary node is not required to be active");
            }

            IgnoreSSLWarning = properties.IgnoreSSLWarning == null || string.IsNullOrEmpty(properties.IgnoreSSLWarning.Value) ? false : bool.Parse(properties.IgnoreSSLWarning.Value);
            UseTokenAuth = properties.UseTokenAuth == null || string.IsNullOrEmpty(properties.UseTokenAuth.Value) ? false : bool.Parse(properties.UseTokenAuth.Value);
            LogHandlerCommon.Trace(logger, JobConfig.CertificateStoreDetails, $"Ignore SSL Warnings '{IgnoreSSLWarning.ToString()}'");
        }

        protected void PrimaryNodeActive()
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PrimaryNodeActive");

            if (PrimaryNodeOnlineRequired)
            {
                F5Client f5 = new F5Client(JobConfig.CertificateStoreDetails, ServerUserName, ServerPassword, JobConfig.UseSSL, JobConfig.JobCertificate.PrivateKeyPassword, IgnoreSSLWarning, UseTokenAuth, JobConfig.LastInventory)
                { PrimaryNode = this.PrimaryNode };
                if (!f5.PrimaryNodeActive())
                {
                    LogHandlerCommon.Warn(logger, JobConfig.CertificateStoreDetails, $"The primary node: '{PrimaryNode}' is not active on try '{_primaryNodeRetryCount++}' of '{PrimaryNodeRetryMax}'");
                    if (_primaryNodeRetryCount == PrimaryNodeRetryMax)
                    {
                        throw new Exception($"The primary node: '{PrimaryNode}' is not active and the maximum number of retries '{PrimaryNodeRetryMax}' has been reached");
                    }

                    LogHandlerCommon.Warn(logger, JobConfig.CertificateStoreDetails, $"Waiting for '{PrimaryNodeRetryWaitSecs}' seconds before checking if '{PrimaryNode}' is active");
                    Thread.Sleep(PrimaryNodeRetryWaitSecs * 1000);

                    LogHandlerCommon.Warn(logger, JobConfig.CertificateStoreDetails, $"Checking '{PrimaryNode}' again");
                    PrimaryNodeActive();
                }
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "The primary node is not required to be active");
            }
            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PrimaryNodeActive");
        }
    }
}
