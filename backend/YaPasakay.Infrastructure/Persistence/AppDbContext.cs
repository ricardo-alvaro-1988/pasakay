using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;

namespace YaPasakay.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<RiderProfile> RiderProfiles => Set<RiderProfile>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Barangay> Barangays => Set<Barangay>();
    public DbSet<OperatorBarangay> OperatorBarangays => Set<OperatorBarangay>();
    public DbSet<RiderPaymentMethod> RiderPaymentMethods => Set<RiderPaymentMethod>();
    public DbSet<RiderWallet> RiderWallets => Set<RiderWallet>();
    public DbSet<RiderWalletTransaction> RiderWalletTransactions => Set<RiderWalletTransaction>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripOffer> TripOffers => Set<TripOffer>();
    public DbSet<TripChatMessage> TripChatMessages => Set<TripChatMessage>();
    public DbSet<FareMatrix> FareMatrices => Set<FareMatrix>();
    public DbSet<FareSurcharge> FareSurcharges => Set<FareSurcharge>();
    public DbSet<OperatorBill> OperatorBills => Set<OperatorBill>();
    public DbSet<OperatorNotification> OperatorNotifications => Set<OperatorNotification>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccessGroup> AccessGroups => Set<AccessGroup>();
    public DbSet<AccessGroupPage> AccessGroupPages => Set<AccessGroupPage>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(x => x.PhoneNumber).IsUnique();
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.GoogleSubject).IsUnique().HasFilter("[GoogleSubject] IS NOT NULL");
            entity.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(160);
            entity.Property(x => x.GoogleSubject).HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasMaxLength(200);
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AccessGroup)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.AccessGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Operator>(entity =>
        {
            entity.HasIndex(x => x.ContactPhone).IsUnique();
            entity.Property(x => x.CompanyName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.ContactPhone).HasMaxLength(20).IsRequired();
            entity.Property(x => x.FullAddress).HasMaxLength(400);
            entity.Property(x => x.AddressDetails).HasMaxLength(200);
            entity.Property(x => x.AreaOfOperation).HasMaxLength(400);
            entity.Property(x => x.GovernmentIdType).HasMaxLength(60);
            entity.Property(x => x.GovernmentId).HasMaxLength(80);
            entity.Property(x => x.ProfilePhotoPath).HasMaxLength(260);
            entity.Property(x => x.GovernmentIdPhotoPath).HasMaxLength(260);
            entity.Property(x => x.MotorcycleCommissionPercent).HasColumnType("decimal(5,2)").HasDefaultValue(10m);
            entity.Property(x => x.TricycleCommissionPercent).HasColumnType("decimal(5,2)").HasDefaultValue(5m);
            entity.HasOne(x => x.AddressBarangay)
                .WithMany()
                .HasForeignKey(x => x.AddressBarangayId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RiderProfile>(entity =>
        {
            entity.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
            entity.Property(x => x.VehicleModel).HasMaxLength(80);
            entity.Property(x => x.LicenseType).HasMaxLength(60);
            entity.Property(x => x.LicenseNumber).HasMaxLength(80);
            entity.Property(x => x.ProfilePhotoPath).HasMaxLength(260);
            entity.Property(x => x.LicensePhotoPath).HasMaxLength(260);
            entity.Property(x => x.AddressDetails).HasMaxLength(200);
            entity.Property(x => x.FullAddress).HasMaxLength(400);
            entity.HasOne(x => x.AddressBarangay)
                .WithMany()
                .HasForeignKey(x => x.AddressBarangayId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AppUser)
                .WithMany()
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.Riders)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.PaymentMethods)
                .WithOne(x => x.Rider)
                .HasForeignKey(x => x.RiderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Offers)
                .WithOne(x => x.Rider)
                .HasForeignKey(x => x.RiderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TripOffer>(entity =>
        {
            entity.HasIndex(x => new { x.TripId, x.RiderId }).IsUnique();
            entity.HasIndex(x => new { x.RiderId, x.Status, x.ExpiresAtUtc });
            entity.Property(x => x.DistanceKm).HasColumnType("decimal(8,2)");
            entity.HasOne(x => x.Trip)
                .WithMany(x => x.Offers)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RiderPaymentMethod>(entity =>
        {
            entity.HasIndex(x => new { x.RiderId, x.Method }).IsUnique();
        });

        modelBuilder.Entity<RiderWallet>(entity =>
        {
            entity.HasIndex(x => x.RiderId).IsUnique();
            entity.Property(x => x.Balance).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.Rider)
                .WithOne(x => x.Wallet)
                .HasForeignKey<RiderWallet>(x => x.RiderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RiderWalletTransaction>(entity =>
        {
            entity.HasIndex(x => new { x.RiderId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.Status, x.Kind });
            entity.HasIndex(x => x.TripId).IsUnique().HasFilter("[TripId] IS NOT NULL");
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(200);
            entity.Property(x => x.RejectionReason).HasMaxLength(200);
            entity.HasOne(x => x.Wallet)
                .WithMany(x => x.Transactions)
                .HasForeignKey(x => x.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Rider)
                .WithMany()
                .HasForeignKey(x => x.RiderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Trip)
                .WithMany()
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ResolvedByUser)
                .WithMany()
                .HasForeignKey(x => x.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(60).IsRequired();
            entity.Property(x => x.PhotoPath).HasMaxLength(260);
            entity.Property(x => x.PinHash).HasMaxLength(200);
            entity.Property(x => x.DeleteRequestReason).HasMaxLength(200);
            entity.Property(x => x.DeleteResolutionNote).HasMaxLength(200);
            entity.Ignore(x => x.DisplayName);
            entity.HasOne(x => x.AppUser)
                .WithMany()
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.HailRider)
                .WithMany()
                .HasForeignKey(x => x.HailRiderId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.HailRiderId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(x => x.Token).IsUnique();
            entity.Property(x => x.Token).HasMaxLength(200).IsRequired();
            entity.HasOne(x => x.AppUser)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Province>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<Municipality>(entity =>
        {
            entity.HasIndex(x => new { x.ProvinceId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasOne(x => x.Province)
                .WithMany(x => x.Municipalities)
                .HasForeignKey(x => x.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Barangay>(entity =>
        {
            entity.HasIndex(x => new { x.MunicipalityId, x.Name }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasOne(x => x.Municipality)
                .WithMany(x => x.Barangays)
                .HasForeignKey(x => x.MunicipalityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperatorBarangay>(entity =>
        {
            entity.HasIndex(x => new { x.OperatorId, x.BarangayId }).IsUnique();
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.Areas)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Barangay)
                .WithMany()
                .HasForeignKey(x => x.BarangayId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasIndex(x => new { x.RiderId, x.RequestedAtUtc });
            entity.HasIndex(x => x.ScheduledAtUtc);
            entity.Property(x => x.Pickup).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Dropoff).HasMaxLength(400).IsRequired();
            entity.Property(x => x.PickupDetails).HasMaxLength(200);
            entity.Property(x => x.DropoffDetails).HasMaxLength(200);
            entity.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CustomerPhone).HasMaxLength(20);
            entity.Property(x => x.Reference).HasMaxLength(24).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(200);
            entity.Property(x => x.CancelReason).HasMaxLength(160);
            entity.Property(x => x.RatingComment).HasMaxLength(200);
            entity.Property(x => x.PaymentMethodOther).HasMaxLength(80);
            entity.Property(x => x.Fare).HasColumnType("decimal(18,2)");
            entity.Property(x => x.DistanceKm).HasColumnType("decimal(8,2)");
            entity.HasOne(x => x.PickupBarangay)
                .WithMany()
                .HasForeignKey(x => x.PickupBarangayId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DropoffBarangay)
                .WithMany()
                .HasForeignKey(x => x.DropoffBarangayId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Operator)
                .WithMany()
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Rider)
                .WithMany(x => x.Trips)
                .HasForeignKey(x => x.RiderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Trips)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Bill)
                .WithMany(x => x.Trips)
                .HasForeignKey(x => x.BillId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.ChatMessages)
                .WithOne(x => x.Trip)
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TripChatMessage>(entity =>
        {
            entity.HasIndex(x => new { x.TripId, x.SentAtUtc });
            entity.Property(x => x.Body).HasMaxLength(400).IsRequired();
            entity.Property(x => x.PhotoPath).HasMaxLength(260);
        });

        modelBuilder.Entity<FareMatrix>(entity =>
        {
            entity.HasIndex(x => new { x.OperatorId, x.VehicleType }).IsUnique();
            entity.Property(x => x.BaseFare).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PerKm).HasColumnType("decimal(18,2)");
            entity.Property(x => x.MinimumFare).HasColumnType("decimal(18,2)");
            entity.Property(x => x.IncludedKm).HasColumnType("decimal(6,2)");
            entity.Property(x => x.OperatorCommissionPercent).HasColumnType("decimal(5,2)");
            entity.Property(x => x.DriverCommissionPercent).HasColumnType("decimal(5,2)");
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.FareMatrices)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FareSurcharge>(entity =>
        {
            entity.HasIndex(x => x.FareMatrixId);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.FareMatrix)
                .WithMany(x => x.Surcharges)
                .HasForeignKey(x => x.FareMatrixId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OperatorBill>(entity =>
        {
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => new { x.OperatorId, x.CreatedAtUtc });
            entity.Property(x => x.Number).HasMaxLength(24).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.MotorcycleAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TricycleAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Note).HasMaxLength(200);
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.Bills)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OperatorNotification>(entity =>
        {
            entity.HasIndex(x => new { x.OperatorId, x.CreatedAtUtc });
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(400).IsRequired();
            entity.HasOne(x => x.Operator)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Bill)
                .WithMany()
                .HasForeignKey(x => x.BillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminNotification>(entity =>
        {
            entity.HasIndex(x => new { x.Kind, x.CreatedAtUtc });
            entity.HasIndex(x => x.SupportTicketId);
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(400).IsRequired();
            entity.HasOne(x => x.SupportTicket)
                .WithMany()
                .HasForeignKey(x => x.SupportTicketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasIndex(x => new { x.Status, x.Kind, x.CreatedAtUtc });
            entity.HasIndex(x => x.OperatorId);
            entity.Property(x => x.Subject).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.OperatorNotes).HasMaxLength(2000);
            entity.HasOne(x => x.Operator)
                .WithMany()
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Trip)
                .WithMany()
                .HasForeignKey(x => x.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Customer)
                .WithMany()
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Rider)
                .WithMany()
                .HasForeignKey(x => x.RiderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.OperatorId, x.CreatedAtUtc });
            entity.Property(x => x.Summary).HasMaxLength(400).IsRequired();
            entity.HasOne(x => x.Operator)
                .WithMany()
                .HasForeignKey(x => x.OperatorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Actor)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccessGroup>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200);
        });

        modelBuilder.Entity<AccessGroupPage>(entity =>
        {
            entity.HasIndex(x => new { x.AccessGroupId, x.PageId }).IsUnique();
            entity.Property(x => x.PageId).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.AccessGroup)
                .WithMany(x => x.Pages)
                .HasForeignKey(x => x.AccessGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceRegistration>(entity =>
        {
            entity.HasIndex(x => x.Token).IsUnique();
            entity.HasIndex(x => x.AppUserId);
            entity.Property(x => x.Token).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Platform).HasMaxLength(40).IsRequired();
            entity.HasOne(x => x.AppUser)
                .WithMany()
                .HasForeignKey(x => x.AppUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
