-- Declare variables
DECLARE @f5ws INT = -1, @f5ssl INT = -1, @f5ca INT = -1

-- Determine if the F5 web server type is defined
SELECT @f5ws = [Id] FROM [cms_agents].[CertStoreTypes] WHERE [ShortName] = 'F5-WS-REST'
IF NOT @f5ws = -1
BEGIN
    IF NOT EXISTS(SELECT [StoreType] FROM [cms_agents].[CertStoreTypeProperties] WHERE [StoreTypeId] = @f5ws AND [Name] = 'PrimaryNodeOnlineRequired')
    BEGIN
        INSERT INTO [cms_agents].[CertStoreTypeProperties]
                   ([StoreTypeId]
                   ,[Name]
                   ,[DisplayName]
                   ,[Type]
                   ,[Required]
                   ,[DependsOn]
                   ,[DefaultValue])
             VALUES
		           (@f5ws,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', '')

        UPDATE [cms_agents].[CertStoreTypeProperties]
            SET [Required] = 1, [DependsOn] = 'PrimaryNodeOnlineRequired'
        WHERE [StoreTypeId] = @f5ws
            AND [Name] IN ('PrimaryNode','PrimaryNodeCheckRetryWaitSecs','PrimaryNodeCheckRetryMax')
    END
END

-- Determine if the F5 ssl profile type is defined
SELECT @f5ssl = [StoreType] FROM [cms_agents].[CertStoreTypes] WHERE [ShortName] = 'F5-SL-REST'
IF NOT @f5ssl = -1
BEGIN
    IF NOT EXISTS(SELECT [Id] FROM [cms_agents].[CertStoreTypeProperties] WHERE [StoreTypeId] = @f5ssl AND [Name] = 'PrimaryNodeOnlineRequired')
    BEGIN
        INSERT INTO [cms_agents].[CertStoreTypeProperties]
                   ([StoreTypeId]
                   ,[Name]
                   ,[DisplayName]
                   ,[Type]
                   ,[Required]
                   ,[DependsOn]
                   ,[DefaultValue])
             VALUES
		           (@f5ssl,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', '')

        UPDATE [cms_agents].[CertStoreTypeProperties]
            SET [Required] = 1, [DependsOn] = 'PrimaryNodeOnlineRequired'
        WHERE [StoreTypeId] = @f5ssl
            AND [Name] IN ('PrimaryNode','PrimaryNodeCheckRetryWaitSecs','PrimaryNodeCheckRetryMax')
    END
END

-- Determine if the F5 ca bundle type is defined
SELECT @f5ca = [StoreType] FROM [cms_agents].[CertStoreTypes] WHERE [ShortName] = 'F5-CA-REST'
IF NOT @f5ca = -1
BEGIN
    IF NOT EXISTS(SELECT [Id] FROM [cms_agents].[CertStoreTypeProperties] WHERE [StoreTypeId] = @f5ca AND [Name] = 'PrimaryNodeOnlineRequired')
    BEGIN
        INSERT INTO [cms_agents].[CertStoreTypeProperties]
                   ([StoreTypeId]
                   ,[Name]
                   ,[DisplayName]
                   ,[Type]
                   ,[Required]
                   ,[DependsOn]
                   ,[DefaultValue])
             VALUES
		           (@f5ca,	'PrimaryNodeOnlineRequired',	'Primary Node Online Required',			1,	1, '', '')

        UPDATE [cms_agents].[CertStoreTypeProperties]
            SET [Required] = 1, [DependsOn] = 'PrimaryNodeOnlineRequired'
        WHERE [StoreTypeId] = @f5ca
            AND [Name] IN ('PrimaryNode','PrimaryNodeCheckRetryWaitSecs','PrimaryNodeCheckRetryMax')
    END
END
