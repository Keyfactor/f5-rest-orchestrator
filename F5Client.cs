using CSS.Common.Logging;
using Keyfactor.Platform.Extensions.Agents.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    internal class F5Client : LoggingClientBase
    {
        #region Properties

        public AnyJobConfigInfo JobConfig { get; set; }
        public string PrimaryNode { get; set; }
        public string F5Version { get; set; }
        private RESTHandler REST
        {
            get
            {
                return new RESTHandler
                {
                    Host = this.JobConfig.Store.ClientMachine,
                    User = this.JobConfig.Server.Username,
                    Password = this.JobConfig.Server.Password,
                    UseSSL = this.JobConfig.Server.UseSSL
                };
            }
        }
        private F5Transaction Transaction { get; set; }

        // Properties
        #endregion

        #region Constructors

        public F5Client(AnyJobConfigInfo jobConfig)
        {
            JobConfig = jobConfig;
        }

        // Constructors
        #endregion

        #region Methods

        #region Certificate/PFX Shared

        public void AddEntry(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "AddEntry");
            LogHandler.Trace(Logger, JobConfig, $"Processing certificate for partition '{partition}' and name '{name}'");
            byte[] entryContents = Convert.FromBase64String(JobConfig.Job.EntryContents);
            string password = JobConfig.Job.PfxPassword;
            CSS.PKI.X509.CertificateConverter converter = CSS.PKI.X509.CertificateConverterFactory.FromDER(entryContents, password);
            X509Certificate2 certificate = converter.ToX509Certificate2(password);
            if (certificate.HasPrivateKey)
            {
                LogHandler.Trace(Logger, JobConfig, $"Certificate for partition '{partition}' and name '{name}' has a private key - performing addition");
                AddPfx(entryContents, partition, name, password);
                LogHandler.Trace(Logger, JobConfig, $"PFX addition for partition '{partition}' and name '{name}' completed");
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"Certificate for partition '{partition}' and name '{name}' does not have a private key - performing addition");
                AddCertificate(entryContents, partition, name);
                LogHandler.Trace(Logger, JobConfig, $"Certificate addition for partition '{partition}' and name '{name}' completed");
            }
            LogHandler.MethodExit(Logger, JobConfig, "AddEntry");
        }

        public void ReplaceEntry(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ReplaceEntry");
            LogHandler.Trace(Logger, JobConfig, $"Processing certificate for partition '{partition}' and name '{name}'");
            byte[] entryContents = Convert.FromBase64String(JobConfig.Job.EntryContents);
            string password = JobConfig.Job.PfxPassword;
            CSS.PKI.X509.CertificateConverter converter = CSS.PKI.X509.CertificateConverterFactory.FromDER(entryContents, password);
            X509Certificate2 certificate = converter.ToX509Certificate2(password);

            if (certificate.HasPrivateKey)
            {
                LogHandler.Trace(Logger, JobConfig, $"Certificate for partition '{partition}' and name '{name}' has a private key - performing replacement");
                ReplacePfx(entryContents, partition, name, password);
                LogHandler.Trace(Logger, JobConfig, $"PFX replacement for partition '{partition}' and name '{name}' completed");
            }
            else
            {
                Logger.Trace($"Certificate for partition '{partition}' and name '{name}' does not have a private key - performing replacement");
                ReplaceCertificate(entryContents, partition, name);
                Logger.Trace($"Certificate replacement for partition '{partition}' and name '{name}' completed");
            }
            LogHandler.MethodExit(Logger, JobConfig, "ReplaceEntry");
        }

        public void RemoveEntry(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "RemoveEntry");
            LogHandler.Trace(Logger, JobConfig, $"Processing certificate for partition '{partition}' and name '{name}'");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");
            if (KeyExists(partition, name))
            {
                LogHandler.Trace(Logger, JobConfig, $"Archiving key at '{partition}' and name '{name}'");
                ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_key_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.key");
                LogHandler.Trace(Logger, JobConfig, $"Removing certificate and key at '{partition}' and name '{name}'");

                string keyName = GetKeyName(name, true);
                REST.Delete($"/mgmt/tm/sys/file/ssl-key/~{partition}~{keyName}");
            }
            LogHandler.Trace(Logger, JobConfig, $"Archiving certificate at '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");
            LogHandler.Trace(Logger, JobConfig, $"Removing certificate at '{partition}' and name '{name}'");

            string crtName = GetCrtName(name, true);
            REST.Delete($"/mgmt/tm/sys/file/ssl-cert/~{partition}~{crtName}");
            LogHandler.MethodExit(Logger, JobConfig, "RemoveEntry");
        }

        public bool KeyExists(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "KeyExists");
            bool exists = false;

            try
            {
                string keyName = GetKeyName(name, true);
                string query = $"/mgmt/tm/sys/file/ssl-key/~{partition}~{keyName}";
                F5Key key = REST.Get<F5Key>(query);
                exists = (key != null);
            }
            catch (F5RESTException rex)
            {
                // A 404 will be returned if the key is not found
                if (rex.code != 404)
                {
                    throw;
                }
            }

            LogHandler.MethodExit(Logger, JobConfig, "KeyExists");
            return exists;
        }

        public bool CertificateExists(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "CertificateExists");
            bool exists = false;

            try
            {
                string crtName = GetCrtName(name, true);
                string query = $"/mgmt/tm/sys/file/ssl-cert/~{partition}~{crtName}";
                F5SSLProfile certificate = REST.Get<F5SSLProfile>(query);
                exists = (certificate != null);
            }
            catch (F5RESTException rex)
            {
                // A 404 will be returned if the certificate is not found
                if (rex.code != 404)
                {
                    throw;
                }
            }

            LogHandler.MethodExit(Logger, JobConfig, "CertificateExists");
            return exists;
        }

        private void AddCertificate(byte[] entryContents, string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "AddCertificate");
            LogHandler.Trace(Logger, JobConfig, $"Uploading file to {partition}-{name}");
            REST.UploadFile($"{partition}-{name}", entryContents);

            LogHandler.Trace(Logger, JobConfig, $"Installing certificate to '{name}'");
            REST.PostInstallCryptoCommand(new F5InstallCommand
            {
                command = "install",
                name = $"{name}",
                localfile = $"/var/config/rest/downloads/{partition}-{name}",
                partition = partition
            }, "cert");
            LogHandler.MethodExit(Logger, JobConfig, "AddCertificate");
        }

        private void AddPfx(byte[] entryContents, string partition, string name, string password)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "AddPfx");
            LogHandler.Trace(Logger, JobConfig, $"Uploading PFX to {partition}-{name}.p12");
            REST.UploadFile($"{partition}-{name}.p12", entryContents);

            LogHandler.Trace(Logger, JobConfig, $"Installing PFX to '{name}'");
            REST.PostInstallCryptoCommand(new F5InstallCommand
            {
                command = "install",
                name = $"{name}",
                localfile = $"/var/config/rest/downloads/{partition}-{name}.p12",
                passphrase = password,
                partition = partition
            }, "pkcs12");
            LogHandler.MethodExit(Logger, JobConfig, "AddPfx");
        }

        private void ReplaceCertificate(byte[] entryContents, string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ReplaceCertificate");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(Logger, JobConfig, $"Archiving the certificate for partition '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");

            LogHandler.Trace(Logger, JobConfig, $"Adding certificate to partition '{partition}' and name '{name}'");
            AddCertificate(entryContents, partition, name);
            LogHandler.MethodExit(Logger, JobConfig, "ReplaceCertificate");
        }

        private void ReplacePfx(byte[] entryContents, string partition, string name, string password)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ReplacePfx");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(Logger, JobConfig, $"Archiving the key and certificate for partition '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_key_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.key");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");

            LogHandler.Trace(Logger, JobConfig, $"Adding PFX to partition '{partition}' and name '{name}'");
            AddPfx(entryContents, partition, name, password);
            LogHandler.MethodExit(Logger, JobConfig, "ReplacePfx");
        }

        private X509Certificate2Collection GetCertificateEntry(string path)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetCertificateEntry");
            string certificateEntry = string.Empty;
            LogHandler.Trace(Logger, JobConfig, $"Getting certificate entry from: '{path}'");

            string crt = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'cat {path} | base64'"
            });

            byte[] crtBytes;
            switch (crt.Substring(0, 1))
            {
                //DER --> M
                case "M":
                    LogHandler.Trace(Logger, JobConfig, "Certificate is DER encoded");
                    certificateEntry = crt;
                    break;
                //PEM(no headers)-- > T
                case "T":
                    LogHandler.Trace(Logger, JobConfig, "Certificate is PEM without headers");
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    break;
                //PEM(w / headers)-- > L
                case "L":
                    LogHandler.Trace(Logger, JobConfig, "Certificate is PEM with headers");
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    break;
                default:
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    throw new Exception("Unknown certificate format found for device certificate");
            }

            LogHandler.MethodExit(Logger, JobConfig, "GetCertificateEntry");

            return CSS.PKI.X509.CertificateCollectionConverterFactory.FromPEM(certificateEntry).ToX509Certificate2Collection();
        }

        private void SetItemStatus(AgentCertStoreInventoryItem agentInventoryItem)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "SetItemStatus");
            AnyJobInventoryItem keyfactorInventoryItem = JobConfig.Store.Inventory
                .SingleOrDefault(i => i.Alias.Equals(agentInventoryItem.Alias, StringComparison.OrdinalIgnoreCase));
            if (keyfactorInventoryItem == null)
            {
                LogHandler.Trace(Logger, JobConfig, "F5 item does not exist in Keyfactor Command and will be tagged as new");
                agentInventoryItem.ItemStatus = AgentInventoryItemStatus.New;
                Logger.MethodExit();
                return;
            }

            LogHandler.Trace(Logger, JobConfig, "Matching alias found, checking entry details");
            if (keyfactorInventoryItem.PrivateKeyEntry != agentInventoryItem.PrivateKeyEntry)
            {
                LogHandler.Trace(Logger, JobConfig, "Private key entry status does not match and will be tagged as modified");
                agentInventoryItem.ItemStatus = AgentInventoryItemStatus.Modified;
                Logger.MethodExit();
                return;
            }

            LogHandler.Trace(Logger, JobConfig, "Private key entry status matches, checking certificates");
            if (keyfactorInventoryItem.Thumbprints.Length != agentInventoryItem.Certificates.Length)
            {
                LogHandler.Trace(Logger, JobConfig, $"F5 entry certificate count: {agentInventoryItem.Certificates.Length} does not match Keyfactor Command count: {keyfactorInventoryItem.Thumbprints.Length} and will be tagged as modified");
                agentInventoryItem.ItemStatus = AgentInventoryItemStatus.Modified;
                Logger.MethodExit();
                return;
            }

            LogHandler.Trace(Logger, JobConfig, "Certificate counts match, checking individual certificates");
            foreach (string pem in agentInventoryItem.Certificates)
            {
                string certificateBase64 = pem;
                certificateBase64 = Regex.Replace(certificateBase64, "\r", "");
                certificateBase64 = Regex.Replace(certificateBase64, "-----BEGIN CERTIFICATE-----\n", "");
                certificateBase64 = Regex.Replace(certificateBase64, "\\n-----END CERTIFICATE-----(\\n|)", "");

                LogHandler.Trace(Logger, JobConfig, "Getting X509 object from F5 certificate pem");
                X509Certificate2 x509 = new X509Certificate2(Convert.FromBase64String(certificateBase64));
                LogHandler.Trace(Logger, JobConfig, $"Looking for CMS thumbprint matching: '{x509.Thumbprint}'");
                if (!keyfactorInventoryItem.Thumbprints.Any(t => t.Equals(x509.Thumbprint, StringComparison.OrdinalIgnoreCase)))
                {
                    LogHandler.Trace(Logger, JobConfig, "Thumbprint not found and will be tagged as modified");
                    agentInventoryItem.ItemStatus = AgentInventoryItemStatus.Modified;
                    LogHandler.MethodExit(Logger, JobConfig, "SetItemStatus");
                    return;
                }
            }

            LogHandler.Trace(Logger, JobConfig, "The inventory item is unchanged");
            agentInventoryItem.ItemStatus = AgentInventoryItemStatus.Unchanged;
            LogHandler.MethodExit(Logger, JobConfig, "SetItemStatus");
        }

        private AgentCertStoreInventoryItem GetInventoryItem(string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetInventoryItem");

            // Get the pfx/certificate contents from the filesystem (using a wildcard as the files have slightly randomized name suffixes)
            X509Certificate2Collection certificateCollection = GetCertificateEntry($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*");
            List<string> certContents = new List<string>();
            bool useChainLevel = certificateCollection.Count > 1;
            bool privateKeyEntry = false;
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                if (certificate.HasPrivateKey) { privateKeyEntry = true; }
                certContents.Add(Convert.ToBase64String(certificate.Export(X509ContentType.Cert)));
            }

            string crtName = GetCrtName(name, false);
            AgentCertStoreInventoryItem inventoryItem = new AgentCertStoreInventoryItem
            {
                ItemStatus = AgentInventoryItemStatus.Unknown,
                Alias = crtName,
                PrivateKeyEntry = privateKeyEntry,
                UseChainLevel = useChainLevel,
                Certificates = certContents.ToArray()
            };
            SetItemStatus(inventoryItem);
            LogHandler.MethodExit(Logger, JobConfig, "GetInventoryItem");
            return inventoryItem;
        }

        private string GetCrtName(string name, bool addExtension)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetCrtName");
            string crtName = name;

            switch (F5Version.ToLowerInvariant())
            {
                case "v13":
                    if (addExtension)
                    {
                        // The .crt extension must be added
                        if (!crtName.EndsWith(".crt", StringComparison.OrdinalIgnoreCase)) { crtName = $"{crtName}.crt"; }
                    }
                    else
                    {
                        // The .crt extension must be removed
                        if (crtName.EndsWith(".crt", StringComparison.OrdinalIgnoreCase)) { crtName = crtName.Substring(0, crtName.Length - 4); }
                    }
                    break;
                case "v14":
                    // No action needed, this version does not use extensions
                    break;
                case "v15":
                    // No action needed, this version does not use extensions
                    break;
                default:
                    throw new Exception($"The provided F5 version: '{F5Version}' is not supported.");
            };

            LogHandler.MethodExit(Logger, JobConfig, "GetCrtName");
            return crtName;
        }

        private string GetKeyName(string name, bool addExtension)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetKeyName");
            string keyName = name;

            switch (F5Version.ToLowerInvariant())
            {
                case "v13":
                    if (addExtension)
                    {
                        // The .key extension must be added
                        if (!keyName.EndsWith(".key", StringComparison.OrdinalIgnoreCase)) { keyName = $"{keyName}.key"; }
                    }
                    else
                    {
                        // The .key extension must be removed
                        if (keyName.EndsWith(".key", StringComparison.OrdinalIgnoreCase)) { keyName = keyName.Substring(0, keyName.Length - 4); }
                    }
                    break;
                case "v14":
                    // No action needed, this version does not use extensions
                    break;
                case "v15":
                    // No action needed, this version does not use extensions
                    break;
                default:
                    throw new Exception($"The provided F5 version: '{F5Version}' is not supported.");
            };

            LogHandler.MethodExit(Logger, JobConfig, "GetKeyName");
            return keyName;
        }

        // Certificate PFX Shared
        #endregion

        #region Infrastructure

        public bool PrimaryNodeActive()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "PrimaryNodeActive");
            F5NodeDevice device = REST.Get<F5NodeDevice>($"/mgmt/tm/cm/device/{PrimaryNode}?$select=name,failoverState");
            bool nodeActive = device.failoverState.Equals("active", StringComparison.OrdinalIgnoreCase);
            LogHandler.MethodExit(Logger, JobConfig, "PrimaryNodeActive");

            return nodeActive;
        }

        public string GetActiveNode()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetActiveNode");
            string activeNode = string.Empty;
            F5NodeDeviceList devices = REST.Get<F5NodeDeviceList>($"/mgmt/tm/cm/device?$select=name,failoverState");
            foreach (F5NodeDevice device in devices.items)
            {
                if (device.failoverState.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    activeNode = device.name;
                    break;
                }
            }
            if (!string.IsNullOrEmpty(activeNode))
            {
                LogHandler.Warn(Logger, JobConfig, "No active node found, returning an empty device name");
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"'{activeNode}' is currently active and will be considered the primary node");
            }
            LogHandler.MethodExit(Logger, JobConfig, "GetActiveNode");

            return activeNode;
        }

        public List<F5Partition> GetPartitions()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetPartitions");
            F5PartitionList partitions = REST.Get<F5PartitionList>($"/mgmt/tm/auth/partition?$select=name,fullPath");
            LogHandler.MethodExit(Logger, JobConfig, "GetPartitions");

            return partitions.items.ToList<F5Partition>();
        }

        public string GetPartitionFromStorePath()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetPartitionsFromStorePath");
            string[] pathParts = JobConfig.Store.StorePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 1) { throw new Exception($"The store path '{JobConfig.Store.StorePath}' does not appear to contain a partition"); }
            LogHandler.MethodExit(Logger, JobConfig, "GetPartitionFromStorePath");
            return pathParts[0];
        }

        // Infrastructure
        #endregion

        #region Web Server

        public List<AgentCertStoreInventoryItem> GetWebServerInventory(AnyJobConfigInfo jobConfig)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetWebServerInventory");
            List<AgentCertStoreInventoryItem> inventory = new List<AgentCertStoreInventoryItem>();
            X509Certificate2Collection certificateCollection = GetCertificateEntry("/config/httpd/conf/ssl.crt/server.crt");
            List<string> webServerInventory = new List<string>();
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                webServerInventory.Add(Convert.ToBase64String(certificate.RawData));
            }

            LogHandler.Trace(Logger, JobConfig, $"Obtained F5 web server device inventory");
            AgentCertStoreInventoryItem inventoryItem = new AgentCertStoreInventoryItem
            {
                Alias = "WebServer",
                PrivateKeyEntry = true,
                ItemStatus = AgentInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = webServerInventory.ToArray()
            };
            SetItemStatus(inventoryItem);
            inventory.Add(inventoryItem);
            LogHandler.MethodExit(Logger, JobConfig, "GetWebServerInventory");

            return inventory;
        }

        public void ReplaceWebServerCrt()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ReplaceWebServerCrt");
            LogHandler.Trace(Logger, JobConfig, "Processing web server certificate");
            byte[] devicePfx = Convert.FromBase64String(JobConfig.Job.EntryContents);
            string password = JobConfig.Job.PfxPassword;
            CSS.PKI.X509.CertificateCollectionConverter converter = CSS.PKI.X509.CertificateCollectionConverterFactory.FromDER(devicePfx, password);
            string pfxPem = converter.ToPEM(password);
            List<X509Certificate2> clist = converter.ToX509Certificate2List(password);

            StringBuilder certPemBuilder = new StringBuilder();



            //////// THE LIST MUST BE REVERSED SO THAT THE END-ENTITY CERT IS FIRST /////////
            //////// CAN IT BE ASSUMED THE LAST ENTRY IS END-ENTIT? /////////////////////////
            clist.Reverse();
            /////////////////////////////////////////////////////////////////////////////////

            LogHandler.Trace(Logger, JobConfig, "Building certificate PEM");
            foreach (X509Certificate2 cert in clist)
            {
                certPemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                certPemBuilder.AppendLine(
                    Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
                certPemBuilder.AppendLine("-----END CERTIFICATE-----");
            }

            LogHandler.Trace(Logger, JobConfig, "Building key PEM");
            byte[] pkBytes = CSS.PKI.PrivateKeys.PrivateKeyConverterFactory.FromPKCS12(devicePfx, password).ToPkcs8BlobUnencrypted();
            StringBuilder keyPemBuilder = new StringBuilder();
            keyPemBuilder.AppendLine("-----BEGIN PRIVATE KEY-----");
            keyPemBuilder.AppendLine(
                Convert.ToBase64String(pkBytes, Base64FormattingOptions.InsertLineBreaks));
            keyPemBuilder.AppendLine("-----END PRIVATE KEY-----");

            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(Logger, JobConfig, "Uploading web server certificate");
            byte[] certbytes = Encoding.ASCII.GetBytes(certPemBuilder.ToString());
            RESTHandler rest = REST;
            rest.UploadFile($"kyfcert-{timestamp}.crt", certbytes);

            LogHandler.Trace(Logger, JobConfig, "Uploading the web server key");
            byte[] keybytes = Encoding.ASCII.GetBytes(keyPemBuilder.ToString());
            rest.UploadFile($"kyfcert-{timestamp}.key", keybytes);

            F5Transaction transaction = BeginTransaction();

            LogHandler.Trace(Logger, JobConfig, "Archiving the web server certificate and key");
            ArchiveFile("/config/httpd/conf/ssl.key/server.key", $"server-{timestamp}.key", transaction.transid);
            ArchiveFile("/config/httpd/conf/ssl.crt/server.crt", $"server-{timestamp}.crt", transaction.transid);

            LogHandler.Trace(Logger, JobConfig, "Replacing the web server certificate and key");
            CopyFile($"/var/config/rest/downloads/kyfcert-{timestamp}.key", "/config/httpd/conf/ssl.key/server.key", transaction.transid);
            CopyFile($"/var/config/rest/downloads/kyfcert-{timestamp}.crt", "/config/httpd/conf/ssl.crt/server.crt", transaction.transid);

            CommitTransaction(transaction);
            LogHandler.MethodExit(Logger, JobConfig, "ReplaceWebServerCrt");
        }

        // WebServer
        #endregion

        #region SSL Profiles

        public List<AgentCertStoreInventoryItem> GetSSLProfiles(int pageSize)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetSSLProfiles");
            string partition = JobConfig.Store.StorePath;
            string query = $"/mgmt/tm/sys/file/ssl-cert?$filter=partition+eq+{partition}&$select=name,isBundle&$top={pageSize}&$skip=0";
            F5PagedSSLProfiles pagedProfiles = REST.Get<F5PagedSSLProfiles>(query);
            List<F5SSLProfile> profiles = new List<F5SSLProfile>();
            List<AgentCertStoreInventoryItem> inventory = new List<AgentCertStoreInventoryItem>();

            if (pagedProfiles.totalItems == 0 || pagedProfiles.items?.Length == 0)
            {
                LogHandler.Trace(Logger, JobConfig, $"No SSL profiles found in partition '{partition}'");
                Logger.MethodExit();
                return inventory;
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"Compiling {pagedProfiles.totalPages} pages containing {pagedProfiles.totalItems} total inventory entries");
            }

            // Collected all of the profile entry names
            for (int i = pagedProfiles.pageIndex; i <= pagedProfiles.totalPages; i++)
            {
                profiles.AddRange(pagedProfiles.items);

                // The current paged profile will contain a link to the next set, unless the end has been reached
                if (string.IsNullOrEmpty(pagedProfiles.nextLink)) { break; }

                // Get the next page of profiles
                query = pagedProfiles.nextLink.Replace("https://localhost", "");
                pagedProfiles = REST.Get<F5PagedSSLProfiles>(query);
            }

            // Compile the entries into inventory items
            for (int i = 0; i < profiles.Count; i++)
            {
                try
                {
                    // Exclude 'ca-bundle.crt' as that can only be managed by F5
                    if (profiles[i].name.Equals("ca-bundle.crt", StringComparison.OrdinalIgnoreCase)
                        || profiles[i].name.Equals("f5-ca-bundle.crt", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Trace($"Skipping '{profiles[i].name}' because it is managed by F5");
                        continue;
                    }
                    inventory.Add(GetInventoryItem(partition, profiles[i].name));
                }
                catch (Exception ex)
                {
                    Logger.Error(ExceptionHandler.FlattenExceptionMessages(ex, $"Unable to process inventory item {profiles[i].name}."));
                }
            }

            LogHandler.MethodExit(Logger, JobConfig, "GetSSLProfiles");
            return inventory;
        }

        // SSL Profiles
        #endregion

        #region Bundles

        public List<F5CABundle> GetCABundles(string partition, int pageSize)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetCABundles");
            string query = $"/mgmt/tm/sys/crypto/ca-bundle-manager?$filter=partition+eq+{partition}&$select=name,fullPath&$top={pageSize}&$skip=0";
            F5PagedCABundles pagedBundles = REST.Get<F5PagedCABundles>(query);
            List<F5CABundle> bundles = new List<F5CABundle>();

            if (pagedBundles.totalItems == 0 || pagedBundles.items?.Length == 0)
            {
                LogHandler.Trace(Logger, JobConfig, $"No CA bundles found in partition '{partition}'");
                Logger.MethodExit();
                return bundles;
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"Compiling {pagedBundles.totalPages} pages containing {pagedBundles.totalItems} total CA bundles");
            }

            for (int i = pagedBundles.pageIndex; i <= pagedBundles.totalPages; i++)
            {
                foreach (F5CABundle bundle in pagedBundles.items)
                {
                    // Remove 'ca-bundle'
                    if (bundle.name.Equals("ca-bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Trace($"Skipping '{bundle.name}' because it is managed by F5");
                        continue;
                    }
                    bundles.Add(bundle);
                }

                if (string.IsNullOrEmpty(pagedBundles.nextLink)) { break; }

                query = pagedBundles.nextLink.Replace("https://localhost", "");
                pagedBundles = REST.Get<F5PagedCABundles>(query);
            }

            LogHandler.MethodExit(Logger, JobConfig, "GetCABundles");
            return bundles;
        }

        public List<AgentCertStoreInventoryItem> GetCABundleInventory()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetCABundleInventory");
            List<AgentCertStoreInventoryItem> inventory = new List<AgentCertStoreInventoryItem>();
            string[] includeBundle = GetCABundleIncludes();

            LogHandler.Trace(Logger, JobConfig, $"Compiling {includeBundle.Length} bundled certificates");
            for (int i = 0; i < includeBundle.Length; i++)
            {
                try
                {
                    LogHandler.Trace(Logger, JobConfig, $"Processing certificate '{includeBundle[i]}'");
                    string[] crtPathParts = includeBundle[i].Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (crtPathParts.Length != 2) { throw new Exception($"Bundled certificate path: '{includeBundle[i]}' is invalid.  Expecting '/<partition>/<certificate>'."); }
                    string partition = crtPathParts[0];
                    string crtName = crtPathParts[1];

                    LogHandler.Trace(Logger, JobConfig, $"Adding inventory item for partition '{partition}' and name '{crtName}'");
                    inventory.Add(GetInventoryItem(partition, crtName));
                }
                catch (Exception ex)
                {
                    Logger.Error(ExceptionHandler.FlattenExceptionMessages(ex, $"Unable to process inventory item {includeBundle[i]}."));
                }
            }

            LogHandler.MethodExit(Logger, JobConfig, "GetCABundleInventory");
            return inventory;
        }

        public bool EntryExistsInBundle()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "EntryExistsInBundle");
            bool exists = false;

            List<string> bundleIncludes = new List<string>(GetCABundleIncludes());
            string partition = GetPartitionFromStorePath();

            string crtName = GetCrtName(JobConfig.Job.Alias, true);
            exists = bundleIncludes.Any<string>(i => i.Equals($"/{partition}/{crtName}", StringComparison.OrdinalIgnoreCase));

            LogHandler.MethodExit(Logger, JobConfig, "EntryExistsInBundle");
            return exists;
        }

        private string[] GetCABundleIncludes()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "GetCABundleIncludes");
            string bundlePath = JobConfig.Store.StorePath;
            string[] bundlePathParts = bundlePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (bundlePathParts.Length != 2) { throw new Exception($"CA bundle path: '{bundlePath}' is invalid.  Expecting '/<partition>/<bundle>'."); }
            string partition = bundlePathParts[0];
            string bundleName = bundlePathParts[1];
            string query = $"/mgmt/tm/sys/crypto/ca-bundle-manager/~{partition}~{bundleName}?$select=includeBundle";
            F5CABundle bundle = REST.Get<F5CABundle>(query);
            string[] includeBundle;
            if (bundle.includeBundle == null)
            {
                LogHandler.Trace(Logger, JobConfig, $"Found 0 included bundles");
                includeBundle = new List<string>().ToArray();
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"Found {bundle.includeBundle.Count()} included bundles");
                includeBundle = bundle.includeBundle;
            }

            LogHandler.MethodExit(Logger, JobConfig, "GetCABundleIncludes");
            return includeBundle;
        }

        public void AddBundleEntry(string bundle, string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "AddBundleEntry");

            // Add the entry to inventory
            if (!CertificateExists(partition, name))
            {
                Logger.Debug($"Add entry '{name}' in '{JobConfig.Store.StorePath}'");
                AddEntry(partition, name);
            }
            else
            {
                if (!JobConfig.Job.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                Logger.Debug($"Replace entry '{name}' in '{JobConfig.Store.StorePath}'");
                ReplaceEntry(partition, name);
            }

            // Add the entry to the bundle
            string crtName = GetCrtName(name, true);
            string crt = $"/{partition}/{crtName}";
            List<string> bundleIncludes = new List<string>(GetCABundleIncludes());
            if (!bundleIncludes.Contains(crt))
            {
                bundleIncludes.Add(crt);
                F5BundleInclude bundleInclude = new F5BundleInclude { includeBundle = bundleIncludes.ToArray() };
                REST.Patch<F5BundleInclude>($"/mgmt/tm/sys/crypto/ca-bundle-manager/{bundle.Replace('/', '~')}", bundleInclude);
            }
            LogHandler.MethodExit(Logger, JobConfig, "AddBundleEntry");
        }

        public void RemoveBundleEntry(string bundle, string partition, string name)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "RemoveBundleEntry");

            string crtName = GetCrtName(name, true);
            string crtEntry = $"/{partition}/{crtName}";

            LogHandler.Trace(Logger, JobConfig, $"Preparing to remove bundle entry '{crtEntry}'");
            List<string> bundleIncludes = new List<string>(GetCABundleIncludes());
            if (bundleIncludes.Contains(crtEntry))
            {
                LogHandler.Trace(Logger, JobConfig, $"The current bundle contains entry '{crtEntry}' - adding removal operation to transaction");
                bundleIncludes.Remove(crtEntry);
                F5BundleInclude bundleInclude = new F5BundleInclude { includeBundle = bundleIncludes.ToArray() };
                REST.Patch<F5BundleInclude>($"/mgmt/tm/sys/crypto/ca-bundle-manager/{bundle.Replace('/', '~')}", bundleInclude);
            }
            else
            {
                LogHandler.Trace(Logger, JobConfig, $"The current bundle does not contain entry '{crtEntry}'");
            }
            LogHandler.MethodExit(Logger, JobConfig, "RemoveBundleEntry");
        }

        // Bundles
        #endregion

        #region File Handling

        private void ArchiveFile(string sourcePath, string targetFilename, string transactionId = "")
        {
            LogHandler.MethodEntry(Logger, JobConfig, "ArchiveFile");

            // Make the 'keyfactor' directory if it doesn't exist
            string mkdirResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'mkdir -p /var/config/rest/downloads/keyfactor'"
            }, transactionId);

            CopyFile(sourcePath, $"/var/config/rest/downloads/keyfactor/{targetFilename}", transactionId);
            LogHandler.MethodExit(Logger, JobConfig, "ArchiveFile");
        }

        private void CopyFile(string source, string target, string transactionId = "")
        {
            LogHandler.MethodEntry(Logger, JobConfig, "CopyFile");
            string copyResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'cp {source} {target}'"
            }, transactionId);
            LogHandler.MethodExit(Logger, JobConfig, "CopyFile");
        }

        private void MoveFile(string source, string target, string transactionId = "")
        {
            LogHandler.MethodEntry(Logger, JobConfig, "MoveFile");
            string moveResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'mv {source} {target}'"
            }, transactionId);
            LogHandler.MethodExit(Logger, JobConfig, "MoveFile");
        }

        private void RemoveFile(string source, string transactionId = "")
        {
            LogHandler.MethodEntry(Logger, JobConfig, "RemoveFile");
            string removeResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'rm {source}'"
            }, transactionId);
            LogHandler.MethodExit(Logger, JobConfig, "RemoveFile");
        }

        // File Handling
        #endregion

        #region Transactions

        private F5Transaction BeginTransaction()
        {
            LogHandler.MethodEntry(Logger, JobConfig, "BeingTransaction");
            F5Transaction transaction = REST.Post<F5Transaction>("/mgmt/tm/transaction", "{}");
            LogHandler.Trace(Logger, JobConfig, $"Initiated transaction '{transaction.transid}'");
            LogHandler.MethodExit(Logger, JobConfig, "BeingTransaction");
            return transaction;
        }

        private void CommitTransaction(F5Transaction transaction)
        {
            LogHandler.MethodEntry(Logger, JobConfig, "CommitTransaction");
            LogHandler.Trace(Logger, JobConfig, $"Committing transaction '{transaction.transid};");
            REST.Patch<F5CommitTransaction>($"/mgmt/tm/transaction/{transaction.transid}", new F5CommitTransaction { state = "VALIDATING", validateOnly = false });
            LogHandler.MethodExit(Logger, JobConfig, "CommitTransaction");
        }

        // Transactions
        #endregion

        // Methods
        #endregion
    }
}
