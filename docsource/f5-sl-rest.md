## Overview

The F5-SL-REST certificate store type manages F5 Big IP TLS certificates.  Renewal of bound certificates is supported, but adding new bindings for new or replacement certificates is not. This store type **does** track the SSL Profiles a certificate is bound to by way of the SSL Profiles entry parameter.  Inventory jobs will return a comma delimited list of SSL Profile names that each certificate is bound to and return this in the SSLProfiles entry parameter.


## Reenrollment (On Device Key Generation)

Reenrollment (ODKG) is supported for this store type.  The private key and CSR are generated on the F5 device, the CSR is submitted to Keyfactor Command for signing, and the returned certificate is installed on the F5 to pair with the key.  Key types of RSA and EC/ECC (curves P-256, P-384 and P-521) are supported.  If the alias already exists, "Overwrite" must be selected or the job will fail; when it is, the new key and certificate are created under the alias with an underscore and a timestamp (yyyyMMddHHmmss) appended, every client and server SSL profile bound to the existing alias is re-bound to the new certificate, and the existing certificate is left on the F5.  The CSR is removed once the certificate is installed.  As with Management Add jobs, any SSL Profiles supplied in the SSLProfiles entry parameter are bound only when the alias is new, and the Sync to Device Group setting is honored.
