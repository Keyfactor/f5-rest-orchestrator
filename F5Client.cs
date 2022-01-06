﻿using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.PKI.X509;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    internal class F5Client
    {
        #region Properties

        protected ILogger logger;

        public CertificateStore CertificateStore { get; set; }
        public string ServerUserName { get; set; }
        public string ServerPassword { get; set; }
        public bool UseSSL { get; set; }
        public string PFXPassword { get; set; }
        public IEnumerable<PreviousInventoryItem> Inventory { get; set; }
        public string PrimaryNode { get; set; }
        public string F5Version { get; set; }
        private RESTHandler REST
        {
            get
            {
                return new RESTHandler()
                {
                    Host = this.CertificateStore.ClientMachine,
                    User = this.ServerUserName,
                    Password = this.ServerPassword,
                    UseSSL = this.UseSSL
                };
            }
        }
        private F5Transaction Transaction { get; set; }

        // Properties
        #endregion

        #region Constructors

        public F5Client(CertificateStore certificateStore, string serverUserName, string serverPassword, bool useSSL, string pfxPassword, IEnumerable<PreviousInventoryItem> inventory)
        {
            CertificateStore = certificateStore;
            ServerUserName = serverUserName;
            ServerPassword = serverPassword;
            UseSSL = useSSL;
            PFXPassword = pfxPassword;
            Inventory = inventory;
            
            if (logger == null)
            {
                logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            }
        }

        // Constructors
        #endregion

        #region Methods

        #region Certificate/PFX Shared

        public void AddEntry(string partition, string name, string b64Certificate)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "AddEntry");
            LogHandler.Trace(logger, CertificateStore, $"Processing certificate for partition '{partition}' and name '{name}'");
            byte[] entryContents = Convert.FromBase64String(b64Certificate);
            string password = PFXPassword;
            CertificateConverter converter = CertificateConverterFactory.FromDER(entryContents, password);
            X509Certificate2 certificate = converter.ToX509Certificate2(password);
            if (certificate.HasPrivateKey)
            {
                LogHandler.Trace(logger, CertificateStore, $"Certificate for partition '{partition}' and name '{name}' has a private key - performing addition");
                AddPfx(entryContents, partition, name, password);
                LogHandler.Trace(logger, CertificateStore, $"PFX addition for partition '{partition}' and name '{name}' completed");
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"Certificate for partition '{partition}' and name '{name}' does not have a private key - performing addition");
                AddCertificate(entryContents, partition, name);
                LogHandler.Trace(logger, CertificateStore, $"Certificate addition for partition '{partition}' and name '{name}' completed");
            }
            LogHandler.MethodExit(logger, CertificateStore, "AddEntry");
        }

        public void ReplaceEntry(string partition, string name, string b64Certificate)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "ReplaceEntry");
            LogHandler.Trace(logger, CertificateStore, $"Processing certificate for partition '{partition}' and name '{name}'");
            byte[] entryContents = Convert.FromBase64String(b64Certificate);
            string password = PFXPassword;
            CertificateConverter converter = CertificateConverterFactory.FromDER(entryContents, password);
            X509Certificate2 certificate = converter.ToX509Certificate2(password);

            if (certificate.HasPrivateKey)
            {
                LogHandler.Trace(logger, CertificateStore, $"Certificate for partition '{partition}' and name '{name}' has a private key - performing replacement");
                ReplacePfx(entryContents, partition, name, password);
                LogHandler.Trace(logger, CertificateStore, $"PFX replacement for partition '{partition}' and name '{name}' completed");
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"Certificate for partition '{partition}' and name '{name}' does not have a private key - performing replacement");
                ReplaceCertificate(entryContents, partition, name);
                LogHandler.Trace(logger, CertificateStore, $"Certificate replacement for partition '{partition}' and name '{name}' completed");
            }
            LogHandler.MethodExit(logger, CertificateStore, "ReplaceEntry");
        }

        public void RemoveEntry(string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "RemoveEntry");
            LogHandler.Trace(logger, CertificateStore, $"Processing certificate for partition '{partition}' and name '{name}'");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");
            if (KeyExists(partition, name))
            {
                LogHandler.Trace(logger, CertificateStore, $"Archiving key at '{partition}' and name '{name}'");
                ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_key_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.key");
                LogHandler.Trace(logger, CertificateStore, $"Removing certificate and key at '{partition}' and name '{name}'");

                string keyName = GetKeyName(name, true);
                REST.Delete($"/mgmt/tm/sys/file/ssl-key/~{partition}~{keyName}");
            }
            LogHandler.Trace(logger, CertificateStore, $"Archiving certificate at '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");
            LogHandler.Trace(logger, CertificateStore, $"Removing certificate at '{partition}' and name '{name}'");

            string crtName = GetCrtName(name, true);
            REST.Delete($"/mgmt/tm/sys/file/ssl-cert/~{partition}~{crtName}");
            LogHandler.MethodExit(logger, CertificateStore, "RemoveEntry");
        }

        public bool KeyExists(string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "KeyExists");
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

            LogHandler.MethodExit(logger, CertificateStore, "KeyExists");
            return exists;
        }

        public bool CertificateExists(string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "CertificateExists");
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

            LogHandler.MethodExit(logger, CertificateStore, "CertificateExists");
            return exists;
        }

        private void AddCertificate(byte[] entryContents, string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "AddCertificate");
            LogHandler.Trace(logger, CertificateStore, $"Uploading file to {partition}-{name}");
            REST.UploadFile($"{partition}-{name}", entryContents);

            LogHandler.Trace(logger, CertificateStore, $"Installing certificate to '{name}'");
            REST.PostInstallCryptoCommand(new F5InstallCommand
            {
                command = "install",
                name = $"{name}",
                localfile = $"/var/config/rest/downloads/{partition}-{name}",
                partition = partition
            }, "cert");
            LogHandler.MethodExit(logger, CertificateStore, "AddCertificate");
        }

        private void AddPfx(byte[] entryContents, string partition, string name, string password)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "AddPfx");
            LogHandler.Trace(logger, CertificateStore, $"Uploading PFX to {partition}-{name}.p12");
            REST.UploadFile($"{partition}-{name}.p12", entryContents);

            LogHandler.Trace(logger, CertificateStore, $"Installing PFX to '{name}'");
            REST.PostInstallCryptoCommand(new F5InstallCommand
            {
                command = "install",
                name = $"{name}",
                localfile = $"/var/config/rest/downloads/{partition}-{name}.p12",
                passphrase = password,
                partition = partition
            }, "pkcs12");
            LogHandler.MethodExit(logger, CertificateStore, "AddPfx");
        }

        private void ReplaceCertificate(byte[] entryContents, string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "ReplaceCertificate");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(logger, CertificateStore, $"Archiving the certificate for partition '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");

            LogHandler.Trace(logger, CertificateStore, $"Adding certificate to partition '{partition}' and name '{name}'");
            AddCertificate(entryContents, partition, name);
            LogHandler.MethodExit(logger, CertificateStore, "ReplaceCertificate");
        }

        private void ReplacePfx(byte[] entryContents, string partition, string name, string password)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "ReplacePfx");
            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(logger, CertificateStore, $"Archiving the key and certificate for partition '{partition}' and name '{name}'");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_key_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.key");
            ArchiveFile($"/config/filestore/files_d/{partition}_d/certificate_d/:{partition}:{name}_*", $"{partition}-{name}-{timestamp}.crt");

            LogHandler.Trace(logger, CertificateStore, $"Adding PFX to partition '{partition}' and name '{name}'");
            AddPfx(entryContents, partition, name, password);
            LogHandler.MethodExit(logger, CertificateStore, "ReplacePfx");
        }

        private X509Certificate2Collection GetCertificateEntry(string path)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetCertificateEntry");
            string certificateEntry = string.Empty;
            LogHandler.Trace(logger, CertificateStore, $"Getting certificate entry from: '{path}'");

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
                    LogHandler.Trace(logger, CertificateStore, "Certificate is DER encoded");
                    certificateEntry = crt;
                    break;
                //PEM(no headers)-- > T
                case "T":
                    LogHandler.Trace(logger, CertificateStore, "Certificate is PEM without headers");
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    break;
                //PEM(w / headers)-- > L
                case "L":
                    LogHandler.Trace(logger, CertificateStore, "Certificate is PEM with headers");
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    break;
                default:
                    crtBytes = System.Convert.FromBase64String(crt);
                    certificateEntry = System.Text.ASCIIEncoding.ASCII.GetString(crtBytes);
                    throw new Exception("Unknown certificate format found for device certificate");
            }

            LogHandler.MethodExit(logger, CertificateStore, "GetCertificateEntry");

            return CertificateCollectionConverterFactory.FromPEM(certificateEntry).ToX509Certificate2Collection();
        }

        private void SetItemStatus(CurrentInventoryItem agentInventoryItem)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "SetItemStatus");
            PreviousInventoryItem keyfactorInventoryItem = Inventory
                .SingleOrDefault(i => i.Alias.Equals(agentInventoryItem.Alias, StringComparison.OrdinalIgnoreCase));
            if (keyfactorInventoryItem == null)
            {
                LogHandler.Trace(logger, CertificateStore, "F5 item does not exist in Keyfactor Command and will be tagged as new");
                agentInventoryItem.ItemStatus = OrchestratorInventoryItemStatus.New;
                LogHandler.MethodExit(logger, CertificateStore, "SetItemStatus");
                return;
            }

            LogHandler.Trace(logger, CertificateStore, "Matching alias found, checking entry details");
            if (keyfactorInventoryItem.PrivateKeyEntry != agentInventoryItem.PrivateKeyEntry)
            {
                LogHandler.Trace(logger, CertificateStore, "Private key entry status does not match and will be tagged as modified");
                agentInventoryItem.ItemStatus = OrchestratorInventoryItemStatus.Modified;
                LogHandler.MethodExit(logger, CertificateStore, "SetItemStatus");
                return;
            }

            LogHandler.Trace(logger, CertificateStore, "Private key entry status matches, checking certificates");
            if (keyfactorInventoryItem.Thumbprints.Count() != agentInventoryItem.Certificates.Count())
            {
                LogHandler.Trace(logger, CertificateStore, $"F5 entry certificate count: {agentInventoryItem.Certificates.Count()} does not match Keyfactor Command count: {keyfactorInventoryItem.Thumbprints.Count()} and will be tagged as modified");
                agentInventoryItem.ItemStatus = OrchestratorInventoryItemStatus.Modified;
                LogHandler.MethodExit(logger, CertificateStore, "SetItemStatus");
                return;
            }

            LogHandler.Trace(logger, CertificateStore, "Certificate counts match, checking individual certificates");
            foreach (string pem in agentInventoryItem.Certificates)
            {
                string certificateBase64 = pem;
                certificateBase64 = Regex.Replace(certificateBase64, "\r", "");
                certificateBase64 = Regex.Replace(certificateBase64, "-----BEGIN CERTIFICATE-----\n", "");
                certificateBase64 = Regex.Replace(certificateBase64, "\\n-----END CERTIFICATE-----(\\n|)", "");

                LogHandler.Trace(logger, CertificateStore, "Getting X509 object from F5 certificate pem");
                X509Certificate2 x509 = new X509Certificate2(Convert.FromBase64String(certificateBase64));
                LogHandler.Trace(logger, CertificateStore, $"Looking for CMS thumbprint matching: '{x509.Thumbprint}'");
                if (!keyfactorInventoryItem.Thumbprints.Any(t => t.Equals(x509.Thumbprint, StringComparison.OrdinalIgnoreCase)))
                {
                    LogHandler.Trace(logger, CertificateStore, "Thumbprint not found and will be tagged as modified");
                    agentInventoryItem.ItemStatus = OrchestratorInventoryItemStatus.Modified;
                    LogHandler.MethodExit(logger, CertificateStore, "SetItemStatus");
                    return;
                }
            }

            LogHandler.Trace(logger, CertificateStore, "The inventory item is unchanged");
            agentInventoryItem.ItemStatus = OrchestratorInventoryItemStatus.Unchanged;
            LogHandler.MethodExit(logger, CertificateStore, "SetItemStatus");
        }

        private CurrentInventoryItem GetInventoryItem(string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetInventoryItem");

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
            CurrentInventoryItem inventoryItem = new CurrentInventoryItem
            {
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                Alias = crtName,
                PrivateKeyEntry = privateKeyEntry,
                UseChainLevel = useChainLevel,
                Certificates = certContents.ToArray()
            };
            SetItemStatus(inventoryItem);
            LogHandler.MethodExit(logger, CertificateStore, "GetInventoryItem");
            return inventoryItem;
        }

        private string GetCrtName(string name, bool addExtension)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetCrtName");
            string crtName = name;

            switch (F5Version.ToLowerInvariant())
            {
                case "v12":
                    throw new Exception($"F5 Version 12 is not supported by the REST-based orchestrator. The legacy SOAP-based orchestrator should be used.");
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
            };

            LogHandler.MethodExit(logger, CertificateStore, "GetCrtName");
            return crtName;
        }

        private string GetKeyName(string name, bool addExtension)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetKeyName");
            string keyName = name;

            // No longer checking past version 14 for future-proofing
            switch (F5Version.ToLowerInvariant())
            {
                case "v12":
                    throw new Exception($"F5 Version 12 is not supported by the REST-based orchestrator. The legacy SOAP-based orchestrator should be used.");
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
            };

            LogHandler.MethodExit(logger, CertificateStore, "GetKeyName");
            return keyName;
        }

        // Certificate PFX Shared
        #endregion

        #region Infrastructure

        public bool PrimaryNodeActive()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "PrimaryNodeActive");
            F5NodeDevice device = REST.Get<F5NodeDevice>($"/mgmt/tm/cm/device/{PrimaryNode}?$select=name,failoverState");
            bool nodeActive = device.failoverState.Equals("active", StringComparison.OrdinalIgnoreCase);
            LogHandler.MethodExit(logger, CertificateStore, "PrimaryNodeActive");

            return nodeActive;
        }

        public string GetActiveNode()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetActiveNode");
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
                LogHandler.Warn(logger, CertificateStore, "No active node found, returning an empty device name");
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"'{activeNode}' is currently active and will be considered the primary node");
            }
            LogHandler.MethodExit(logger, CertificateStore, "GetActiveNode");

            return activeNode;
        }

        public List<F5Partition> GetPartitions()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetPartitions");
            F5PartitionList partitions = REST.Get<F5PartitionList>($"/mgmt/tm/auth/partition?$select=name,fullPath");
            LogHandler.MethodExit(logger, CertificateStore, "GetPartitions");

            return partitions.items.ToList<F5Partition>();
        }

        public string GetPartitionFromStorePath()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetPartitionsFromStorePath");
            string[] pathParts = CertificateStore.StorePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 1) { throw new Exception($"The store path '{CertificateStore.StorePath}' does not appear to contain a partition"); }
            LogHandler.MethodExit(logger, CertificateStore, "GetPartitionFromStorePath");
            return pathParts[0];
        }

        // Infrastructure
        #endregion

        #region Web Server

        public List<CurrentInventoryItem> GetWebServerInventory()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetWebServerInventory");
            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();
            X509Certificate2Collection certificateCollection = GetCertificateEntry("/config/httpd/conf/ssl.crt/server.crt");
            List<string> webServerInventory = new List<string>();
            foreach (X509Certificate2 certificate in certificateCollection)
            {
                webServerInventory.Add(Convert.ToBase64String(certificate.RawData));
            }

            LogHandler.Trace(logger, CertificateStore, $"Obtained F5 web server device inventory");
            CurrentInventoryItem inventoryItem = new CurrentInventoryItem
            {
                Alias = "WebServer",
                PrivateKeyEntry = true,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = webServerInventory.ToArray()
            };
            SetItemStatus(inventoryItem);
            inventory.Add(inventoryItem);
            LogHandler.MethodExit(logger, CertificateStore, "GetWebServerInventory");

            return inventory;
        }

        public void ReplaceWebServerCrt(string b64Certificate)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "ReplaceWebServerCrt");
            LogHandler.Trace(logger, CertificateStore, "Processing web server certificate");
            byte[] devicePfx = Convert.FromBase64String(b64Certificate);
            string password = PFXPassword;
            CertificateCollectionConverter converter = CertificateCollectionConverterFactory.FromDER(devicePfx, password);
            string pfxPem = converter.ToPEM(password);
            List<X509Certificate2> clist = converter.ToX509Certificate2List(password);

            StringBuilder certPemBuilder = new StringBuilder();



            //////// THE LIST MUST BE REVERSED SO THAT THE END-ENTITY CERT IS FIRST /////////
            //////// CAN IT BE ASSUMED THE LAST ENTRY IS END-ENTIT? /////////////////////////
            clist.Reverse();
            /////////////////////////////////////////////////////////////////////////////////

            LogHandler.Trace(logger, CertificateStore, "Building certificate PEM");
            foreach (X509Certificate2 cert in clist)
            {
                certPemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                certPemBuilder.AppendLine(
                    Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
                certPemBuilder.AppendLine("-----END CERTIFICATE-----");
            }

            LogHandler.Trace(logger, CertificateStore, "Building key PEM");
            byte[] pkBytes = Keyfactor.PKI.PrivateKeys.PrivateKeyConverterFactory.FromPKCS12(devicePfx, password).ToPkcs8BlobUnencrypted();
            StringBuilder keyPemBuilder = new StringBuilder();
            keyPemBuilder.AppendLine("-----BEGIN PRIVATE KEY-----");
            keyPemBuilder.AppendLine(
                Convert.ToBase64String(pkBytes, Base64FormattingOptions.InsertLineBreaks));
            keyPemBuilder.AppendLine("-----END PRIVATE KEY-----");

            string timestamp = DateTime.Now.ToString("MM-dd-yy:H:mm:ss");

            LogHandler.Trace(logger, CertificateStore, "Uploading web server certificate");
            byte[] certbytes = Encoding.ASCII.GetBytes(certPemBuilder.ToString());
            RESTHandler rest = REST;
            rest.UploadFile($"kyfcert-{timestamp}.crt", certbytes);

            LogHandler.Trace(logger, CertificateStore, "Uploading the web server key");
            byte[] keybytes = Encoding.ASCII.GetBytes(keyPemBuilder.ToString());
            rest.UploadFile($"kyfcert-{timestamp}.key", keybytes);

            F5Transaction transaction = BeginTransaction();

            LogHandler.Trace(logger, CertificateStore, "Archiving the web server certificate and key");
            ArchiveFile("/config/httpd/conf/ssl.key/server.key", $"server-{timestamp}.key", transaction.transid);
            ArchiveFile("/config/httpd/conf/ssl.crt/server.crt", $"server-{timestamp}.crt", transaction.transid);

            LogHandler.Trace(logger, CertificateStore, "Replacing the web server certificate and key");
            CopyFile($"/var/config/rest/downloads/kyfcert-{timestamp}.key", "/config/httpd/conf/ssl.key/server.key", transaction.transid);
            CopyFile($"/var/config/rest/downloads/kyfcert-{timestamp}.crt", "/config/httpd/conf/ssl.crt/server.crt", transaction.transid);

            CommitTransaction(transaction);
            LogHandler.MethodExit(logger, CertificateStore, "ReplaceWebServerCrt");
        }

        // WebServer
        #endregion

        #region SSL Profiles

        public List<CurrentInventoryItem> GetSSLProfiles(int pageSize)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetSSLProfiles");
            string partition = CertificateStore.StorePath;
            string query = $"/mgmt/tm/sys/file/ssl-cert?$filter=partition+eq+{partition}&$select=name,isBundle&$top={pageSize}&$skip=0";
            F5PagedSSLProfiles pagedProfiles = REST.Get<F5PagedSSLProfiles>(query);
            List<F5SSLProfile> profiles = new List<F5SSLProfile>();
            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            if (pagedProfiles.totalItems == 0 || pagedProfiles.items?.Length == 0)
            {
                LogHandler.Trace(logger, CertificateStore, $"No SSL profiles found in partition '{partition}'");
                LogHandler.MethodExit(logger, CertificateStore, "GetSSLProfiles");
                return inventory;
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"Compiling {pagedProfiles.totalPages} pages containing {pagedProfiles.totalItems} total inventory entries");
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
                        LogHandler.Trace(logger, CertificateStore, $"Skipping '{profiles[i].name}' because it is managed by F5");
                        continue;
                    }
                    inventory.Add(GetInventoryItem(partition, profiles[i].name));
                }
                catch (Exception ex)
                {
                    LogHandler.Error(logger, CertificateStore, ExceptionHandler.FlattenExceptionMessages(ex, $"Unable to process inventory item {profiles[i].name}."));
                }
            }

            LogHandler.MethodExit(logger, CertificateStore, "GetSSLProfiles");
            return inventory;
        }

        // SSL Profiles
        #endregion

        #region Bundles

        public List<F5CABundle> GetCABundles(string partition, int pageSize)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetCABundles");
            string query = $"/mgmt/tm/sys/crypto/ca-bundle-manager?$filter=partition+eq+{partition}&$select=name,fullPath&$top={pageSize}&$skip=0";
            F5PagedCABundles pagedBundles = REST.Get<F5PagedCABundles>(query);
            List<F5CABundle> bundles = new List<F5CABundle>();

            if (pagedBundles.totalItems == 0 || pagedBundles.items?.Length == 0)
            {
                LogHandler.Trace(logger, CertificateStore, $"No CA bundles found in partition '{partition}'");
                LogHandler.MethodExit(logger, CertificateStore, "GetCABundles");
                return bundles;
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"Compiling {pagedBundles.totalPages} pages containing {pagedBundles.totalItems} total CA bundles");
            }

            for (int i = pagedBundles.pageIndex; i <= pagedBundles.totalPages; i++)
            {
                foreach (F5CABundle bundle in pagedBundles.items)
                {
                    // Remove 'ca-bundle'
                    if (bundle.name.Equals("ca-bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        LogHandler.Trace(logger, CertificateStore, $"Skipping '{bundle.name}' because it is managed by F5");
                        continue;
                    }
                    bundles.Add(bundle);
                }

                if (string.IsNullOrEmpty(pagedBundles.nextLink)) { break; }

                query = pagedBundles.nextLink.Replace("https://localhost", "");
                pagedBundles = REST.Get<F5PagedCABundles>(query);
            }

            LogHandler.MethodExit(logger, CertificateStore, "GetCABundles");
            return bundles;
        }

        public List<CurrentInventoryItem> GetCABundleInventory()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetCABundleInventory");
            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();
            string[] includeBundle = GetCABundleIncludes();

            LogHandler.Trace(logger, CertificateStore, $"Compiling {includeBundle.Length} bundled certificates");
            for (int i = 0; i < includeBundle.Length; i++)
            {
                try
                {
                    LogHandler.Trace(logger, CertificateStore, $"Processing certificate '{includeBundle[i]}'");
                    string[] crtPathParts = includeBundle[i].Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    if (crtPathParts.Length != 2) { throw new Exception($"Bundled certificate path: '{includeBundle[i]}' is invalid.  Expecting '/<partition>/<certificate>'."); }
                    string partition = crtPathParts[0];
                    string crtName = crtPathParts[1];

                    LogHandler.Trace(logger, CertificateStore, $"Adding inventory item for partition '{partition}' and name '{crtName}'");
                    inventory.Add(GetInventoryItem(partition, crtName));
                }
                catch (Exception ex)
                {
                    LogHandler.Error(logger, CertificateStore, ExceptionHandler.FlattenExceptionMessages(ex, $"Unable to process inventory item {includeBundle[i]}."));
                }
            }

            LogHandler.MethodExit(logger, CertificateStore, "GetCABundleInventory");
            return inventory;
        }

        public bool EntryExistsInBundle(string alias)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "EntryExistsInBundle");
            bool exists = false;

            List<string> bundleIncludes = new List<string>(GetCABundleIncludes());
            string partition = GetPartitionFromStorePath();

            string crtName = GetCrtName(alias, true);
            exists = bundleIncludes.Any<string>(i => i.Equals($"/{partition}/{crtName}", StringComparison.OrdinalIgnoreCase));

            LogHandler.MethodExit(logger, CertificateStore, "EntryExistsInBundle");
            return exists;
        }

        private string[] GetCABundleIncludes()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "GetCABundleIncludes");
            string bundlePath = CertificateStore.StorePath;
            string[] bundlePathParts = bundlePath.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (bundlePathParts.Length != 2) { throw new Exception($"CA bundle path: '{bundlePath}' is invalid.  Expecting '/<partition>/<bundle>'."); }
            string partition = bundlePathParts[0];
            string bundleName = bundlePathParts[1];
            string query = $"/mgmt/tm/sys/crypto/ca-bundle-manager/~{partition}~{bundleName}?$select=includeBundle";
            F5CABundle bundle = REST.Get<F5CABundle>(query);
            string[] includeBundle;
            if (bundle.includeBundle == null)
            {
                LogHandler.Trace(logger, CertificateStore, $"Found 0 included bundles");
                includeBundle = new List<string>().ToArray();
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"Found {bundle.includeBundle.Count()} included bundles");
                includeBundle = bundle.includeBundle;
            }

            LogHandler.MethodExit(logger, CertificateStore, "GetCABundleIncludes");
            return includeBundle;
        }

        public void AddBundleEntry(string bundle, string partition, string name, string b64Certificate, string alias, bool overwrite)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "AddBundleEntry");

            // Add the entry to inventory
            if (!CertificateExists(partition, name))
            {
                LogHandler.Debug(logger, CertificateStore, $"Add entry '{name}' in '{CertificateStore.StorePath}'");
                AddEntry(partition, name, b64Certificate);
            }
            else
            {
                if (!overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandler.Debug(logger, CertificateStore, $"Replace entry '{name}' in '{CertificateStore.StorePath}'");
                ReplaceEntry(partition, name, b64Certificate);
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
            LogHandler.MethodExit(logger, CertificateStore, "AddBundleEntry");
        }

        public void RemoveBundleEntry(string bundle, string partition, string name)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "RemoveBundleEntry");

            string crtName = GetCrtName(name, true);
            string crtEntry = $"/{partition}/{crtName}";

            LogHandler.Trace(logger, CertificateStore, $"Preparing to remove bundle entry '{crtEntry}'");
            List<string> bundleIncludes = new List<string>(GetCABundleIncludes());
            if (bundleIncludes.Contains(crtEntry))
            {
                LogHandler.Trace(logger, CertificateStore, $"The current bundle contains entry '{crtEntry}' - adding removal operation to transaction");
                bundleIncludes.Remove(crtEntry);
                F5BundleInclude bundleInclude = new F5BundleInclude { includeBundle = bundleIncludes.ToArray() };
                REST.Patch<F5BundleInclude>($"/mgmt/tm/sys/crypto/ca-bundle-manager/{bundle.Replace('/', '~')}", bundleInclude);
            }
            else
            {
                LogHandler.Trace(logger, CertificateStore, $"The current bundle does not contain entry '{crtEntry}'");
            }
            LogHandler.MethodExit(logger, CertificateStore, "RemoveBundleEntry");
        }

        // Bundles
        #endregion

        #region File Handling

        private void ArchiveFile(string sourcePath, string targetFilename, string transactionId = "")
        {
            LogHandler.MethodEntry(logger, CertificateStore, "ArchiveFile");

            // Make the 'keyfactor' directory if it doesn't exist
            string mkdirResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'mkdir -p /var/config/rest/downloads/keyfactor'"
            }, transactionId);

            CopyFile(sourcePath, $"/var/config/rest/downloads/keyfactor/{targetFilename}", transactionId);
            LogHandler.MethodExit(logger, CertificateStore, "ArchiveFile");
        }

        private void CopyFile(string source, string target, string transactionId = "")
        {
            LogHandler.MethodEntry(logger, CertificateStore, "CopyFile");
            string copyResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'cp {source} {target}'"
            }, transactionId);
            LogHandler.MethodExit(logger, CertificateStore, "CopyFile");
        }

        private void MoveFile(string source, string target, string transactionId = "")
        {
            LogHandler.MethodEntry(logger, CertificateStore, "MoveFile");
            string moveResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'mv {source} {target}'"
            }, transactionId);
            LogHandler.MethodExit(logger, CertificateStore, "MoveFile");
        }

        private void RemoveFile(string source, string transactionId = "")
        {
            LogHandler.MethodEntry(logger, CertificateStore, "RemoveFile");
            string removeResult = REST.PostBASHCommand(new F5BashCommand
            {
                command = "run",
                utilCmdArgs = $"-c 'rm {source}'"
            }, transactionId);
            LogHandler.MethodExit(logger, CertificateStore, "RemoveFile");
        }

        // File Handling
        #endregion

        #region Transactions

        private F5Transaction BeginTransaction()
        {
            LogHandler.MethodEntry(logger, CertificateStore, "BeingTransaction");
            F5Transaction transaction = REST.Post<F5Transaction>("/mgmt/tm/transaction", "{}");
            LogHandler.Trace(logger, CertificateStore, $"Initiated transaction '{transaction.transid}'");
            LogHandler.MethodExit(logger, CertificateStore, "BeingTransaction");
            return transaction;
        }

        private void CommitTransaction(F5Transaction transaction)
        {
            LogHandler.MethodEntry(logger, CertificateStore, "CommitTransaction");
            LogHandler.Trace(logger, CertificateStore, $"Committing transaction '{transaction.transid};");
            REST.Patch<F5CommitTransaction>($"/mgmt/tm/transaction/{transaction.transid}", new F5CommitTransaction { state = "VALIDATING", validateOnly = false });
            LogHandler.MethodExit(logger, CertificateStore, "CommitTransaction");
        }

        // Transactions
        #endregion

        // Methods
        #endregion
    }
}
