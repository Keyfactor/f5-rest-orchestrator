## Overview

The F5-SL-REST certificate store type manages F5 Big IP TLS certificates.  Renewal of bound certificates is supported, but adding new bindings for new or replacement certificates is not. This store type **does** track the SSL Profiles a certificate is bound to by way of the SSL Profiles entry parameter.  Inventory jobs will return a comma delimited list of SSL Profile names that each certificate is bound to and return this in the SSLProfiles entry parameter.


## Reenrollment (On Device Key Generation)

Reenrollment (ODKG) is supported for this store type.  The private key and CSR are generated on the F5 device using a temporary name, the CSR is submitted to Keyfactor Command for signing, and the returned certificate is combined with the key and installed on the F5 under the alias supplied for the job.  The temporary key and CSR are then removed from the F5.  Key types of RSA and EC/ECC are supported.  If the alias already exists, "Overwrite" must be selected or the job will fail.  As with Management Add jobs, any SSL Profiles supplied in the SSLProfiles entry parameter are bound only when the alias is new, and the Sync to Device Group setting is honored.
