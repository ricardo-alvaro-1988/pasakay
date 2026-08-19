using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Services;

namespace YaPasakay.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string AdminPhone = "09000000000";
    public const string SupportStaffPhone = "09000000001";
    public const string OperatorPhone = "09170001111";
    public const string RiderPassword = "123456";
    public const string OperatorPassword = "123456";
    public const string AdminPassword = "123456";

    public static async Task SeedAsync(AppDbContext db, ILogger? logger = null, string? uploadRoot = null, CancellationToken cancellationToken = default)
    {
        await SeedTerritoriesAsync(db, logger, cancellationToken);
        await SeedAdminAsync(db, cancellationToken);
        await SeedAccessAsync(db, cancellationToken);
        await SeedAdminPasswordsAsync(db, cancellationToken);
        var op = await SeedOperatorAsync(db, cancellationToken);
        await SeedOperatorPasswordsAsync(db, cancellationToken);
        await SeedOperatorAddressAsync(db, op, cancellationToken);
        await SeedOperatorAreasAsync(db, op, cancellationToken);
        await SeedRidersAsync(db, op, cancellationToken);
        await SeedRiderPasswordsAsync(db, cancellationToken);
        await SeedRiderPaymentMethodsAsync(db, cancellationToken);
        await SeedRiderWalletsAsync(db, cancellationToken);
        await SeedRiderLicensesAsync(db, op, cancellationToken);
        await SeedRiderAddressesAsync(db, op, cancellationToken);
        await SeedRiderLocationsAsync(db, cancellationToken);
        await SeedCustomersAsync(db, uploadRoot, cancellationToken);
        await SeedTripsAsync(db, op, cancellationToken);
        await SeedTripPaymentMethodsAsync(db, cancellationToken);
        await SeedTripCommissionDeductionsAsync(db, cancellationToken);
        await SeedWalletRequestsAsync(db, cancellationToken);
        await SeedBookingPipelineAsync(db, op, cancellationToken);
        await ReleaseCustomerAppTripsAsync(db, cancellationToken);
        await SeedScheduledBookingsAsync(db, op, cancellationToken);
        await SeedTripBookingDetailsAsync(db, op, cancellationToken);
        await SeedTripStopsAsync(db, op, cancellationToken);
        await SeedTripCoordinatesAsync(db, op, cancellationToken);
        await SeedTripRatingsAsync(db, op, cancellationToken);
        await SeedTripChatsAsync(db, op, cancellationToken);
        await SeedTripCustomersAsync(db, op, cancellationToken);
        await SeedAccountDeleteAlertsAsync(db, cancellationToken);
        await SeedFareMatricesAsync(db, op, cancellationToken);
        await SeedFareCommissionSplitsAsync(db, op, cancellationToken);
        await SeedFareSurchargesAsync(db, op, cancellationToken);
        await SeedAnnouncementsAsync(db, cancellationToken);
        await SeedSupportTicketsAsync(db, op, cancellationToken);
        await SeedSosTicketLocationsAsync(db, cancellationToken);
        await SeedAdminSosAlertsAsync(db, cancellationToken);
        await SeedAuditLogsAsync(db, op, cancellationToken);
    }

    private static async Task SeedTerritoriesAsync(AppDbContext db, ILogger? logger, CancellationToken cancellationToken)
    {
        if (await db.Barangays.CountAsync(cancellationToken) >= 40_000)
        {
            return;
        }

        logger?.LogInformation("Seeding full PH territory catalog. This can take up to a minute...");
        db.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        var lgus = PhTerritoryCatalog.Lgus;
        var barangayRows = PhTerritoryCatalog.Barangays;

        var provinces = await db.Provinces.ToListAsync(cancellationToken);
        var davao = provinces.FirstOrDefault(x => PhTerritoryCatalog.Normalize(x.Name) == "DAVAO");
        if (davao is not null &&
            provinces.All(x => PhTerritoryCatalog.Normalize(x.Name) != "DAVAO DEL SUR"))
        {
            davao.Name = "Davao Del Sur";
            await db.SaveChangesAsync(cancellationToken);
        }

        var provinceByKey = provinces
            .GroupBy(x => PhTerritoryCatalog.Normalize(x.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var provinceName in lgus.Select(x => x.Province).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var key = PhTerritoryCatalog.Normalize(provinceName);
            if (provinceByKey.ContainsKey(key))
            {
                continue;
            }

            var province = new Province { Name = provinceName };
            db.Provinces.Add(province);
            provinces.Add(province);
            provinceByKey[key] = province;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger?.LogInformation("Provinces ready: {Count}", provinceByKey.Count);

        var municipalities = await db.Municipalities.Include(x => x.Province).ToListAsync(cancellationToken);
        var municipalityByKey = municipalities
            .GroupBy(x => PhTerritoryCatalog.Key(x.Province.Name, x.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var lgu in lgus)
        {
            var key = PhTerritoryCatalog.Key(lgu.Province, lgu.Name);
            if (municipalityByKey.ContainsKey(key))
            {
                continue;
            }

            if (!provinceByKey.TryGetValue(PhTerritoryCatalog.Normalize(lgu.Province), out var province))
            {
                continue;
            }

            var municipality = new Municipality
            {
                ProvinceId = province.Id,
                Name = lgu.Name
            };
            db.Municipalities.Add(municipality);
            municipalityByKey[key] = municipality;
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        logger?.LogInformation("Cities and municipalities ready: {Count}", municipalityByKey.Count);

        var existingBarangays = await db.Barangays
            .AsNoTracking()
            .Select(x => new { x.MunicipalityId, x.Name })
            .ToListAsync(cancellationToken);
        var barangayKeys = existingBarangays
            .Select(x => $"{x.MunicipalityId}|{PhTerritoryCatalog.NormalizeBarangay(x.Name)}")
            .ToHashSet(StringComparer.Ordinal);

        var inserted = 0;
        var batch = new List<Barangay>(5000);
        foreach (var row in barangayRows)
        {
            if (!municipalityByKey.TryGetValue(PhTerritoryCatalog.Key(row.Province, row.Municipality), out var municipality))
            {
                continue;
            }

            var key = $"{municipality.Id}|{PhTerritoryCatalog.NormalizeBarangay(row.Name)}";
            if (!barangayKeys.Add(key))
            {
                continue;
            }

            batch.Add(new Barangay
            {
                MunicipalityId = municipality.Id,
                Name = row.Name
            });

            if (batch.Count >= 5000)
            {
                await BulkInsertBarangaysAsync(db, batch, cancellationToken);
                inserted += batch.Count;
                batch.Clear();
                logger?.LogInformation("Barangays inserted: {Count}", inserted);
            }
        }

        if (batch.Count > 0)
        {
            await BulkInsertBarangaysAsync(db, batch, cancellationToken);
            inserted += batch.Count;
        }

        logger?.LogInformation("Territory seed complete. Added {Count} barangays.", inserted);
    }

    private static async Task BulkInsertBarangaysAsync(
        AppDbContext db,
        IReadOnlyList<Barangay> rows,
        CancellationToken cancellationToken)
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("CreatedAtUtc", typeof(DateTime));
        table.Columns.Add("MunicipalityId", typeof(Guid));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("UpdatedAtUtc", typeof(DateTime));
        foreach (var row in rows)
        {
            table.Rows.Add(row.Id, row.CreatedAtUtc, row.MunicipalityId, row.Name, DBNull.Value);
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        using var bulk = new SqlBulkCopy((SqlConnection)connection)
        {
            DestinationTableName = "Barangays",
            BatchSize = 5000,
            BulkCopyTimeout = 300
        };
        bulk.ColumnMappings.Add("Id", "Id");
        bulk.ColumnMappings.Add("CreatedAtUtc", "CreatedAtUtc");
        bulk.ColumnMappings.Add("MunicipalityId", "MunicipalityId");
        bulk.ColumnMappings.Add("Name", "Name");
        bulk.ColumnMappings.Add("UpdatedAtUtc", "UpdatedAtUtc");
        await bulk.WriteToServerAsync(table, cancellationToken);
    }

    private static async Task SeedOperatorAddressAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (op.AddressBarangayId.HasValue)
        {
            return;
        }

        var cebuMunicipalities = await db.Municipalities
            .Include(x => x.Province)
            .Where(x => x.Province.Name == "Cebu")
            .ToListAsync(cancellationToken);
        var cebuCity = cebuMunicipalities.FirstOrDefault(x => PhTerritoryCatalog.Normalize(x.Name) == "CEBU");
        if (cebuCity is null)
        {
            return;
        }

        var barangays = await db.Barangays
            .Where(x => x.MunicipalityId == cebuCity.Id)
            .ToListAsync(cancellationToken);
        var barangay = barangays.FirstOrDefault(x => PhTerritoryCatalog.NormalizeBarangay(x.Name) == "COGON RAMOS");
        if (barangay is null)
        {
            return;
        }

        var (ok, _) = await OperatorAddressSync.AssignAsync(db, op, barangay.Id, "123 Colon Street", cancellationToken);
        if (ok)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedOperatorAreasAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.OperatorBarangays.AnyAsync(x => x.OperatorId == op.Id, cancellationToken))
        {
            return;
        }

        var wanted = new (string Municipality, string Barangay)[]
        {
            ("Cebu City", "Lahug"),
            ("Cebu City", "Guadalupe"),
            ("Cebu City", "Mabolo"),
            ("Mandaue", "Centro"),
            ("Lapu-Lapu", "Poblacion")
        };

        var cebuMunicipalities = await db.Municipalities
            .Include(x => x.Province)
            .Where(x => x.Province.Name == "Cebu")
            .ToListAsync(cancellationToken);
        var municipalityByKey = cebuMunicipalities
            .GroupBy(x => PhTerritoryCatalog.Normalize(x.Name), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var municipalityIds = wanted
            .Select(w => municipalityByKey.GetValueOrDefault(PhTerritoryCatalog.Normalize(w.Municipality)))
            .Where(x => x is not null)
            .Select(x => x!.Id)
            .Distinct()
            .ToList();
        var barangays = municipalityIds.Count == 0
            ? []
            : await db.Barangays
                .Include(x => x.Municipality)
                .Where(x => municipalityIds.Contains(x.MunicipalityId))
                .ToListAsync(cancellationToken);

        var matches = wanted
            .Select(w => barangays.FirstOrDefault(x =>
                PhTerritoryCatalog.Normalize(x.Municipality.Name) == PhTerritoryCatalog.Normalize(w.Municipality) &&
                PhTerritoryCatalog.NormalizeBarangay(x.Name) == PhTerritoryCatalog.NormalizeBarangay(w.Barangay)))
            .Where(x => x is not null)
            .Cast<Barangay>()
            .ToList();

        if (matches.Count == 0)
        {
            return;
        }

        var (ok, _) = await OperatorAreaSync.AssignAsync(db, op, matches.Select(x => x.Id).ToList(), cancellationToken);
        if (ok)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedAdminAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(x => x.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        db.Users.Add(new AppUser
        {
            PhoneNumber = AdminPhone,
            FullName = "Administrator",
            PasswordHash = SecretHasher.Hash(AdminPassword),
            Role = UserRole.Admin,
            IsMainAdmin = true,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAccessAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var main = await db.Users.FirstOrDefaultAsync(
            x => x.PhoneNumber == AdminPhone && x.Role == UserRole.Admin,
            cancellationToken);
        if (main is not null)
        {
            main.IsMainAdmin = true;
            main.AccessGroupId = null;
            main.FullName = "Administrator";
            if (string.IsNullOrWhiteSpace(main.PasswordHash))
            {
                main.PasswordHash = SecretHasher.Hash(AdminPassword);
            }
        }

        var supportGroup = await EnsureRoleAsync(db, "Support", "Handles tickets and SOS.", ["support"], cancellationToken);
        await EnsureRoleAsync(db, "Operations", "Operators, customers, territories, and fares.", ["overview", "operators", "customers", "territories", "fares"], cancellationToken);
        await EnsureRoleAsync(db, "Finance", "Billing, fare matrix, and audit.", ["billing", "fares", "audit"], cancellationToken);

        if (!await db.Users.AnyAsync(x => x.PhoneNumber == SupportStaffPhone, cancellationToken))
        {
            db.Users.Add(new AppUser
            {
                PhoneNumber = SupportStaffPhone,
                FullName = "Lea Support",
                PasswordHash = SecretHasher.Hash(AdminPassword),
                Role = UserRole.Admin,
                IsMainAdmin = false,
                AccessGroupId = supportGroup.Id,
                IsActive = true
            });
        }
        else
        {
            var supportStaff = await db.Users.FirstOrDefaultAsync(
                x => x.PhoneNumber == SupportStaffPhone && x.Role == UserRole.Admin,
                cancellationToken);
            if (supportStaff is not null && string.IsNullOrWhiteSpace(supportStaff.PasswordHash))
            {
                supportStaff.PasswordHash = SecretHasher.Hash(AdminPassword);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminPasswordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Role == UserRole.Admin && (x.PasswordHash == null || x.PasswordHash == ""))
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            user.PasswordHash = SecretHasher.Hash(AdminPassword);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<AccessGroup> EnsureRoleAsync(
        AppDbContext db,
        string name,
        string description,
        string[] pages,
        CancellationToken cancellationToken)
    {
        var role = await db.AccessGroups.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new AccessGroup { Name = name, Description = description };
        foreach (var page in pages)
        {
            role.Pages.Add(new AccessGroupPage { PageId = page });
        }

        db.AccessGroups.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static async Task<Operator> SeedOperatorAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Operators.OrderBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.GovernmentIdType))
            {
                existing.GovernmentIdType = "Driver's License";
                if (string.IsNullOrWhiteSpace(existing.GovernmentId))
                {
                    existing.GovernmentId = "GOV-CEB-0001";
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var op = new Operator
        {
            CompanyName = "Cebu Pasakay",
            ContactName = "Maria Santos",
            ContactPhone = OperatorPhone,
            FullAddress = "123 Colon Street, Cebu City, Cebu",
            AreaOfOperation = "Cebu City, Mandaue, Lapu-Lapu",
            GovernmentIdType = "Driver's License",
            GovernmentId = "GOV-CEB-0001",
            IsActive = true,
            MotorcycleCommissionPercent = 10,
            TricycleCommissionPercent = 5
        };
        db.Operators.Add(op);
        db.Users.Add(new AppUser
        {
            PhoneNumber = OperatorPhone,
            FullName = "Maria Santos",
            PasswordHash = SecretHasher.Hash(OperatorPassword),
            Role = UserRole.Operator,
            OperatorId = op.Id,
            IsActive = true
        });
        await db.SaveChangesAsync(cancellationToken);
        return op;
    }

    private static async Task SeedOperatorPasswordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => x.Role == UserRole.Operator && (x.PasswordHash == null || x.PasswordHash == ""))
            .ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            user.PasswordHash = SecretHasher.Hash(OperatorPassword);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRidersAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.RiderProfiles.AnyAsync(x => x.OperatorId == op.Id, cancellationToken))
        {
            return;
        }

        var riders = new (string Name, string Phone, VehicleType Type, string Plate, string Model, string License)[]
        {
            ("Juan Dela Cruz", "09171110001", VehicleType.Motorcycle, "MC-1001", "Honda Click 125", "N01-11-100001"),
            ("Pedro Reyes", "09171110002", VehicleType.Motorcycle, "MC-1002", "Yamaha NMAX", "N01-11-100002"),
            ("Ana Villanueva", "09171110003", VehicleType.Tricycle, "TRI-2001", "TVS King", "N01-11-100003"),
            ("Lito Ramos", "09171110004", VehicleType.Tricycle, "TRI-2002", "Kawasaki CT100", "N01-11-100004")
        };

        foreach (var row in riders)
        {
            if (await db.Users.AnyAsync(x => x.PhoneNumber == row.Phone, cancellationToken))
            {
                continue;
            }

            var user = new AppUser
            {
                PhoneNumber = row.Phone,
                FullName = row.Name,
                PasswordHash = SecretHasher.Hash(RiderPassword),
                Role = UserRole.Rider,
                OperatorId = op.Id,
                IsActive = true
            };
            db.Users.Add(user);
            db.RiderProfiles.Add(new RiderProfile
            {
                AppUserId = user.Id,
                OperatorId = op.Id,
                VehicleType = row.Type,
                PlateNumber = row.Plate,
                VehicleModel = row.Model,
                LicenseType = "Driver's License",
                LicenseNumber = row.License,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderPasswordsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var riders = await db.Users
            .Where(x => x.Role == UserRole.Rider && (x.PasswordHash == null || x.PasswordHash == ""))
            .ToListAsync(cancellationToken);
        if (riders.Count == 0)
        {
            return;
        }

        foreach (var rider in riders)
        {
            rider.PasswordHash = SecretHasher.Hash(RiderPassword);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderPaymentMethodsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Include(x => x.PaymentMethods)
            .Where(x => x.PaymentMethods.Count == 0)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (riders.Count == 0)
        {
            return;
        }

        var presets = new[]
        {
            new[] { PaymentMethod.Cash, PaymentMethod.GCash },
            new[] { PaymentMethod.Cash, PaymentMethod.GCash, PaymentMethod.Maya },
            new[] { PaymentMethod.Cash, PaymentMethod.Maya },
            new[] { PaymentMethod.Cash, PaymentMethod.GCash, PaymentMethod.Maya, PaymentMethod.Other }
        };

        foreach (var rider in riders)
        {
            var methods = presets[riders.IndexOf(rider) % presets.Length];
            foreach (var method in methods)
            {
                db.RiderPaymentMethods.Add(new RiderPaymentMethod
                {
                    RiderId = rider.Id,
                    Method = method
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTripPaymentMethodsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Trips.AnyAsync(x => x.PaymentMethod != PaymentMethod.Cash, cancellationToken))
        {
            return;
        }

        var riders = await db.RiderProfiles
            .Include(x => x.PaymentMethods)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var trips = await db.Trips.ToListAsync(cancellationToken);
        foreach (var trip in trips)
        {
            if (!riders.TryGetValue(trip.RiderId, out var rider) || rider.PaymentMethods.Count == 0)
            {
                continue;
            }

            var methods = rider.PaymentMethods.Select(x => x.Method).OrderBy(x => x).ToList();
            var pick = methods[Math.Abs(trip.Reference.GetHashCode(StringComparison.Ordinal)) % methods.Count];
            trip.PaymentMethod = pick;
            trip.PaymentMethodOther = pick == PaymentMethod.Other ? "Bank transfer" : null;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderWalletsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Include(x => x.Wallet)
            .Where(x => x.Wallet == null)
            .ToListAsync(cancellationToken);
        foreach (var rider in riders)
        {
            db.RiderWallets.Add(new RiderWallet
            {
                RiderId = rider.Id,
                Balance = 500m
            });
        }

        if (riders.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedTripCommissionDeductionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.RiderWalletTransactions.AnyAsync(x => x.Kind == WalletTransactionKind.Commission, cancellationToken))
        {
            return;
        }

        var trips = await db.Trips
            .Where(x => x.Status == TripStatus.Completed)
            .OrderBy(x => x.CompletedAtUtc)
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return;
        }

        var fares = await db.FareMatrices
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var wallets = await db.RiderWallets.ToDictionaryAsync(x => x.RiderId, cancellationToken);

        foreach (var trip in trips)
        {
            if (!wallets.TryGetValue(trip.RiderId, out var wallet))
            {
                wallet = new RiderWallet { RiderId = trip.RiderId, Balance = 0 };
                db.RiderWallets.Add(wallet);
                await db.SaveChangesAsync(cancellationToken);
                wallets[trip.RiderId] = wallet;
            }

            var fare = fares.FirstOrDefault(x => x.OperatorId == trip.OperatorId && x.VehicleType == trip.VehicleType);
            var operatorPercent = fare?.OperatorCommissionPercent ?? FareCommissionSplit.DefaultOperatorShare;
            var amount = CommissionCut.Round(trip.Fare * operatorPercent / 100m);
            if (amount <= 0)
            {
                continue;
            }

            wallet.Balance = CommissionCut.Round(wallet.Balance - amount);
            wallet.UpdatedAtUtc = DateTime.UtcNow;
            db.RiderWalletTransactions.Add(new RiderWalletTransaction
            {
                WalletId = wallet.Id,
                RiderId = trip.RiderId,
                Kind = WalletTransactionKind.Commission,
                Status = WalletTransactionStatus.Approved,
                Amount = amount,
                BalanceAfter = wallet.Balance,
                TripId = trip.Id,
                Note = $"Operator commission ({operatorPercent:0.##}%) for {trip.Reference}",
                ResolvedAtUtc = trip.CompletedAtUtc ?? trip.RequestedAtUtc
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedWalletRequestsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.RiderWalletTransactions.AnyAsync(
                x => x.Status == WalletTransactionStatus.Pending,
                cancellationToken))
        {
            return;
        }

        var riders = await db.RiderProfiles
            .Include(x => x.Wallet)
            .Include(x => x.PaymentMethods)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (riders.Count == 0)
        {
            return;
        }

        var samples = new[]
        {
            (Kind: WalletTransactionKind.CashIn, Method: PaymentMethod.GCash, Amount: 300m, Note: "Top up via GCash ref 1234"),
            (Kind: WalletTransactionKind.CashOut, Method: PaymentMethod.Cash, Amount: 150m, Note: "Cash pickup at operator desk")
        };

        for (var i = 0; i < Math.Min(riders.Count, samples.Length); i++)
        {
            var rider = riders[i];
            if (rider.Wallet is null)
            {
                continue;
            }

            var sample = samples[i];
            if (!rider.PaymentMethods.Any(x => x.Method == sample.Method))
            {
                continue;
            }

            db.RiderWalletTransactions.Add(new RiderWalletTransaction
            {
                WalletId = rider.Wallet.Id,
                RiderId = rider.Id,
                Kind = sample.Kind,
                Status = WalletTransactionStatus.Pending,
                PaymentMethod = sample.Method,
                Amount = sample.Amount,
                Note = sample.Note
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderLocationsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Where(x => x.LastLat == null || x.LastLng == null)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (riders.Count == 0)
        {
            return;
        }

        var known = new Dictionary<string, (double Lat, double Lng)>(StringComparer.Ordinal)
        {
            ["09171110001"] = (10.3157, 123.8854),
            ["09171110002"] = (10.3274, 123.9068),
            ["09171110003"] = (10.2936, 123.9019),
            ["09171110004"] = (10.3390, 123.9170)
        };

        var index = 0;
        foreach (var rider in riders)
        {
            if (known.TryGetValue(rider.AppUser.PhoneNumber, out var pin))
            {
                rider.LastLat = pin.Lat;
                rider.LastLng = pin.Lng;
            }
            else
            {
                rider.LastLat = 10.3157 + (index * 0.006);
                rider.LastLng = 123.8854 + ((index % 4) * 0.008);
            }

            rider.LastLocationAtUtc = DateTime.UtcNow.AddMinutes(-(index + 1) * 2);
            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderLicensesAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Where(x => x.OperatorId == op.Id && (x.LicenseNumber == "" || x.LicenseType == ""))
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (riders.Count == 0)
        {
            return;
        }

        var index = 1;
        foreach (var rider in riders)
        {
            if (string.IsNullOrWhiteSpace(rider.LicenseType))
            {
                rider.LicenseType = "Driver's License";
            }

            if (string.IsNullOrWhiteSpace(rider.LicenseNumber))
            {
                rider.LicenseNumber = $"N01-11-{100000 + index:000000}";
            }

            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRiderAddressesAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var riders = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == op.Id && x.AddressBarangayId == null)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (riders.Count == 0)
        {
            return;
        }

        var cebuCity = await FindCebuMunicipalityAsync(db, "CEBU", cancellationToken);
        var mandaue = await FindCebuMunicipalityAsync(db, "MANDAUE", cancellationToken);
        if (cebuCity is null)
        {
            return;
        }

        var wanted = new (string MunicipalityKey, string Barangay, string Details)[]
        {
            ("CEBU", "Lahug", "12 Salinas Drive"),
            ("CEBU", "Guadalupe", "45 V. Rama Avenue"),
            ("CEBU", "Mabolo", "8 M.J. Cuenco Avenue"),
            ("MANDAUE", "Centro", "21 A.S. Fortuna Street")
        };

        var municipalityIds = new List<Guid> { cebuCity.Id };
        if (mandaue is not null)
        {
            municipalityIds.Add(mandaue.Id);
        }
        var barangays = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Where(x => municipalityIds.Contains(x.MunicipalityId))
            .ToListAsync(cancellationToken);

        for (var i = 0; i < riders.Count; i++)
        {
            var row = wanted[i % wanted.Length];
            var barangay = barangays.FirstOrDefault(x =>
                PhTerritoryCatalog.Normalize(x.Municipality.Name) == row.MunicipalityKey &&
                PhTerritoryCatalog.NormalizeBarangay(x.Name) == PhTerritoryCatalog.NormalizeBarangay(row.Barangay));
            if (barangay is null)
            {
                continue;
            }

            riders[i].AddressBarangayId = barangay.Id;
            riders[i].AddressDetails = row.Details;
            riders[i].FullAddress = OperatorAddressSync.Format(row.Details, barangay);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Municipality?> FindCebuMunicipalityAsync(
        AppDbContext db,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        var municipalities = await db.Municipalities
            .Include(x => x.Province)
            .Where(x => x.Province.Name == "Cebu")
            .ToListAsync(cancellationToken);
        return municipalities.FirstOrDefault(x => PhTerritoryCatalog.Normalize(x.Name) == normalizedName);
    }

    private static async Task SeedTripsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.Trips.AnyAsync(x => x.OperatorId == op.Id, cancellationToken))
        {
            return;
        }

        var riders = await db.RiderProfiles
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (riders.Count == 0)
        {
            return;
        }

        var stops = await LoadCebuStopsAsync(db, cancellationToken);
        if (stops.Count < 2)
        {
            return;
        }

        var profiles = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var customers = profiles.Count > 0
            ? profiles.Select(x => $"{x.FirstName} {x.LastName}".Trim()).ToArray()
            : ["Rico Tan", "May Flores", "Ben Cruz", "Lina Ong", "Carlo Lim", "Tess Uy"];
        var now = DateTime.UtcNow;
        var random = new Random(42);

        foreach (var rider in riders)
        {
            for (var i = 0; i < 12; i++)
            {
                var pickup = stops[random.Next(stops.Count)];
                var dropoff = stops[(stops.IndexOf(pickup) + 3 + i) % stops.Count];
                if (dropoff.BarangayId == pickup.BarangayId)
                {
                    dropoff = stops[(stops.IndexOf(pickup) + 1) % stops.Count];
                }
                var requested = now.Date.AddDays(-i).AddHours(8 + (i % 10)).AddMinutes(random.Next(0, 50));
                var status = i switch
                {
                    0 when rider == riders[0] => TripStatus.Ongoing,
                    1 => TripStatus.Pending,
                    2 => TripStatus.Waiting,
                    3 or 8 => TripStatus.Cancelled,
                    _ => TripStatus.Completed
                };
                var fare = 80m + random.Next(20, 180);
                var customerIndex = (i + riders.IndexOf(rider)) % customers.Length;
                var customerName = customers[customerIndex];
                var profile = profiles.Count > 0 ? profiles[customerIndex % profiles.Count] : null;
                var trip = new Trip
                {
                    OperatorId = op.Id,
                    RiderId = rider.Id,
                    VehicleType = rider.VehicleType,
                    Status = status,
                    Pickup = pickup.FullAddress,
                    PickupDetails = pickup.Details,
                    PickupBarangayId = pickup.BarangayId,
                    PickupLat = pickup.Lat,
                    PickupLng = pickup.Lng,
                    Dropoff = dropoff.FullAddress,
                    DropoffDetails = dropoff.Details,
                    DropoffBarangayId = dropoff.BarangayId,
                    DropoffLat = dropoff.Lat,
                    DropoffLng = dropoff.Lng,
                    CustomerId = profile?.Id,
                    CustomerName = customerName,
                    CustomerPhone = profile?.AppUser.PhoneNumber ?? $"0918{1000000 + random.Next(0, 8999999):0000000}",
                    Reference = $"YP{requested:yyyyMMdd}-{riders.IndexOf(rider) + 1:00}{i + 1:00}",
                    Notes = i % 4 == 0 ? "Gate is the blue one beside the sari-sari store" : null,
                    Fare = fare,
                    DistanceKm = Math.Round(2.4m + (decimal)random.NextDouble() * 8m, 1),
                    RequestedAtUtc = requested,
                    CompletedAtUtc = status == TripStatus.Completed ? requested.AddMinutes(12 + random.Next(8, 35)) : null,
                    CancelledAtUtc = status == TripStatus.Cancelled ? requested.AddMinutes(3 + random.Next(2, 12)) : null,
                    CancelReason = status == TripStatus.Cancelled
                        ? (i % 2 == 0 ? "Customer cancelled before pickup" : "No rider available nearby")
                        : null
                };
                ApplySeedRating(trip, i, random);
                db.Trips.Add(trip);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedBookingPipelineAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var hasPending = await db.Trips.AnyAsync(x => x.OperatorId == op.Id && x.Status == TripStatus.Pending, cancellationToken);
        var hasWaiting = await db.Trips.AnyAsync(x => x.OperatorId == op.Id && x.Status == TripStatus.Waiting, cancellationToken);
        if (hasPending && hasWaiting)
        {
            return;
        }

        var riders = await db.RiderProfiles
            .Where(x => x.OperatorId == op.Id && x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (riders.Count == 0)
        {
            return;
        }

        var stops = await LoadCebuStopsAsync(db, cancellationToken);
        if (stops.Count < 2)
        {
            return;
        }

        var profiles = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var needed = new List<(TripStatus Status, int Count)>();
        if (!hasPending)
        {
            needed.Add((TripStatus.Pending, 3));
        }
        if (!hasWaiting)
        {
            needed.Add((TripStatus.Waiting, 3));
        }

        var index = 0;
        foreach (var (status, count) in needed)
        {
            for (var i = 0; i < count; i++)
            {
                var rider = riders[index % riders.Count];
                var pickup = stops[index % stops.Count];
                var dropoff = stops[(index + 2) % stops.Count];
                var profile = profiles.Count > 0 ? profiles[index % profiles.Count] : null;
                var requested = now.AddMinutes(-(8 + i * 6));
                db.Trips.Add(new Trip
                {
                    OperatorId = op.Id,
                    RiderId = rider.Id,
                    VehicleType = rider.VehicleType,
                    Status = status,
                    Pickup = pickup.FullAddress,
                    PickupDetails = pickup.Details,
                    PickupBarangayId = pickup.BarangayId,
                    Dropoff = dropoff.FullAddress,
                    DropoffDetails = dropoff.Details,
                    DropoffBarangayId = dropoff.BarangayId,
                    CustomerId = profile?.Id,
                    CustomerName = profile is null ? "Walk-in customer" : $"{profile.FirstName} {profile.LastName}".Trim(),
                    CustomerPhone = profile?.AppUser.PhoneNumber ?? $"0918{2000000 + index:0000000}",
                    Reference = $"YP{requested:yyyyMMdd}-D{status.ToString()[0]}{i + 1:00}",
                    Fare = 95m + (i * 15),
                    DistanceKm = 3.2m + i,
                    RequestedAtUtc = requested
                });
                index++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedScheduledBookingsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.Trips.AnyAsync(x => x.OperatorId == op.Id && x.ScheduledAtUtc != null, cancellationToken))
        {
            return;
        }

        var riders = await db.RiderProfiles
            .Where(x => x.OperatorId == op.Id && x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var stops = await LoadCebuStopsAsync(db, cancellationToken);
        var profiles = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (riders.Count == 0 || stops.Count < 2)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var slots = new[] { 3, 8, 26 };
        for (var i = 0; i < slots.Length; i++)
        {
            var rider = riders[i % riders.Count];
            var pickup = stops[i % stops.Count];
            var dropoff = stops[(i + 2) % stops.Count];
            var profile = profiles.Count > 0 ? profiles[i % profiles.Count] : null;
            var scheduled = now.AddHours(slots[i]);
            db.Trips.Add(new Trip
            {
                OperatorId = op.Id,
                RiderId = rider.Id,
                VehicleType = rider.VehicleType,
                Status = TripStatus.Pending,
                Pickup = pickup.FullAddress,
                PickupDetails = pickup.Details,
                PickupBarangayId = pickup.BarangayId,
                Dropoff = dropoff.FullAddress,
                DropoffDetails = dropoff.Details,
                DropoffBarangayId = dropoff.BarangayId,
                CustomerId = profile?.Id,
                CustomerName = profile is null ? "Scheduled customer" : $"{profile.FirstName} {profile.LastName}".Trim(),
                CustomerPhone = profile?.AppUser.PhoneNumber ?? $"0918{3100000 + i:0000000}",
                Reference = $"YP{scheduled:yyyyMMdd}-S{i + 1:00}",
                Notes = "Customer set this scheduled booking for the rider.",
                Fare = 110m + (i * 20),
                DistanceKm = 4.5m + i,
                RequestedAtUtc = now,
                ScheduledAtUtc = scheduled
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static readonly string[] CustomerAppPhones = ["09181110001", "09181110003", "09181110004"];

    private static async Task ReleaseCustomerAppTripsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var live = await db.Trips
            .Where(x => CustomerAppPhones.Contains(x.CustomerPhone)
                && (x.Status == TripStatus.Pending || x.Status == TripStatus.Waiting || x.Status == TripStatus.Ongoing))
            .ToListAsync(cancellationToken);
        if (live.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var trip in live)
        {
            if (trip.Status == TripStatus.Ongoing)
            {
                trip.Status = TripStatus.Completed;
                trip.CompletedAtUtc = now;
            }
            else
            {
                trip.Status = TripStatus.Cancelled;
                trip.CancelledAtUtc = now;
                trip.CancelReason = "Released so the customer app can book a new ride.";
            }

            trip.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTripBookingDetailsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id && (x.Reference == "" || x.CustomerPhone == ""))
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        if (trips.Count == 0)
        {
            return;
        }

        var index = 1;
        foreach (var trip in trips)
        {
            if (string.IsNullOrWhiteSpace(trip.Reference))
            {
                trip.Reference = $"YP{trip.RequestedAtUtc:yyyyMMdd}-{index:0000}";
            }

            if (string.IsNullOrWhiteSpace(trip.CustomerPhone))
            {
                trip.CustomerPhone = $"0918{1000000 + index:0000000}";
            }

            if (trip.Status == TripStatus.Cancelled && trip.CancelledAtUtc is null)
            {
                trip.CancelledAtUtc = trip.RequestedAtUtc.AddMinutes(4);
                trip.CancelReason ??= "Customer cancelled before pickup";
            }

            if (index % 5 == 0 && string.IsNullOrWhiteSpace(trip.Notes))
            {
                trip.Notes = "Near the 7-Eleven / landmark";
            }

            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedStop(Guid BarangayId, string Details, string FullAddress, double Lat, double Lng);

    private static readonly (string MunicipalityKey, string Barangay, string Details, double Lat, double Lng)[] StopCatalog =
    [
        ("CEBU", "Lahug", "12 Salinas Drive, near Ayala Center Cebu", 10.3231, 123.8994),
        ("CEBU", "Guadalupe", "45 V. Rama Avenue, beside Cebu Doctors University", 10.3108, 123.8936),
        ("CEBU", "Mabolo", "SM City Cebu, Juan Luna Avenue", 10.3119, 123.9224),
        ("CEBU", "Cogon Ramos", "123 Colon Street, front of the old cinema", 10.2966, 123.8984),
        ("CEBU", "Banilad", "Gov. M. Cuenco Avenue, across Gaisano Country Mall", 10.3389, 123.9047),
        ("CEBU", "Talamban", "Nasipit Road, beside the barangay hall", 10.3524, 123.9210),
        ("CEBU", "Apas", "IT Park, Jose Maria del Mar Street, Building A lobby", 10.3292, 123.9055),
        ("MANDAUE", "Centro", "21 A.S. Fortuna Street, Mandaue Poblacion", 10.3238, 123.9228),
        ("LAPU LAPU", "Poblacion", "M.L. Quezon National Highway, near Gaisano Grand Mactan", 10.3103, 123.9494)
    ];

    private static async Task<List<SeedStop>> LoadCebuStopsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var municipalities = await db.Municipalities
            .Include(x => x.Province)
            .Where(x => x.Province.Name == "Cebu")
            .ToListAsync(cancellationToken);
        var municipalityIds = municipalities.Select(x => x.Id).ToList();
        var barangays = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Where(x => municipalityIds.Contains(x.MunicipalityId))
            .ToListAsync(cancellationToken);

        var stops = new List<SeedStop>();
        foreach (var row in StopCatalog)
        {
            var barangay = barangays.FirstOrDefault(x =>
                PhTerritoryCatalog.Normalize(x.Municipality.Name) == row.MunicipalityKey &&
                PhTerritoryCatalog.NormalizeBarangay(x.Name) == PhTerritoryCatalog.NormalizeBarangay(row.Barangay));
            if (barangay is null)
            {
                continue;
            }

            stops.Add(new SeedStop(barangay.Id, row.Details, OperatorAddressSync.Format(row.Details, barangay), row.Lat, row.Lng));
        }

        return stops;
    }

    private static async Task SeedTripStopsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id && (x.PickupDetails == "" || !x.Pickup.Contains(",")))
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return;
        }

        var stops = await LoadCebuStopsAsync(db, cancellationToken);
        if (stops.Count < 2)
        {
            return;
        }

        var index = 0;
        foreach (var trip in trips)
        {
            var pickup = stops[index % stops.Count];
            var dropoff = stops[(index + 3) % stops.Count];
            if (dropoff.BarangayId == pickup.BarangayId)
            {
                dropoff = stops[(index + 1) % stops.Count];
            }

            trip.PickupDetails = pickup.Details;
            trip.PickupBarangayId = pickup.BarangayId;
            trip.Pickup = pickup.FullAddress;
            trip.PickupLat = pickup.Lat;
            trip.PickupLng = pickup.Lng;
            trip.DropoffDetails = dropoff.Details;
            trip.DropoffBarangayId = dropoff.BarangayId;
            trip.Dropoff = dropoff.FullAddress;
            trip.DropoffLat = dropoff.Lat;
            trip.DropoffLng = dropoff.Lng;
            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTripCoordinatesAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id && (x.PickupLat == null || x.DropoffLat == null))
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return;
        }

        var stops = await LoadCebuStopsAsync(db, cancellationToken);
        if (stops.Count < 2)
        {
            return;
        }

        var index = 0;
        foreach (var trip in trips)
        {
            var pickup = stops.FirstOrDefault(x => x.Details == trip.PickupDetails) ?? stops[index % stops.Count];
            var dropoff = stops.FirstOrDefault(x => x.Details == trip.DropoffDetails)
                ?? stops[(index + 3) % stops.Count];
            trip.PickupLat ??= pickup.Lat;
            trip.PickupLng ??= pickup.Lng;
            trip.DropoffLat ??= dropoff.Lat;
            trip.DropoffLng ??= dropoff.Lng;
            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTripRatingsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.Trips.AnyAsync(x => x.OperatorId == op.Id && x.Rating != null, cancellationToken))
        {
            return;
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.Status == TripStatus.Completed)
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return;
        }

        var random = new Random(91);
        for (var i = 0; i < trips.Count; i++)
        {
            ApplySeedRating(trips[i], i, random);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ApplySeedRating(Trip trip, int index, Random random)
    {
        if (trip.Status != TripStatus.Completed || trip.CompletedAtUtc is null || trip.Rating is not null)
        {
            return;
        }

        if (index % 5 == 0)
        {
            return;
        }

        var scores = new[] { 5, 5, 4, 5, 3, 4, 5, 2, 5, 4 };
        var comments = new[]
        {
            "Safe ride and on time",
            "Rider was polite and knew the area",
            null,
            "Helmet was clean",
            "Took a longer route than expected",
            "Very smooth trip",
            null,
            "Bike was a bit noisy",
            "Would book again",
            "Dropped me at the exact gate"
        };

        trip.Rating = scores[index % scores.Length];
        trip.RatingComment = comments[index % comments.Length];
        trip.RatedAtUtc = trip.CompletedAtUtc.Value.AddMinutes(2 + random.Next(3, 40));
    }

    private static async Task SeedTripChatsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.TripChatMessages.AnyAsync(cancellationToken))
        {
            return;
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        for (var i = 0; i < trips.Count; i++)
        {
            if (i % 6 == 0)
            {
                continue;
            }

            foreach (var message in BuildSeedChat(trips[i], i))
            {
                db.TripChatMessages.Add(message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<TripChatMessage> BuildSeedChat(Trip trip, int index)
    {
        var start = trip.RequestedAtUtc.AddMinutes(1);
        var lines = trip.Status switch
        {
            TripStatus.Cancelled => new (ChatSender Sender, string Body, int Minutes)[]
            {
                (ChatSender.Customer, "Hi, how long po to pickup?", 1),
                (ChatSender.Rider, "About 8 minutes. Traffic sa Colon.", 3),
                (ChatSender.Customer, "Sorry, cancel na. Something came up.", 6)
            },
            TripStatus.Ongoing => new (ChatSender Sender, string Body, int Minutes)[]
            {
                (ChatSender.Customer, "I'm at the pickup now, blue gate.", 1),
                (ChatSender.Rider, "Noted. 2 minutes away, white motorcycle.", 3),
                (ChatSender.Customer, "Ok, I'll wait outside.", 4)
            },
            _ => index % 2 == 0
                ? new (ChatSender Sender, string Body, int Minutes)[]
                {
                    (ChatSender.Customer, "Hi, I'm waiting at the lobby.", 1),
                    (ChatSender.Rider, "On the way. I'll message when I'm at the gate.", 3),
                    (ChatSender.Customer, "Ok, I'll go out now.", 7),
                    (ChatSender.Rider, "I'm here. White motorcycle.", 9),
                    (ChatSender.Customer, "Thanks, smooth ride.", 18)
                }
                : new (ChatSender Sender, string Body, int Minutes)[]
                {
                    (ChatSender.Rider, "Heading to your pickup now.", 1),
                    (ChatSender.Customer, "Please drop me at the exact gate, not the corner.", 2),
                    (ChatSender.Rider, "Copy. 4 minutes away.", 4),
                    (ChatSender.Customer, "Thank you po.", 16)
                }
        };

        return lines.Select(line => new TripChatMessage
        {
            TripId = trip.Id,
            Sender = line.Sender,
            Body = line.Body,
            SentAtUtc = start.AddMinutes(line.Minutes)
        });
    }

    private static readonly (string First, string Last, string Phone, DeleteAccountStatus Delete, string? Reason)[] CustomerCatalog =
    [
        ("Rico", "Tan", "09181110001", DeleteAccountStatus.None, null),
        ("May", "Flores", "09181110002", DeleteAccountStatus.Pending, "I no longer use the app."),
        ("Ben", "Cruz", "09181110003", DeleteAccountStatus.None, null),
        ("Lina", "Ong", "09181110004", DeleteAccountStatus.None, null),
        ("Carlo", "Lim", "09181110005", DeleteAccountStatus.Approved, "Requested deletion after moving abroad."),
        ("Tess", "Uy", "09181110006", DeleteAccountStatus.Rejected, "Opened by mistake.")
    ];

    private static async Task SeedCustomersAsync(AppDbContext db, string? uploadRoot, CancellationToken cancellationToken)
    {
        if (await db.CustomerProfiles.AnyAsync(cancellationToken))
        {
            var existing = await db.CustomerProfiles.Include(x => x.AppUser).ToListAsync(cancellationToken);
            foreach (var customer in existing)
            {
                if (string.IsNullOrWhiteSpace(customer.FirstName) && string.IsNullOrWhiteSpace(customer.LastName))
                {
                    var parts = (customer.AppUser.FullName ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    customer.FirstName = parts.ElementAtOrDefault(0) ?? "";
                    customer.LastName = parts.ElementAtOrDefault(1) ?? "";
                }

                customer.PhotoPath ??= WriteInitialAvatar(uploadRoot, customer.Id, customer.FirstName, customer.LastName);
                if (string.IsNullOrWhiteSpace(customer.AppUser.PasswordHash))
                {
                    customer.AppUser.PasswordHash = SecretHasher.Hash("1234");
                }

                if (string.IsNullOrWhiteSpace(customer.AppUser.Email))
                {
                    customer.AppUser.Email = $"{customer.FirstName}.{customer.LastName}@yapasakay.test"
                        .ToLowerInvariant()
                        .Replace(" ", "");
                }

                customer.Gender ??= Gender.Male;
            }

            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        for (var i = 0; i < CustomerCatalog.Length; i++)
        {
            var (first, last, phone, delete, reason) = CustomerCatalog[i];
            var created = now.AddDays(-20 + i);
            var user = new AppUser
            {
                PhoneNumber = phone,
                FullName = $"{first} {last}",
                Email = $"{first}.{last}@yapasakay.test".ToLowerInvariant(),
                PasswordHash = SecretHasher.Hash("1234"),
                Role = UserRole.Customer,
                IsActive = delete != DeleteAccountStatus.Approved,
                CreatedAtUtc = created
            };
            var customer = new CustomerProfile
            {
                AppUser = user,
                FirstName = first,
                LastName = last,
                Gender = i % 2 == 0 ? Gender.Male : Gender.Female,
                DeleteStatus = delete,
                DeleteRequestReason = reason,
                CreatedAtUtc = created
            };
            if (delete == DeleteAccountStatus.Pending)
            {
                customer.DeleteRequestedAtUtc = now.AddDays(-1);
            }
            else if (delete == DeleteAccountStatus.Approved)
            {
                customer.DeleteRequestedAtUtc = now.AddDays(-6);
                customer.DeleteResolvedAtUtc = now.AddDays(-5);
                customer.DeleteResolutionNote = "Account deactivated on customer request.";
            }
            else if (delete == DeleteAccountStatus.Rejected)
            {
                customer.DeleteRequestedAtUtc = now.AddDays(-4);
                customer.DeleteResolvedAtUtc = now.AddDays(-3);
                customer.DeleteResolutionNote = "Customer still takes trips. Request declined.";
            }

            db.Users.Add(user);
            db.CustomerProfiles.Add(customer);
            await db.SaveChangesAsync(cancellationToken);
            customer.PhotoPath = WriteInitialAvatar(uploadRoot, customer.Id, first, last);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedTripCustomersAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var customers = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        if (customers.Count == 0)
        {
            return;
        }

        var trips = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.CustomerId == null)
            .OrderBy(x => x.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        if (trips.Count == 0)
        {
            return;
        }

        var index = 0;
        foreach (var trip in trips)
        {
            var match = customers.FirstOrDefault(x =>
                string.Equals($"{x.FirstName} {x.LastName}".Trim(), trip.CustomerName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.AppUser.FullName, trip.CustomerName, StringComparison.OrdinalIgnoreCase));
            match ??= customers[index % customers.Count];
            trip.CustomerId = match.Id;
            trip.CustomerName = $"{match.FirstName} {match.LastName}".Trim();
            trip.CustomerPhone = match.AppUser.PhoneNumber;
            index++;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAccountDeleteAlertsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.AdminNotifications.AnyAsync(x => x.Kind == NotificationKind.AccountDelete, cancellationToken))
        {
            return;
        }

        var pending = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .Where(x => x.DeleteStatus == DeleteAccountStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var customer in pending)
        {
            await CustomerDeleteAlerts.NotifyAsync(db, customer, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? WriteInitialAvatar(string? uploadRoot, Guid customerId, string first, string last)
    {
        if (string.IsNullOrWhiteSpace(uploadRoot))
        {
            return null;
        }

        var relative = $"customers/{customerId:D}/profile.svg";
        var full = Path.Combine(uploadRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var initials = $"{(first.Length > 0 ? first[0] : ' ')}{(last.Length > 0 ? last[0] : ' ')}".Trim().ToUpperInvariant();
        if (initials.Length == 0)
        {
            initials = "C";
        }

        var colors = new[] { "#E30613", "#1b8a5a", "#2b6cb0", "#b45309", "#6d28d9", "#0f766e" };
        var color = colors[Math.Abs(customerId.GetHashCode()) % colors.Length];
        var svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"256\" height=\"256\" viewBox=\"0 0 256 256\">" +
            $"<rect width=\"256\" height=\"256\" rx=\"128\" fill=\"{color}\"/>" +
            $"<text x=\"50%\" y=\"54%\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-family=\"Segoe UI, sans-serif\" font-size=\"96\" font-weight=\"700\" fill=\"#fff\">{initials}</text>" +
            "</svg>";
        File.WriteAllText(full, svg);
        return relative.Replace('\\', '/');
    }

    private static async Task SeedFareMatricesAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.FareMatrices.AnyAsync(x => x.OperatorId == op.Id, cancellationToken))
        {
            return;
        }

        db.FareMatrices.AddRange(
            new FareMatrix
            {
                OperatorId = op.Id,
                VehicleType = VehicleType.Motorcycle,
                BaseFare = 40m,
                PerKm = 12m,
                MinimumFare = 40m,
                IncludedKm = 1m,
                OperatorCommissionPercent = FareCommissionSplit.Defaults(op.MotorcycleCommissionPercent).Operator,
                DriverCommissionPercent = FareCommissionSplit.Defaults(op.MotorcycleCommissionPercent).Driver,
                IsActive = true
            },
            new FareMatrix
            {
                OperatorId = op.Id,
                VehicleType = VehicleType.Tricycle,
                BaseFare = 50m,
                PerKm = 15m,
                MinimumFare = 50m,
                IncludedKm = 1m,
                OperatorCommissionPercent = FareCommissionSplit.Defaults(op.TricycleCommissionPercent).Operator,
                DriverCommissionPercent = FareCommissionSplit.Defaults(op.TricycleCommissionPercent).Driver,
                IsActive = true
            });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedFareCommissionSplitsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var fares = await db.FareMatrices
            .Where(x => x.OperatorId == op.Id)
            .ToListAsync(cancellationToken);
        foreach (var fare in fares)
        {
            var system = FareCommissionSplit.SystemPercent(op, fare.VehicleType);
            if (Math.Abs(system + fare.OperatorCommissionPercent + fare.DriverCommissionPercent - 100m) <= 0.01m)
            {
                continue;
            }

            FareCommissionSplit.ApplyDefaults(fare, system);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedFareSurchargesAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        var fares = await db.FareMatrices
            .Where(x => x.OperatorId == op.Id)
            .Select(x => new { x.Id, x.VehicleType })
            .ToListAsync(cancellationToken);

        foreach (var fare in fares)
        {
            if (await db.FareSurcharges.AnyAsync(x => x.FareMatrixId == fare.Id, cancellationToken))
            {
                continue;
            }

            var evening = fare.VehicleType == VehicleType.Motorcycle ? 50m : 60m;
            var holiday = fare.VehicleType == VehicleType.Motorcycle ? 100m : 120m;
            db.FareSurcharges.Add(new FareSurcharge
            {
                FareMatrixId = fare.Id,
                Kind = SurchargeKind.TimeWindow,
                Name = "Evening",
                Amount = evening,
                WindowStart = new TimeOnly(21, 0),
                WindowEnd = new TimeOnly(23, 0),
                IsActive = true
            });
            db.FareSurcharges.Add(new FareSurcharge
            {
                FareMatrixId = fare.Id,
                Kind = SurchargeKind.DateRange,
                Name = "Holiday",
                Amount = holiday,
                RangeStartUtc = PhilippineTime.ToUtc(2026, 12, 23, 0, 0),
                RangeEndUtc = PhilippineTime.ToUtc(2026, 12, 27, 23, 59),
                IsActive = true
            });
            db.FareSurcharges.Add(new FareSurcharge
            {
                FareMatrixId = fare.Id,
                Kind = SurchargeKind.TimeWindow,
                Name = "Rain",
                Amount = fare.VehicleType == VehicleType.Motorcycle ? 20m : 25m,
                WindowStart = new TimeOnly(0, 0),
                WindowEnd = new TimeOnly(23, 59),
                IsActive = false
            });
        }

        foreach (var fare in fares)
        {
            if (await db.FareSurcharges.AnyAsync(x => x.FareMatrixId == fare.Id && x.Name == "Rain", cancellationToken))
            {
                continue;
            }

            db.FareSurcharges.Add(new FareSurcharge
            {
                FareMatrixId = fare.Id,
                Kind = SurchargeKind.TimeWindow,
                Name = "Rain",
                Amount = fare.VehicleType == VehicleType.Motorcycle ? 20m : 25m,
                WindowStart = new TimeOnly(0, 0),
                WindowEnd = new TimeOnly(23, 59),
                IsActive = false
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAnnouncementsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Announcements.AnyAsync(cancellationToken))
        {
            return;
        }

        const string title = "Evening surcharge window";
        const string body = "A ₱50 evening surcharge applies from 9:00 PM to 11:00 PM, Philippine time. Holiday surcharge of ₱100 applies from Dec 23, 2026 12:00 AM to Dec 27, 2026 11:59 PM.";

        db.Announcements.Add(new Announcement
        {
            Title = title,
            Body = body,
            ForOperators = true,
            ForRiders = true,
            ForCustomers = true,
            StartsAtUtc = DateTime.UtcNow,
            IsActive = true
        });

        var operatorIds = await db.Operators.Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var operatorId in operatorIds)
        {
            db.OperatorNotifications.Add(new OperatorNotification
            {
                OperatorId = operatorId,
                Kind = NotificationKind.Announcement,
                Title = title,
                Body = body
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSupportTicketsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.SupportTickets.AnyAsync(cancellationToken))
        {
            return;
        }

        var rider = await db.RiderProfiles
            .Include(x => x.AppUser)
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var may = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.FirstName == "May", cancellationToken) ?? customer;

        var ongoing = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.Status == TripStatus.Ongoing)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var cancelled = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.Status == TripStatus.Cancelled)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var completed = await db.Trips
            .Where(x => x.OperatorId == op.Id && x.Status == TripStatus.Completed)
            .OrderByDescending(x => x.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;

        if (ongoing is not null && rider is not null)
        {
            db.SupportTickets.Add(new SupportTicket
            {
                OperatorId = op.Id,
                TripId = ongoing.Id,
                RiderId = rider.Id,
                Kind = SupportKind.Sos,
                Status = SupportStatus.Open,
                OpenedBy = SupportOpenedBy.Rider,
                Subject = "SOS",
                Body = "Rider pressed SOS during an ongoing trip. Operator is responsible for contacting the rider and local help in this municipality.",
                SosLat = rider.LastLat,
                SosLng = rider.LastLng,
                SosAtUtc = rider.LastLocationAtUtc ?? now.AddMinutes(-18),
                CreatedAtUtc = now.AddMinutes(-18)
            });
        }

        if (cancelled is not null && may is not null)
        {
            db.SupportTickets.Add(new SupportTicket
            {
                OperatorId = op.Id,
                TripId = cancelled.Id,
                CustomerId = may.Id,
                Kind = SupportKind.Support,
                Status = SupportStatus.Open,
                OpenedBy = SupportOpenedBy.Customer,
                Subject = "Cancelled booking",
                Body = "Customer asked why the booking was cancelled and whether another rider can be sent. Operator handles dispatch in this municipality.",
                CreatedAtUtc = now.AddHours(-3)
            });
        }

        if (completed is not null && customer is not null)
        {
            db.SupportTickets.Add(new SupportTicket
            {
                OperatorId = op.Id,
                TripId = completed.Id,
                CustomerId = customer.Id,
                Kind = SupportKind.Support,
                Status = SupportStatus.Closed,
                OpenedBy = SupportOpenedBy.Customer,
                Subject = "Left an item in the vehicle",
                Body = "Customer left a tote bag on the passenger footrest and asked the Operator to recover it.",
                OperatorNotes = "Called the rider. Bag is at the Cebu City terminal. Customer was told to pick it up today.",
                ClosedAtUtc = now.AddHours(-6),
                CreatedAtUtc = now.AddHours(-9)
            });
        }

        if (rider is not null)
        {
            db.SupportTickets.Add(new SupportTicket
            {
                OperatorId = op.Id,
                RiderId = rider.Id,
                Kind = SupportKind.Support,
                Status = SupportStatus.Closed,
                OpenedBy = SupportOpenedBy.Rider,
                Subject = "Plate number update",
                Body = "Rider asked the Operator to update the displayed plate after a replacement.",
                OperatorNotes = "Plate updated in the rider profile. Rider confirmed the new plate on the next trip.",
                ClosedAtUtc = now.AddDays(-1),
                CreatedAtUtc = now.AddDays(-2).AddHours(-4)
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSosTicketLocationsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var tickets = await db.SupportTickets
            .Include(x => x.Rider)
            .Include(x => x.Trip)
                .ThenInclude(x => x!.Rider)
            .Where(x => x.Kind == SupportKind.Sos && (x.SosLat == null || x.SosLng == null))
            .ToListAsync(cancellationToken);
        if (tickets.Count == 0)
        {
            return;
        }

        foreach (var ticket in tickets)
        {
            var rider = ticket.Trip?.Rider ?? ticket.Rider;
            if (rider?.LastLat is not null && rider.LastLng is not null)
            {
                ticket.SosLat = rider.LastLat;
                ticket.SosLng = rider.LastLng;
                ticket.SosAtUtc ??= ticket.CreatedAtUtc;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedAdminSosAlertsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var openSos = await db.SupportTickets
            .Where(x => x.Kind == SupportKind.Sos && x.Status == SupportStatus.Open)
            .ToListAsync(cancellationToken);
        foreach (var ticket in openSos)
        {
            await SosAlerts.EnsureAdminAlertAsync(db, ticket, cancellationToken);
        }
    }

    private static async Task SeedAuditLogsAsync(AppDbContext db, Operator op, CancellationToken cancellationToken)
    {
        if (await db.AuditLogs.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(x => x.Role == UserRole.Admin, cancellationToken);
        var bills = await db.OperatorBills
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            OperatorId = op.Id,
            ActorUserId = admin?.Id,
            Action = AuditAction.OperatorCreated,
            Summary = $"Created Operator {op.CompanyName}.",
            CreatedAtUtc = op.CreatedAtUtc
        });
        db.AuditLogs.Add(new AuditLog
        {
            OperatorId = op.Id,
            ActorUserId = admin?.Id,
            Action = AuditAction.OperatorUpdated,
            Summary = $"Updated Operator {op.CompanyName}. Motorcycle commission 10%, tricycle commission 5%.",
            CreatedAtUtc = op.CreatedAtUtc.AddMinutes(12)
        });

        foreach (var bill in bills)
        {
            db.AuditLogs.Add(new AuditLog
            {
                OperatorId = op.Id,
                ActorUserId = admin?.Id,
                Action = AuditAction.BillIssued,
                Summary = bill.DisabledOperator
                    ? $"Issued billing record {bill.Number} for ₱{bill.Amount:0.00} covering {bill.TripCount} trip(s) and disabled Operator {op.CompanyName}."
                    : $"Issued billing record {bill.Number} for ₱{bill.Amount:0.00} covering {bill.TripCount} trip(s) for {op.CompanyName}.",
                CreatedAtUtc = bill.CreatedAtUtc
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
