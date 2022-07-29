v1.3
- Fix to match F5 hotfix modification to handle certificates/keys with dissimilar names within F5.  Please go to the [Troubleshooting Guide](Troubleshooting.md#certificate-renewal-error) for details.

v1.2
- Add new IgnoreSSLWarning optional certificate store type parameter which will determine whether SSL warnings for F% API requests should be ignored.
- Minor bug fix to handle embedded whitespace in PEM encoded certificates returned from F5 inventory

v1.1
- Modify PrimaryNodeOnlineRequired certificate store type parameter to be optional for backwards compatibility

v1.0 
- Initial Version
