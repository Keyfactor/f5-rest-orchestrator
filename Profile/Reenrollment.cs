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

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Profile
{
    public class Reenrollment : ReenrollmentBase
    {
        private F5ProfileStorePath _profilePath;

        public Reenrollment(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        protected override string GetPartition(F5Client f5)
        {
            // Same requirement as Management - validated here so it fails before anything is created on the F5
            if (string.IsNullOrEmpty(GetStoreString("AliasFormat")))
            {
                throw new System.Exception("Alias format is required.");
            }

            _profilePath = f5.ParseProfileStorePath();
            return _profilePath.Partition;
        }

        protected override void InstallCertificate(F5Client f5, string partition, string alias, string b64Pfx, bool certificateExists)
        {
            f5.AddOrReplaceProfileCertificate(partition, F5Client.GetProfileEndpoint(_profilePath.ProfileType), _profilePath.ProfileName,
                alias, b64Pfx, StorePassword, GetStoreString("AliasFormat"), JobConfig.Overwrite);
        }
    }
}
