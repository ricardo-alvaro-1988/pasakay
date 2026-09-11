using Microsoft.EntityFrameworkCore;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

public static class MerchantCatalogBootstrap
{
    public const string MigrationId = "20260911014037_OperatorMerchantCatalog";

    /// <summary>
    /// Applies merchant catalog schema idempotently and marks the EF migration as done.
    /// Used when MigrateAsync fails/times out so AppUser queries (login) still work.
    /// </summary>
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ProductAddonOptions]', N'U') IS NOT NULL DROP TABLE [ProductAddonOptions];
            IF OBJECT_ID(N'[ProductAddonGroups]', N'U') IS NOT NULL DROP TABLE [ProductAddonGroups];
            IF OBJECT_ID(N'[MerchantProducts]', N'U') IS NOT NULL DROP TABLE [MerchantProducts];
            IF OBJECT_ID(N'[MerchantProductCategories]', N'U') IS NOT NULL DROP TABLE [MerchantProductCategories];
            IF OBJECT_ID(N'[MerchantOperatingHours]', N'U') IS NOT NULL DROP TABLE [MerchantOperatingHours];
            IF OBJECT_ID(N'[Merchants]', N'U') IS NOT NULL DROP TABLE [Merchants];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Users_MerchantId' AND object_id = OBJECT_ID(N'[Users]')
            )
                DROP INDEX [IX_Users_MerchantId] ON [Users];

            IF COL_LENGTH(N'Users', N'MerchantId') IS NULL
                ALTER TABLE [Users] ADD [MerchantId] uniqueidentifier NULL;

            IF OBJECT_ID(N'[Merchants]', N'U') IS NULL
            BEGIN
                CREATE TABLE [Merchants] (
                    [Id] uniqueidentifier NOT NULL,
                    [OperatorId] uniqueidentifier NOT NULL,
                    [BusinessName] nvarchar(160) NOT NULL,
                    [ContactPerson] nvarchar(120) NOT NULL,
                    [Latitude] float NOT NULL,
                    [Longitude] float NOT NULL,
                    [PinnedAddress] nvarchar(400) NOT NULL,
                    [ManagedByMerchant] bit NOT NULL,
                    [Mobile] nvarchar(20) NOT NULL,
                    [Email] nvarchar(160) NOT NULL,
                    [AppUserId] uniqueidentifier NULL,
                    [LogoPath] nvarchar(260) NULL,
                    [BackgroundPath] nvarchar(260) NULL,
                    [IsActive] bit NOT NULL,
                    [SortOrder] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_Merchants] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_Merchants_Operators_OperatorId] FOREIGN KEY ([OperatorId]) REFERENCES [Operators] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_Merchants_Users_AppUserId] FOREIGN KEY ([AppUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );
            END

            IF OBJECT_ID(N'[MerchantOperatingHours]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantOperatingHours] (
                    [Id] uniqueidentifier NOT NULL,
                    [MerchantId] uniqueidentifier NOT NULL,
                    [DayOfWeek] int NOT NULL,
                    [IsClosed] bit NOT NULL,
                    [OpenTime] time NULL,
                    [CloseTime] time NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantOperatingHours] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantOperatingHours_Merchants_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [Merchants] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[MerchantProductCategories]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantProductCategories] (
                    [Id] uniqueidentifier NOT NULL,
                    [MerchantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantProductCategories] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantProductCategories_Merchants_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [Merchants] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[MerchantProducts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantProducts] (
                    [Id] uniqueidentifier NOT NULL,
                    [MerchantId] uniqueidentifier NOT NULL,
                    [CategoryId] uniqueidentifier NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [Description] nvarchar(1000) NOT NULL,
                    [BasePrice] decimal(18,2) NOT NULL,
                    [AvailableOnStorefront] bit NOT NULL CONSTRAINT [DF_MerchantProducts_AvailableOnStorefront] DEFAULT CAST(1 AS bit),
                    [SortOrder] int NOT NULL,
                    [ImagePath] nvarchar(260) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantProducts] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantProducts_Merchants_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [Merchants] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_MerchantProducts_MerchantProductCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [MerchantProductCategories] ([Id]) ON DELETE NO ACTION
                );
            END

            IF OBJECT_ID(N'[ProductAddonGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProductAddonGroups] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProductId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [MinSelect] int NOT NULL,
                    [MaxSelect] int NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_ProductAddonGroups] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ProductAddonGroups_MerchantProducts_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [MerchantProducts] ([Id]) ON DELETE CASCADE
                );
            END

            IF OBJECT_ID(N'[ProductAddonOptions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ProductAddonOptions] (
                    [Id] uniqueidentifier NOT NULL,
                    [AddonGroupId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [PriceDelta] decimal(18,2) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_ProductAddonOptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_ProductAddonOptions_ProductAddonGroups_AddonGroupId] FOREIGN KEY ([AddonGroupId]) REFERENCES [ProductAddonGroups] ([Id]) ON DELETE CASCADE
                );
            END

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Users_MerchantId' AND object_id = OBJECT_ID(N'[Users]')
            )
                CREATE INDEX [IX_Users_MerchantId] ON [Users] ([MerchantId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_MerchantOperatingHours_MerchantId_DayOfWeek' AND object_id = OBJECT_ID(N'[MerchantOperatingHours]')
            )
                CREATE UNIQUE INDEX [IX_MerchantOperatingHours_MerchantId_DayOfWeek] ON [MerchantOperatingHours] ([MerchantId], [DayOfWeek]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_MerchantProductCategories_MerchantId_Name' AND object_id = OBJECT_ID(N'[MerchantProductCategories]')
            )
                CREATE INDEX [IX_MerchantProductCategories_MerchantId_Name] ON [MerchantProductCategories] ([MerchantId], [Name]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_MerchantProducts_CategoryId' AND object_id = OBJECT_ID(N'[MerchantProducts]')
            )
                CREATE INDEX [IX_MerchantProducts_CategoryId] ON [MerchantProducts] ([CategoryId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_MerchantProducts_MerchantId' AND object_id = OBJECT_ID(N'[MerchantProducts]')
            )
                CREATE INDEX [IX_MerchantProducts_MerchantId] ON [MerchantProducts] ([MerchantId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Merchants_AppUserId' AND object_id = OBJECT_ID(N'[Merchants]')
            )
                CREATE INDEX [IX_Merchants_AppUserId] ON [Merchants] ([AppUserId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Merchants_OperatorId' AND object_id = OBJECT_ID(N'[Merchants]')
            )
                CREATE INDEX [IX_Merchants_OperatorId] ON [Merchants] ([OperatorId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_Merchants_OperatorId_BusinessName' AND object_id = OBJECT_ID(N'[Merchants]')
            )
                CREATE INDEX [IX_Merchants_OperatorId_BusinessName] ON [Merchants] ([OperatorId], [BusinessName]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ProductAddonGroups_ProductId' AND object_id = OBJECT_ID(N'[ProductAddonGroups]')
            )
                CREATE INDEX [IX_ProductAddonGroups_ProductId] ON [ProductAddonGroups] ([ProductId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_ProductAddonOptions_AddonGroupId' AND object_id = OBJECT_ID(N'[ProductAddonOptions]')
            )
                CREATE INDEX [IX_ProductAddonOptions_AddonGroupId] ON [ProductAddonOptions] ([AddonGroupId]);

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911014037_OperatorMerchantCatalog'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911014037_OperatorMerchantCatalog', N'9.0.8');
            """, cancellationToken);
    }

    public static async Task EnsureUsersMerchantIdAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'Users', N'MerchantId') IS NULL
                ALTER TABLE [Users] ADD [MerchantId] uniqueidentifier NULL;

            IF COL_LENGTH(N'Users', N'MerchantId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Users_MerchantId' AND object_id = OBJECT_ID(N'[Users]')
               )
                CREATE INDEX [IX_Users_MerchantId] ON [Users] ([MerchantId]);
            """, cancellationToken);
    }

    public static async Task EnsureAddonLibraryAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[MerchantAddonGroups]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantAddonGroups] (
                    [Id] uniqueidentifier NOT NULL,
                    [MerchantId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [MinSelect] int NOT NULL,
                    [MaxSelect] int NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantAddonGroups] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantAddonGroups_Merchants_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [Merchants] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_MerchantAddonGroups_MerchantId] ON [MerchantAddonGroups] ([MerchantId]);
            END

            IF OBJECT_ID(N'[MerchantAddonOptions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantAddonOptions] (
                    [Id] uniqueidentifier NOT NULL,
                    [AddonGroupId] uniqueidentifier NOT NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [PriceDelta] decimal(18,2) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantAddonOptions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantAddonOptions_MerchantAddonGroups_AddonGroupId] FOREIGN KEY ([AddonGroupId]) REFERENCES [MerchantAddonGroups] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_MerchantAddonOptions_AddonGroupId] ON [MerchantAddonOptions] ([AddonGroupId]);
            END

            IF OBJECT_ID(N'[MerchantProductAddons]', N'U') IS NULL
            BEGIN
                CREATE TABLE [MerchantProductAddons] (
                    [Id] uniqueidentifier NOT NULL,
                    [ProductId] uniqueidentifier NOT NULL,
                    [AddonGroupId] uniqueidentifier NOT NULL,
                    [SortOrder] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_MerchantProductAddons] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_MerchantProductAddons_MerchantProducts_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [MerchantProducts] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_MerchantProductAddons_MerchantAddonGroups_AddonGroupId] FOREIGN KEY ([AddonGroupId]) REFERENCES [MerchantAddonGroups] ([Id]) ON DELETE NO ACTION
                );
                CREATE UNIQUE INDEX [IX_MerchantProductAddons_ProductId_AddonGroupId] ON [MerchantProductAddons] ([ProductId], [AddonGroupId]);
            END

            IF COL_LENGTH(N'MerchantProducts', N'AvailableFromTime') IS NULL
                ALTER TABLE [MerchantProducts] ADD [AvailableFromTime] time NULL;
            IF COL_LENGTH(N'MerchantProducts', N'AvailableToTime') IS NULL
                ALTER TABLE [MerchantProducts] ADD [AvailableToTime] time NULL;

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911030157_MerchantAddonLibrary'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911030157_MerchantAddonLibrary', N'9.0.8');
            """, cancellationToken);
    }

    public static async Task<bool> HasMerchantsTableAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var conn = db.Database.GetDbConnection();
        var shouldClose = conn.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[Merchants]', N'U') IS NULL THEN 0 ELSE 1 END";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
