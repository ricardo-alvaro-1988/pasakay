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
        // Users.MerchantId must be its own batch — never ADD + CREATE INDEX together.
        await EnsureUsersMerchantIdAsync(db, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ProductAddonOptions]', N'U') IS NOT NULL DROP TABLE [ProductAddonOptions];
            IF OBJECT_ID(N'[ProductAddonGroups]', N'U') IS NOT NULL DROP TABLE [ProductAddonGroups];
            IF OBJECT_ID(N'[MerchantProducts]', N'U') IS NOT NULL DROP TABLE [MerchantProducts];
            IF OBJECT_ID(N'[MerchantProductCategories]', N'U') IS NOT NULL DROP TABLE [MerchantProductCategories];
            IF OBJECT_ID(N'[MerchantOperatingHours]', N'U') IS NOT NULL DROP TABLE [MerchantOperatingHours];
            IF OBJECT_ID(N'[Merchants]', N'U') IS NOT NULL DROP TABLE [Merchants];

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
        // Separate batches: SQL Server can reject CREATE INDEX on a column added in the same batch,
        // rolling back the ALTER TABLE and leaving Users without MerchantId while EF still selects it.
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'Users', N'MerchantId') IS NULL
                ALTER TABLE [Users] ADD [MerchantId] uniqueidentifier NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'Users', N'MerchantId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Users_MerchantId' AND object_id = OBJECT_ID(N'[Users]')
               )
                CREATE INDEX [IX_Users_MerchantId] ON [Users] ([MerchantId]);
            """, cancellationToken);
    }

    public static async Task<bool> HasUsersMerchantIdAsync(AppDbContext db, CancellationToken cancellationToken = default)
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
            cmd.CommandText = "SELECT CASE WHEN COL_LENGTH(N'Users', N'MerchantId') IS NULL THEN 0 ELSE 1 END";
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
                    [BasePrice] decimal(18,2) NOT NULL CONSTRAINT [DF_MerchantAddonOptions_BasePrice] DEFAULT (0),
                    [SellingPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_MerchantAddonOptions_SellingPrice] DEFAULT (0),
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

    public static async Task EnsureDualPricingAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'MerchantProducts', N'SellingPrice') IS NULL
            BEGIN
                ALTER TABLE [MerchantProducts] ADD [SellingPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_MerchantProducts_SellingPrice] DEFAULT (0);
                UPDATE [MerchantProducts] SET [SellingPrice] = [BasePrice];
            END

            IF OBJECT_ID(N'[MerchantAddonOptions]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'MerchantAddonOptions', N'SellingPrice') IS NULL
                   AND COL_LENGTH(N'MerchantAddonOptions', N'PriceDelta') IS NOT NULL
                BEGIN
                    EXEC sp_rename N'MerchantAddonOptions.PriceDelta', N'SellingPrice', N'COLUMN';
                END

                IF COL_LENGTH(N'MerchantAddonOptions', N'SellingPrice') IS NULL
                    ALTER TABLE [MerchantAddonOptions] ADD [SellingPrice] decimal(18,2) NOT NULL CONSTRAINT [DF_MerchantAddonOptions_SellingPrice] DEFAULT (0);

                IF COL_LENGTH(N'MerchantAddonOptions', N'BasePrice') IS NULL
                    ALTER TABLE [MerchantAddonOptions] ADD [BasePrice] decimal(18,2) NOT NULL CONSTRAINT [DF_MerchantAddonOptions_BasePrice] DEFAULT (0);
            END

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911041500_MerchantDualPricing'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911041500_MerchantDualPricing', N'9.0.8');
            """, cancellationToken);
    }

    public static async Task EnsurePabiliMatrixAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'Operators', N'PabiliFareSystemCommissionPercent') IS NULL
                ALTER TABLE [Operators] ADD [PabiliFareSystemCommissionPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_Operators_PabiliFareSystemCommissionPercent] DEFAULT (10);

            IF COL_LENGTH(N'Operators', N'PabiliMarkupSystemCommissionPercent') IS NULL
                ALTER TABLE [Operators] ADD [PabiliMarkupSystemCommissionPercent] decimal(5,2) NOT NULL CONSTRAINT [DF_Operators_PabiliMarkupSystemCommissionPercent] DEFAULT (10);

            IF OBJECT_ID(N'[PabiliMatrices]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliMatrices] (
                    [Id] uniqueidentifier NOT NULL,
                    [OperatorId] uniqueidentifier NOT NULL,
                    [BaseFareAmount] decimal(18,2) NOT NULL,
                    [KmScope] decimal(6,2) NOT NULL,
                    [SucceedingKm] decimal(18,2) NOT NULL,
                    [FareOperatorCommissionPercent] decimal(5,2) NOT NULL,
                    [FareRiderCommissionPercent] decimal(5,2) NOT NULL,
                    [MarkupOperatorCommissionPercent] decimal(5,2) NOT NULL,
                    [MarkupRiderCommissionPercent] decimal(5,2) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliMatrices] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliMatrices_Operators_OperatorId] FOREIGN KEY ([OperatorId]) REFERENCES [Operators] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_PabiliMatrices_OperatorId] ON [PabiliMatrices] ([OperatorId]);
            END

            IF OBJECT_ID(N'[PabiliSurcharges]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliSurcharges] (
                    [Id] uniqueidentifier NOT NULL,
                    [PabiliMatrixId] uniqueidentifier NOT NULL,
                    [Kind] int NOT NULL,
                    [Name] nvarchar(80) NOT NULL,
                    [Amount] decimal(18,2) NOT NULL,
                    [WindowStart] time NULL,
                    [WindowEnd] time NULL,
                    [RangeStartUtc] datetime2 NULL,
                    [RangeEndUtc] datetime2 NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliSurcharges] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliSurcharges_PabiliMatrices_PabiliMatrixId] FOREIGN KEY ([PabiliMatrixId]) REFERENCES [PabiliMatrices] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_PabiliSurcharges_PabiliMatrixId] ON [PabiliSurcharges] ([PabiliMatrixId]);
            END

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911042730_PabiliMatrix'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911042730_PabiliMatrix', N'9.0.8');

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911072342_PabiliSurcharges'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911072342_PabiliSurcharges', N'9.0.8');
            """, cancellationToken);
    }

    public static async Task EnsurePabiliRiderFlagsAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'RiderProfiles', N'AcceptsPasakay') IS NULL
                ALTER TABLE [RiderProfiles] ADD [AcceptsPasakay] bit NOT NULL CONSTRAINT [DF_RiderProfiles_AcceptsPasakay] DEFAULT (1);

            IF COL_LENGTH(N'RiderProfiles', N'AcceptsPabili') IS NULL
                ALTER TABLE [RiderProfiles] ADD [AcceptsPabili] bit NOT NULL CONSTRAINT [DF_RiderProfiles_AcceptsPabili] DEFAULT (0);

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911073842_PabiliRiderFlags'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911073842_PabiliRiderFlags', N'9.0.8');
            """, cancellationToken);
    }

    public static async Task EnsurePabiliOrdersAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'RiderWalletTransactions', N'PabiliOrderId') IS NULL
                ALTER TABLE [RiderWalletTransactions] ADD [PabiliOrderId] uniqueidentifier NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'RiderWalletTransactions', N'PabiliOrderId') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RiderWalletTransactions_PabiliOrderId' AND object_id = OBJECT_ID(N'[RiderWalletTransactions]'))
                CREATE UNIQUE INDEX [IX_RiderWalletTransactions_PabiliOrderId]
                    ON [RiderWalletTransactions] ([PabiliOrderId])
                    WHERE [PabiliOrderId] IS NOT NULL;
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrders]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliOrders] (
                    [Id] uniqueidentifier NOT NULL,
                    [OperatorId] uniqueidentifier NOT NULL,
                    [MerchantId] uniqueidentifier NOT NULL,
                    [CustomerId] uniqueidentifier NOT NULL,
                    [RiderId] uniqueidentifier NULL,
                    [Reference] nvarchar(40) NOT NULL,
                    [Status] int NOT NULL,
                    [CustomerName] nvarchar(120) NOT NULL,
                    [CustomerPhone] nvarchar(20) NOT NULL,
                    [MerchantName] nvarchar(160) NOT NULL,
                    [PickupAddress] nvarchar(260) NOT NULL,
                    [PickupLat] float NOT NULL,
                    [PickupLng] float NOT NULL,
                    [DropoffAddress] nvarchar(260) NOT NULL,
                    [DropoffLat] float NOT NULL,
                    [DropoffLng] float NOT NULL,
                    [DropoffBarangayId] uniqueidentifier NULL,
                    [DistanceKm] decimal(18,2) NOT NULL,
                    [GoodsSubtotal] decimal(18,2) NOT NULL,
                    [GoodsBaseSubtotal] decimal(18,2) NOT NULL,
                    [DeliveryFee] decimal(18,2) NOT NULL,
                    [SurchargeTotal] decimal(18,2) NOT NULL,
                    [AdjustmentAmount] decimal(18,2) NOT NULL,
                    [AdjustmentLabel] nvarchar(120) NOT NULL,
                    [CustomerTotal] decimal(18,2) NOT NULL,
                    [PaymentMethod] int NOT NULL,
                    [Notes] nvarchar(500) NULL,
                    [AcceptedAtUtc] datetime2 NULL,
                    [PickedUpAtUtc] datetime2 NULL,
                    [DeliveringAtUtc] datetime2 NULL,
                    [CompletedAtUtc] datetime2 NULL,
                    [CancelledAtUtc] datetime2 NULL,
                    [CancelledBy] int NOT NULL,
                    [CancelReason] nvarchar(200) NULL,
                    [FareSystemPercent] decimal(5,2) NULL,
                    [FareOperatorPercent] decimal(5,2) NULL,
                    [FareRiderPercent] decimal(5,2) NULL,
                    [MarkupSystemPercent] decimal(5,2) NULL,
                    [MarkupOperatorPercent] decimal(5,2) NULL,
                    [MarkupRiderPercent] decimal(5,2) NULL,
                    [FareSystemAmount] decimal(18,2) NULL,
                    [FareOperatorAmount] decimal(18,2) NULL,
                    [FareRiderAmount] decimal(18,2) NULL,
                    [MarkupSystemAmount] decimal(18,2) NULL,
                    [MarkupOperatorAmount] decimal(18,2) NULL,
                    [MarkupRiderAmount] decimal(18,2) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliOrders] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliOrders_Operators_OperatorId] FOREIGN KEY ([OperatorId]) REFERENCES [Operators] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PabiliOrders_Merchants_MerchantId] FOREIGN KEY ([MerchantId]) REFERENCES [Merchants] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PabiliOrders_CustomerProfiles_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [CustomerProfiles] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PabiliOrders_RiderProfiles_RiderId] FOREIGN KEY ([RiderId]) REFERENCES [RiderProfiles] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_PabiliOrders_Barangays_DropoffBarangayId] FOREIGN KEY ([DropoffBarangayId]) REFERENCES [Barangays] ([Id]) ON DELETE NO ACTION
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrders]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrders_OperatorId' AND object_id = OBJECT_ID(N'[PabiliOrders]'))
                    CREATE INDEX [IX_PabiliOrders_OperatorId] ON [PabiliOrders] ([OperatorId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrders_OperatorId_Status_CreatedAtUtc' AND object_id = OBJECT_ID(N'[PabiliOrders]'))
                    CREATE INDEX [IX_PabiliOrders_OperatorId_Status_CreatedAtUtc] ON [PabiliOrders] ([OperatorId], [Status], [CreatedAtUtc]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrders_CustomerId' AND object_id = OBJECT_ID(N'[PabiliOrders]'))
                    CREATE INDEX [IX_PabiliOrders_CustomerId] ON [PabiliOrders] ([CustomerId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrders_RiderId' AND object_id = OBJECT_ID(N'[PabiliOrders]'))
                    CREATE INDEX [IX_PabiliOrders_RiderId] ON [PabiliOrders] ([RiderId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrders_Reference' AND object_id = OBJECT_ID(N'[PabiliOrders]'))
                    CREATE INDEX [IX_PabiliOrders_Reference] ON [PabiliOrders] ([Reference]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrderItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliOrderItems] (
                    [Id] uniqueidentifier NOT NULL,
                    [OrderId] uniqueidentifier NOT NULL,
                    [ProductId] uniqueidentifier NULL,
                    [Name] nvarchar(160) NOT NULL,
                    [Quantity] int NOT NULL,
                    [UnitBasePrice] decimal(18,2) NOT NULL,
                    [UnitSellingPrice] decimal(18,2) NOT NULL,
                    [LineBaseTotal] decimal(18,2) NOT NULL,
                    [LineSellingTotal] decimal(18,2) NOT NULL,
                    [SortOrder] int NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliOrderItems] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliOrderItems_PabiliOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [PabiliOrders] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_PabiliOrderItems_OrderId] ON [PabiliOrderItems] ([OrderId]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrderItemAddons]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliOrderItemAddons] (
                    [Id] uniqueidentifier NOT NULL,
                    [OrderItemId] uniqueidentifier NOT NULL,
                    [AddonOptionId] uniqueidentifier NULL,
                    [Name] nvarchar(120) NOT NULL,
                    [Quantity] int NOT NULL,
                    [UnitBasePrice] decimal(18,2) NOT NULL,
                    [UnitSellingPrice] decimal(18,2) NOT NULL,
                    [LineBaseTotal] decimal(18,2) NOT NULL,
                    [LineSellingTotal] decimal(18,2) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliOrderItemAddons] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliOrderItemAddons_PabiliOrderItems_OrderItemId] FOREIGN KEY ([OrderItemId]) REFERENCES [PabiliOrderItems] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_PabiliOrderItemAddons_OrderItemId] ON [PabiliOrderItemAddons] ([OrderItemId]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrderOffers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [PabiliOrderOffers] (
                    [Id] uniqueidentifier NOT NULL,
                    [OrderId] uniqueidentifier NOT NULL,
                    [RiderId] uniqueidentifier NOT NULL,
                    [Status] int NOT NULL,
                    [DistanceKm] decimal(18,2) NULL,
                    [OfferedAtUtc] datetime2 NOT NULL,
                    [ExpiresAtUtc] datetime2 NOT NULL,
                    [RespondedAtUtc] datetime2 NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_PabiliOrderOffers] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PabiliOrderOffers_PabiliOrders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [PabiliOrders] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_PabiliOrderOffers_RiderProfiles_RiderId] FOREIGN KEY ([RiderId]) REFERENCES [RiderProfiles] ([Id]) ON DELETE NO ACTION
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[PabiliOrderOffers]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrderOffers_OrderId_RiderId' AND object_id = OBJECT_ID(N'[PabiliOrderOffers]'))
                    CREATE UNIQUE INDEX [IX_PabiliOrderOffers_OrderId_RiderId] ON [PabiliOrderOffers] ([OrderId], [RiderId]);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PabiliOrderOffers_RiderId_Status' AND object_id = OBJECT_ID(N'[PabiliOrderOffers]'))
                    CREATE INDEX [IX_PabiliOrderOffers_RiderId_Status] ON [PabiliOrderOffers] ([RiderId], [Status]);
            END

            IF COL_LENGTH(N'RiderWalletTransactions', N'PabiliOrderId') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RiderWalletTransactions_PabiliOrders_PabiliOrderId'
               )
                ALTER TABLE [RiderWalletTransactions] WITH CHECK
                    ADD CONSTRAINT [FK_RiderWalletTransactions_PabiliOrders_PabiliOrderId]
                    FOREIGN KEY ([PabiliOrderId]) REFERENCES [PabiliOrders] ([Id]) ON DELETE NO ACTION;

            IF NOT EXISTS (
                SELECT 1 FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911083227_PabiliOrders'
            )
                INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20260911083227_PabiliOrders', N'9.0.8');
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
