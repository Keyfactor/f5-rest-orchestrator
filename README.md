# F5

The F5 Orchestrator allows for the remote management of F5 Stores. Discovery, Inventory, and Management functions are supported.

#### Integration status: Production - Ready for use in production environments.

## About the Keyfactor Universal Orchestrator Capability

This repository contains a Universal Orchestrator Capability which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Capabilities, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Capability, see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Capability plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.

---

## Use Cases

The F5 Orchestrator supports three different types of certificates stores with the capabilities for each below:

- CA Bundles
  - Discovery
  - Inventory
  - Management (Add)
- Web Server Device Certificates
  - Inventory
  - Management (Add, but replacement/renewal of existing certificate only) 
- SSL Certificates
  - Discovery
  - Inventory
  - Management (Add)




## Versioning

The version number of a the F5 Orchestrator can be verified by right clicking on the F5Orchestrator.dll file, selecting Properties, and then clicking on the Details tab.

## Keyfactor Version Supported

The F5 Orchestrator has been tested using Keyfactor Command version 9.4 and the Keyfactor Universal Orchestrator version 9.5.  However, this should be compatible with any Keyfactor versions 9.x and above.

## F5 Orchestrator Installation

1. In the Keyfactor Orchestrator installation folder (by convention usually C:\Program Files\Keyfactor\Keyfactor Orchestrator), find the "extensions" folder. Underneath that, create a new folder for each F5 Orchestrator certificate store type you wish to manage.  Suggested names for each are below:
    - F5-CA-REST (for management of CA Bundles)
    - F5-WS-REST (for management of Web Server Certificates)
    - F5-SL-REST (for management of SSL Certificates)
3. Download the latest version of the F5 Orchestrator from [GitHub](https://github.com/Keyfactor/f5-rest-orchestrator).
4. Copy the contents of the download installation zip file to each of the folders created in step 1.
5. (Optional) If you decided to name any of the folders in step 1 to something different than the suggested names, you will need to edit the manifest.json file in each of the folders.  For each section change {folder name} in "CertStores.{folder name}.*Capability*" to the folder name you used for each store type.


## F5 Orchestrator Configuration

**1. In Keyfactor Command, if any of the aforementioned certificate store types do not already exist, create a new certificate store type for each of the 3 that you wish to manage by navigating to Settings (the "gear" icon in the top right) => Certificate Store Types.**

**<u>CA Bundles:</u>**

![](images/image1.png)
![](images/image2.png)



**<u>Web Server Certificates</u>**

![](images/image9.png)
![](images/image10.png)



**<u>SSL Certificates</u>**

![](images/image11.png)
![](images/image12.png)

- **Name** – Required. The display name of the new Certificate Store Type
- **Short Name** – Required. This value ***must match*** the folder name for this store type under the "extensions" folder in the install path.
- **Custom Capability** - Leave unchecked
- **Supported Job Types** – Select Inventory and Add for all 3 types, and Discovery for CA Bundles and SSL Certificates.
- **General Settings** - Select Needs Server.  Leave Uses PowerShell unchecked.  Select Blueprint Allowed if you plan to use blueprinting.
- **Password Settings** - Leave both options unchecked
- **All selections on Advanced tab** - Set the values on this tab ***exactly*** as they are shown in the above screen prints for each applicable store type.



The Custom Fields tab contains 5 custom store parameters that must be set up.  The set up is consistent across store types, and should look as follows:

![](images/image3.png)<br>  
![](images/image4.png)<br>  
![](images/image5.png)<br>  
![](images/image6.png)<br>  
![](images/image7.png)<br>  
![](images/image8.png)<br>  



**2a. Create a F5 Certificate Store within Keyfactor Command**
![](images/image13.png)

If you choose to manually create a F5 store In Keyfactor Command rather than running a Discovery job (Step 2b) to automatically find the store, you can navigate to Certificate Locations =\> Certificate Stores within Keyfactor Command to add the store. Below are the values that should be entered.![](Images/Image13.png)

- **Category** – Required.  One of the 3 F5 store types - F5 Web Server REST, F5 CA Bundles REST, or F5 SSL Profiles REST (your configured names may be different based on what you entered when creating the certificate store types in Step 1).

- **Container** – Optional.  Select a container if utilized.

- **Client Machine & Credentials** – Required.  The server name or IP Address and login credentials for the F5 device.  The credentials for server login can be any of:
  
  - UserId/Password
  
  - PAM provider information to pass the UserId/Password or UserId/SSH private key credentials
  
  
  When entering the credentials, UseSSL ***must*** be selected.
  
- **Store Path** – Required.  Enter the name of the partition on the F5 device you wish to manage.  This value is case sensitive, so if the partition name is "Common", it must be entered as "Common" and not "common".

- **Version of F5** - Required.  Select v13, v14, or v15 to match the version for the F5 device being managed

- **Primary Node Online Required** – Optional.  Select this if you wish to stop the orchestrator from adding, replacing or renewing certificates on nodes that are inactive.  If this is not selected, adding, replacing and renewing certificates on inactive nodes will be allowed.  If you choose not to add this custom field, the default value of False will be assumed.

- **Primary Node** - Only required (and shown) if Primary Node Online Required is added and selected.  Enter the fully qualified domain name of the F5 device that acts as the primary node in a highly available F5 implementation.  If you're using a single F5 device, this will typically be the same value you entered in the Client Machine field.

- **Primary Node Check Retry Maximum** - Only required (and shown) if Primary Node Online Required is added and selected.  Enter the number of times a Management-Add job will attempt to add/replace/renew a certificate if the node is inactive before failing.

- **Primary Node Check Retry Wait Seconds** - Only required (and shown) if Primary Node Online Required is added and selected.  Enter the number of seconds to wait between attempts to add/replace/renew a certificate if the node is inactive.

- **Orchestrator** – Required.  Select the orchestrator you wish to use to manage this store

- **Inventory Schedule** – Set a schedule for running Inventory jobs or none, if you choose not to schedule Inventory at this time.

**2b. (Optional) Schedule a F5 Discovery Job**

Rather than manually creating F5 certificate stores, you can schedule a Discovery job to search find them (CA Bundle and SSL Certificate store types only).

First, in Keyfactor Command navigate to Certificate Locations =\> Certificate Stores. Select the Discover tab and then the Schedule button. Complete the dialog and click Done to schedule.
![](images/image14.png)

- **Category** – Required. The F5 store type you wish to find stores for.

- **Orchestrator** – Select the orchestrator you wish to use to manage this store

- **Client Machine & Credentials** – Required.  The server name or IP Address and login credentials for the F5 device.  The credentials for server login can be any of:

  - UserId/Password
  - PAM provider information to pass the UserId/Password or UserId/SSH private key credentials
  
  When entering the credentials, UseSSL ***must*** be selected.
  
- **When** – Required. The date and time when you would like this to execute.

- **Directories to search** – Required but not used. This field is not used in the search to Discover certificate stores, but ***is*** a required field in this dialog, so just enter any value.  It will not be used.

- **Directories to ignore/Extensions/File name patterns to match/Follow SymLinks/Include PKCS12 Files** – Not used.  Leave blank.

Once the Discovery job has completed, a list of F5 certificate store locations should show in the Certificate Stores Discovery tab in Keyfactor Command. Right click on a store and select Approve to bring up a dialog that will ask for the remaining necessary certificate store parameters described in Step 2a.  Complete those and click Save, and the Certificate Store should now show up in the list of stores in the Certificate Stores tab.
