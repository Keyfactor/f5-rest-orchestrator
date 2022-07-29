# Troubleshooting Guide

## Certificate Renewal Error

While attempting to renew an existing F5 certificate, you receive the following error message back in the Keyfactor Job Console: <span style="color:red">Unable to complete the management operation. 01070317:3: profile /Common/someName's key(/Common/someName.key) and certificate(/Common/someName.crt) do not match.</span>

This is a very specific condition that can occur with F5 versions 14 and higher after upgrading from version 13.  After upgrading, the certificates and associated private keys can be left in a state where they have different names (for example, someName.crt and someName.key, respectively).  The F5 API that the Keyfactor Orchestrator Extension uses to renew a certificate does not allow for 2 names to be passed, leading to the above error with the renewed certificate not being properly bound to the F5 SSL endpoint.

The new certificate will actually be added to F5, but the old certificate will still be the one bound to the SSL endpoint.

Version 1.3 of the Keyfactor F5 orchestrator extension fixes this issue, but it does so in concert with an F5 hotfix that must be applied first.  Eventually, this hotfix will become part of the F5 core installation, but there is no word yet on when that will occur.  Below is information regarding what should be done to acquire this hotfix as well as a couple of links that describe F5 hotfixes and how to apply them.

<b><u>Related F5 Hotfix Information</u></b>

Open a ticket with F5 global support services requesting an EHF that includes a fix for ID1124209 until the fix has been rolled into release.  Eventually this change will be included into a public release for each release train.

The only difference between the EHF and release at this point for this fix will be including additional documentation for online help (CLI and potentially GUI).  This was intentionally omitted from the EHF to meet deadline requirements.  Once rolled into release, the release notes will contain a description of ID1124209 and a known-issue knowledge base article will be created which can be used for reference.

<b><u>Additional F5 links:</u></b>

https://support.f5.com/csp/article/K55025573

https://support.f5.com/csp/article/K13123
