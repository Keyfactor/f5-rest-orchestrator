v1.9.1
- Add ability to inventory PEM files with Bag Attributes at the beginning of each certificate

v1.9.0
- Added new SSLProfiles READ ONLY entry parameter that will contain a comma delimited list of SSL Profiles a certificate is bound to on the F5 device. 

v1.8.1
- Documentation changes including highlighting lack of HA support as well as a correction to the proper StorePath value for F5-CA-REST stores.

v1.8.0
- Add new custom field - Remove Chain on Add - to allow the removal of the certificate chain before adding/replacing a certificate on the F5 device.  Default = false.
- Apply store password when replacing a certificate as well as adding (extension to change made in v1.6.0)
- Added additional error logging

v1.7.0 
- Deprecate F5 Version Custom Field for all store types.
- Make Store Password a "PAM eligible" field on the orchestrator
- Remove session token at end of each job
- Convert documentation to use Doctool
- Create separate .net6 and .net8 builds on release

v1.6.0
- Add Store Password (optional) to allow for setting key type to "Password" when adding/replacing a certificate.  This will encrypt the private key deployed on the F5 device with the password set as the Store Password.

v1.5.0
- Add new optional custom paramter - UseTokenAuth - to make token auth vs basic auth (default) a selectable option

v1.4.5
- Bug Fix: For F5-WS-REST store type, make sure certificate chain is ordered properly when installing to F5 - EE Cert => Issuing CA Cert => One-to-many Intermediate CA Certs => Root CA Cert.
- Bug Fix: Allow PEM formats with # comments at top of file during inventory

v1.4.3
- Bug Fix: IgnoreSSLWarning was not recognized when set to true
- Modified login API call for token auth to fix issue some users were experiencing

v1.4
- Modified authentication for API calls from Basic to Token Auth.  Initial login uses id/password to retrieve temporary access token, so the same id/password credentials are still required for the certificate store, but all subsequent API calls will use the token retrieved on initial login.
- Added PAM Support
- Fix bug where Private Key Entry is always False

v1.3
- Fix to match F5 hotfix modification to handle certificates/keys with dissimilar names within F5.  Please go to the [Troubleshooting Guide](Troubleshooting.md#certificate-renewal-error) for details.

v1.2
- Add new IgnoreSSLWarning optional certificate store type parameter which will determine whether SSL warnings for F% API requests should be ignored.
- Minor bug fix to handle embedded whitespace in PEM encoded certificates returned from F5 inventory

v1.1
- Modify PrimaryNodeOnlineRequired certificate store type parameter to be optional for backwards compatibility

v1.0  
- Initial Version

