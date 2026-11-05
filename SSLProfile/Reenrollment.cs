// Copyright 2023 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use this file except in compliance with the License.  You may obtain a
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless
// required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// thespecific language governing permissions and limitations under the
// License.
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.SSLProfile
{
    public class Reenrollment : ReenrollmentBase
    {
        public Reenrollment(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        protected override string GetPartition(F5Client f5)
        {
            return f5.GetPartitionFromStorePath();
        }

        protected override void InstallCertificate(F5Client f5, string partition, string alias, string b64Pfx, bool certificateExists)
        {
            if (certificateExists)
            {
                f5.ReplaceEntry(partition, alias, b64Pfx, StorePassword);
            }
            else
            {
                f5.AddEntry(partition, alias, b64Pfx, StorePassword);

                // Same as Management Add: bindings are only applied to new certificates
                string sslProfiles = GetJobProperty("SSLProfiles");
                if (!string.IsNullOrEmpty(sslProfiles))
                    f5.BindCertificateToSSLProfiles(alias, sslProfiles, StorePassword);
            }

            if (GetStoreBool("SyncDevice"))
                f5.SyncDevice(GetStoreString("SyncDeviceGroup"));
        }
    }
}
