## Overview

The F5-PF-REST certificate store type manages the certificate bound to a single F5 Big IP SSL Profile (client or server only).  Adding a certificate to this store will both add the certificate to the F5 Big IP device as well as bind it to
the SSL Profile identified in the Keyfactor Command certificate store configuration.  Inventory, Create, and Add (both new certificates and replace/renew) capabilities are supported, but Removal is not, as that would leave an SSL Profile unbound.  
The certificate store configuration maps to the SSL Profile being managed by way of the Client Machine (IP address or DNS of the F5 instance being managed) and the Store Path which needs to have the format of "Partition/SSLProfileType/SSLProfileName" where 
SSLProfileType **must** be either "Client" or "Server".



## Reenrollment (On Device Key Generation)

Reenrollment (ODKG) is supported for this store type.  The private key and CSR are generated on the F5 device, the CSR is submitted to Keyfactor Command for signing, and the returned certificate is installed on the F5 to pair with the key.  Key types of RSA and EC/ECC (curves P-256, P-384 and P-521) are supported.  If the alias already exists, "Overwrite" must be selected or the job will fail; when it is, the new key and certificate are created under the alias with an underscore and a timestamp (yyyyMMddHHmmss) appended, every client and server SSL profile bound to the existing alias (including the one this store manages) is re-bound to the new certificate, and the existing certificate is left on the F5.  A new alias is bound to the SSL Profile identified in the certificate store configuration.  The CSR is removed once the certificate is installed.
