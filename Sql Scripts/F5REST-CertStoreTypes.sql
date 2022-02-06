DECLARE @f5ws INT, @f5ssl INT, @f5ca INT

INSERT INTO [cms_agents].[CertStoreTypes]
           ([Name]
           ,[ShortName]
           ,[LocalStore]
           ,[ServerRegistration]
           ,[ImportType]
           ,[InventoryJobType]
           ,[ManagementJobType]
           ,[AddSupported]
           ,[RemoveSupported]
           ,[CreateSupported]
           ,[DiscoveryJobType]
           ,[EnrollmentJobType]
           ,[InventoryEndpoint]
           ,[EntryPasswordSupported]
           ,[StorePasswordRequired]
           ,[PrivateKeyAllowed]
           ,[StorePathType]
           ,[CustomAliasAllowed]
           ,[JobProperties]
           ,[PowerShell]
           ,[PasswordStyle]
           ,[BlueprintAllowed])
     VALUES
           ('F5 Web Server REST',	'F5-WS-REST',	0,	3,	102,	'7639C64D-BBF1-402A-8838-1A8EE5E06855',	'1802DF3C-322B-4E4C-AD59-6E84DA1AA88E',	1,	0,	0,	NULL,									NULL,	'/AnyInventory/Update',	0,	0,	2,	'WebServer',	0,	'[""]',	0,	0,	0);
SET @f5ws = @@IDENTITY;

INSERT INTO [cms_agents].[CertStoreTypes]
           ([Name]
           ,[ShortName]
           ,[LocalStore]
           ,[ServerRegistration]
           ,[ImportType]
           ,[InventoryJobType]
           ,[ManagementJobType]
           ,[AddSupported]
           ,[RemoveSupported]
           ,[CreateSupported]
           ,[DiscoveryJobType]
           ,[EnrollmentJobType]
           ,[InventoryEndpoint]
           ,[EntryPasswordSupported]
           ,[StorePasswordRequired]
           ,[PrivateKeyAllowed]
           ,[StorePathType]
           ,[CustomAliasAllowed]
           ,[JobProperties]
           ,[PowerShell]
           ,[PasswordStyle]
           ,[BlueprintAllowed])
     VALUES
		   ('F5 SSL Profiles REST',	'F5-SL-REST',	0,	4,	103,	'B98621F3-D779-40D3-8F09-EECF32D68183',	'433D98CA-E570-4F7A-8F32-4D31DC19002E',	1,	1,	0,	'0D893E43-FBE4-4E32-8067-6999E70C6864',	NULL,	'/AnyInventory/Update',	0,	0,	1,	NULL,	2,	'[""]',	0,	0,	1);
SET @f5ssl = @@IDENTITY;

INSERT INTO [cms_agents].[CertStoreTypes]
           ([Name]
           ,[ShortName]
           ,[LocalStore]
           ,[ServerRegistration]
           ,[ImportType]
           ,[InventoryJobType]
           ,[ManagementJobType]
           ,[AddSupported]
           ,[RemoveSupported]
           ,[CreateSupported]
           ,[DiscoveryJobType]
           ,[EnrollmentJobType]
           ,[InventoryEndpoint]
           ,[EntryPasswordSupported]
           ,[StorePasswordRequired]
           ,[PrivateKeyAllowed]
           ,[StorePathType]
           ,[CustomAliasAllowed]
           ,[JobProperties]
           ,[PowerShell]
           ,[PasswordStyle]
           ,[BlueprintAllowed])
     VALUES
		   ('F5 CA Bundles REST',	'F5-CA-REST',	0,	5,	104,	'63E394D4-B73B-43D3-8A96-07907A864C74',	'0CFD99FE-61BF-4A1A-91AA-DFF58B75BFBF',	1,	1,	0,	'1253642F-321B-45B4-A754-44C07DDC3D64',	NULL,	'/AnyInventory/Update',	0,	0,	0,	NULL,	2,	'[""]',	0,	0,	1);
SET @f5ca = @@IDENTITY;

