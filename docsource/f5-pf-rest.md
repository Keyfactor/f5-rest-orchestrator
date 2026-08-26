## Overview

The F5-PF-REST certificate store type manages the certificate bound to a single F5 Big IP SSL Profile (client or server only).  Adding a certificate to this store will both add the certificate to the F5 Big IP device as well as bind it to
the SSL Profile identified in the Keyfactor Command certificate store configuration.  Inventory, Create, and Add (both new certificates and replace/renew) capabilities are supported, but Removal is not, as that would leave an SSL Profile unbound.  
The certificate store configuration maps to the SSL Profile being managed by way of the Client Machine (IP address or DNS of the F5 instance being managed) and the Store Path which needs to have the format of "Partition/SSLProfileType/SSLProfileName" where 
SSLProfileType **must** be either "Client" or "Server".


