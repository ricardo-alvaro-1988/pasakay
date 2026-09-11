using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YaPasakay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorMerchantCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: recover from a failed prior attempt that left partial objects
            // and avoid SQL Server "multiple cascade paths" on MerchantProducts.
            migrationBuilder.Sql("""
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

                CREATE INDEX [IX_Users_MerchantId] ON [Users] ([MerchantId]);
                CREATE UNIQUE INDEX [IX_MerchantOperatingHours_MerchantId_DayOfWeek] ON [MerchantOperatingHours] ([MerchantId], [DayOfWeek]);
                CREATE INDEX [IX_MerchantProductCategories_MerchantId_Name] ON [MerchantProductCategories] ([MerchantId], [Name]);
                CREATE INDEX [IX_MerchantProducts_CategoryId] ON [MerchantProducts] ([CategoryId]);
                CREATE INDEX [IX_MerchantProducts_MerchantId] ON [MerchantProducts] ([MerchantId]);
                CREATE INDEX [IX_Merchants_AppUserId] ON [Merchants] ([AppUserId]);
                CREATE INDEX [IX_Merchants_OperatorId] ON [Merchants] ([OperatorId]);
                CREATE INDEX [IX_Merchants_OperatorId_BusinessName] ON [Merchants] ([OperatorId], [BusinessName]);
                CREATE INDEX [IX_ProductAddonGroups_ProductId] ON [ProductAddonGroups] ([ProductId]);
                CREATE INDEX [IX_ProductAddonOptions_AddonGroupId] ON [ProductAddonOptions] ([AddonGroupId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                IF COL_LENGTH(N'Users', N'MerchantId') IS NOT NULL
                    ALTER TABLE [Users] DROP COLUMN [MerchantId];
                """);
        }
    }
}