INSERT INTO [cms_agents].[CertStoreTypeProperties]
           ([StoreTypeId]
           ,[Name]
           ,[DisplayName]
           ,[Type]
           ,[Required]
           ,[DependsOn]
           ,[DefaultValue])
     VALUES
		   (@f5ws,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', ''),	
		   (@f5ws,	'PrimaryNode',					'Primary Node',							0,	0, 'PrimaryNodeOnlineRequired', ''),	
		   (@f5ws,	'PrimaryNodeCheckRetryWaitSecs','Primary Node Check Retry Wait Seconds',0,	0, 'PrimaryNodeOnlineRequired', '120'),
		   (@f5ws,	'PrimaryNodeCheckRetryMax',		'Primary Node Check Retry Maximum',		0,	0, 'PrimaryNodeOnlineRequired', '3'),
		   (@f5ssl,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', ''),	
		   (@f5ssl,	'PrimaryNode',					'Primary Node',							0,	0, 'PrimaryNodeOnlineRequired', ''),	
		   (@f5ssl,	'PrimaryNodeCheckRetryWaitSecs','Primary Node Check Retry Wait Seconds',0,	0, 'PrimaryNodeOnlineRequired', '120'),
		   (@f5ssl,	'PrimaryNodeCheckRetryMax',		'Primary Node Check Retry Maximum',		0,	0, 'PrimaryNodeOnlineRequired', '3'),
		   (@f5ssl,	'F5Version',					'Version of F5',						2,	1, '', 'v12,v13,v14,v15'),
		   (@f5ca,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', ''),	
		   (@f5ca,	'PrimaryNode',					'Primary Node',							0,	0, 'PrimaryNodeOnlineRequired', ''),	
		   (@f5ca,	'PrimaryNodeCheckRetryMax',		'Primary Node Check Retry Maximum',		0,	0, 'PrimaryNodeOnlineRequired', '3'),
		   (@f5ca,	'PrimaryNodeCheckRetryWaitSecs','Primary Node Check Retry Wait Seconds',0,	0, 'PrimaryNodeOnlineRequired', '120'),
		   (@f5ca,	'F5Version',					'Version of F5',						2,	1, '', 'v12,v13,v14,v15');

INSERT INTO [cms_agents].[JobTypes]
           ([Id]
           ,[ConfigurationEndpoint]
           ,[CompletionEndpoint]
           ,[SubmitEndpoint]
           ,[Name]
           ,[Description])
     VALUES
           ('1802DF3C-322B-4E4C-AD59-6E84DA1AA88E',	'AnyManagement/Configure',	'AnyManagement/Complete',	NULL,	'F5-WS-RESTManagement',	'F5 Web Server REST Management'),
			('7639C64D-BBF1-402A-8838-1A8EE5E06855',	'AnyInventory/Configure',	'AnyInventory/Complete',	NULL,	'F5-WS-RESTInventory',	'F5 Web Server REST Inventory'),
			('B98621F3-D779-40D3-8F09-EECF32D68183',	'AnyInventory/Configure',	'AnyInventory/Complete',	NULL,	'F5-SL-RESTInventory',	'F5 SSL Profiles REST Inventory'),
			('433D98CA-E570-4F7A-8F32-4D31DC19002E',	'AnyManagement/Configure',	'AnyManagement/Complete',	NULL,	'F5-SL-RESTManagement',	'F5 SSL Profiles REST Management'),
			('0D893E43-FBE4-4E32-8067-6999E70C6864',	'AnyDiscovery/Configure',	'AnyDiscovery/Complete',	NULL,	'F5-SL-RESTDiscovery',	'F5 SSL Profiles REST Discovery'),
			('1253642F-321B-45B4-A754-44C07DDC3D64',	'AnyDiscovery/Configure',	'AnyDiscovery/Complete',	NULL,	'F5-CA-RESTDiscovery',	'F5 CA Bundles REST Discovery'),
			('63E394D4-B73B-43D3-8A96-07907A864C74',	'AnyInventory/Configure',	'AnyInventory/Complete',	NULL,	'F5-CA-RESTInventory',	'F5 CA Bundles REST Inventory'),
			('0CFD99FE-61BF-4A1A-91AA-DFF58B75BFBF',	'AnyManagement/Configure',	'AnyManagement/Complete',	NULL,	'F5-CA-RESTManagement',	'F5 CA Bundles REST Management');
