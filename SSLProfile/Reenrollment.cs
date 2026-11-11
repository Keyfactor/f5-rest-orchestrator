// Copyright 2026 Keyfactor
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

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.SSLProfile
{
    public class Reenrollment : ReenrollmentBase
    {
        public Reenrollment(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(ReenrollmentJobConfiguration config, SubmitReenrollmentCSR submitReenrollment) { return base.ProcessJob(config, submitReenrollment); }

        protected override string GetPartition(F5Client f5)
        {
            return f5.GetPartitionFromStorePath();
        }

        protected override void AfterCertificateInstalled(F5Client f5, string partition, string alias, bool certificateExisted)
        {
            // Same as Management Add: SSLProfiles bindings are only applied to new certificates
            string sslProfiles = GetJobProperty("SSLProfiles");
            if (!certificateExisted && !string.IsNullOrEmpty(sslProfiles))
                f5.BindCertificateToSSLProfiles(alias, sslProfiles, string.Empty);

            if (GetStoreBool("SyncDevice"))
                f5.SyncDevice(GetStoreString("SyncDeviceGroup"));
        }
    }
}
