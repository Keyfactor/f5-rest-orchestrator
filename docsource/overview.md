## Overview
The F5 Universal Orchestrator extension is designed to facilitate the management of cryptographic certificates within F5 devices directly from Keyfactor Command. F5 devices often use certificates for a variety of purposes, including SSL/TLS termination, ensuring secure communication between different components, and building a chain of trust.

### Certificate Store Types
The extension supports three different types of certificate stores, each serving distinct purposes and capabilities:

1. **CA Bundles**
    * These stores contain certificate authority (CA) bundles, which are used to validate certificate chains. The supported job types for CA Bundles are Discovery, Inventory, and Management (Add and Remove). It’s important to note that certificates in CA Bundles are presumed not to have private keys.

2. **Web Server Device Certificates**
    * These stores are specifically for certificates installed on F5 used by web servers. The job types supported are Inventory and Management (Add). The Management Add action here only supports the replacement or renewal of existing certificates, and these stores are assumed to have private keys. 

3. **SSL Certificates**
    * These stores handle SSL certificates, which are primarily used for SSL/TLS termination to ensure secure communications. Supported job types include Discovery, Inventory, and Management (Add and Remove). Similar to Web Server Certificates, SSL Certificates are also assumed to have private keys.

Unlike Web Server and SSL certificate stores, CA Bundle stores do not have private keys associated with their certificates. This distinction is crucial during inventory tasks, as it informs how the extension treats and manages these certificates.

