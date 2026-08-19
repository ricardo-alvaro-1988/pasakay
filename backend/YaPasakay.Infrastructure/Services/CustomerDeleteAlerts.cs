using Microsoft.EntityFrameworkCore;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Infrastructure.Services;

public static class CustomerDeleteAlerts
{
    public static async Task NotifyAsync(AppDbContext db, CustomerProfile customer, CancellationToken cancellationToken)
    {
        var name = customer.AppUser.FullName;
        var phone = customer.AppUser.PhoneNumber;
        const string title = "Account deletion requested";
        var body = $"{name} ({phone}) requested to delete their account.";

        db.AdminNotifications.Add(new AdminNotification
        {
            Kind = NotificationKind.AccountDelete,
            Title = title,
            Body = body
        });

        var operatorIds = await db.Trips
            .Where(x => x.CustomerId == customer.Id)
            .Select(x => x.OperatorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var operatorId in operatorIds)
        {
            db.OperatorNotifications.Add(new OperatorNotification
            {
                OperatorId = operatorId,
                Kind = NotificationKind.AccountDelete,
                Title = title,
                Body = body
            });
        }
    }
}
