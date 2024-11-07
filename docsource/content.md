## Overview

The F5 Orchestrator supports three different types of certificates stores with the capabilities for each below:

- CA Bundles
  - Discovery
  - Inventory*
  - Management (Add and Remove)
- Web Server Device Certificates
  - Inventory*
  - Management (Add, but replacement/renewal of existing certificate only) 
- SSL Certificates
  - Discovery
  - Inventory*
  - Management (Add and Remove)  

*Special note on private keys: One of the pieces of information that Keyfactor collects during an Inventory job is whether or not the certificate stored in F5 has a private key.  The private key is NEVER actually retrieved by Keyfactor, but Keyfactor does track whether one exists.  F5 does not provide an API to determine this, so by convention, all CA Bundle certificates are deemed to not have private keys, while Web Server and SSL certificates are deemed to have them.  Any Management jobs adding (new or renewal) a certificate will renew without the private key for CA Bundle stores and with the private key for Web Server or SSL stores.


## Requirements

An administrator account must be set up in F5 to be used with this orchestrator extension.  This F5 user id is what must be used as credentials when setting up a Keyfactor Command certificate store pointing to the F5 device intending to be managed.


## Discovery

For SSL Certificate (F5-SL-REST) and CA Bundle (F5-CA-REST) store types, discovery jobs can be scheduled to find F5 partitions that can be configured as Keyfactor Command certificate stores.

First, in Keyfactor Command navigate to Certificate Locations =\> Certificate Stores. Select the Discover tab and then the Schedule button. Complete the dialog and click Done to schedule.
![](images/image14.png)

- **Category** - Required. The F5 store type you wish to find stores for.

- **Orchestrator** - Select the orchestrator you wish to use to manage this store

- **Client Machine & Credentials** - Required.  The server name or IP Address and login credentials for the F5 device.  The credentials for server login can be any of:

  - UserId/Password
  - PAM provider information to pass the UserId/Password or UserId/SSH private key credentials
  
  When entering the credentials, UseSSL ***must*** be selected.
  
- **When** - Required. The date and time when you would like this to execute.

- **Directories to search** - Required but not used. This field is not used in the search to Discover certificate stores, but ***is*** a required field in this dialog, so just enter any value.  It will not be used.

- **Directories to ignore/Extensions/File name patterns to match/Follow SymLinks/Include PKCS12 Files** - Not used.  Leave blank.

Once the Discovery job has completed, a list of F5 certificate store locations should show in the Certificate Stores Discovery tab in Keyfactor Command. Right click on a store and select Approve to bring up a dialog that will ask for the remaining necessary certificate store parameters described in Step 2a.  Complete those and click Save, and the Certificate Store should now show up in the list of stores in the Certificate Stores tab.
