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
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Profile
{
    public class Discovery : DiscoveryBase
    {
        private const int DEFAULT_PROFILE_PAGE_SIZE = 50;
        private const string CLIENT_SSL_ENDPOINT = "client-ssl";
        private const string SERVER_SSL_ENDPOINT = "server-ssl";

        public Discovery(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate sdr)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            CertificateStore certificateStore = new CertificateStore() { ClientMachine = config.ClientMachine };
            LogHandlerCommon.MethodEntry(logger, certificateStore, "ProcessJob");

            try
            {
                LogHandlerCommon.Debug(logger, certificateStore, "Getting partitions");

                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);

                F5Client f5 = new F5Client(certificateStore, ServerUserName, ServerPassword, config.UseSSL, string.Empty, true, false, new List<PreviousInventoryItem>());

                ValidateF5Release(logger, certificateStore, f5);

                List<string> partitions = f5.GetPartitions().Select(p => p.name).ToList();

                LogHandlerCommon.Trace(logger, certificateStore, $"Found {partitions?.Count} partitions");
                List<string> locations = new List<string>();
                foreach (string partition in partitions)
                {
                    foreach (string profileType in new[] { "Client", "Server" })
                    {
                        string profileEndpoint = profileType.Equals("Server", StringComparison.OrdinalIgnoreCase) ? SERVER_SSL_ENDPOINT : CLIENT_SSL_ENDPOINT;
                        LogHandlerCommon.Trace(logger, certificateStore, $"Getting {profileType} SSL profiles for partition '{partition}'");
                        List<F5SSLProfile> profiles = f5.GetSSLProfiles(DEFAULT_PROFILE_PAGE_SIZE, profileEndpoint, partition);

                        foreach (F5SSLProfile profile in profiles)
                        {
                            if (profile.defaultsFrom.Substring(0,1) == $"/")
                            {
                                profile.defaultsFrom = profile.defaultsFrom.Substring(1);
                            }
                            //string inheritedProfile = string.Empty;
                            //if (!string.IsNullOrEmpty(profile.defaultsFrom))
                            //{
                            //    string[] inheritedParts = profile.defaultsFrom.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            //    inheritedProfile = inheritedParts[inheritedParts.Length - 1];
                            //}

                            string location = string.IsNullOrEmpty(profile.defaultsFrom)
                                ? $"{partition}\\{profile.name}\\{profileType}"
                                : $"{partition}\\{profile.name}\\{profileType}\\{profile.defaultsFrom}";
                            locations.Add(location);
                        }
                    }
                }

                LogHandlerCommon.Debug(logger, certificateStore, $"Submitting {locations.Count} locations");
                sdr.Invoke(locations);

                LogHandlerCommon.Debug(logger, certificateStore, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                LogHandlerCommon.Error(logger, certificateStore, ExceptionHandler.FlattenExceptionMessages(ex, $"Error performing Discovery."));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the discovery operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, certificateStore, "ProcessJob");
            }
        }
    }
}
