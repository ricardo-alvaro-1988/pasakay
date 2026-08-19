using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;

namespace YaPasakay.Infrastructure.Persistence;

public static class OperatorAreaSync
{
    public static async Task<(bool Ok, string? Error)> AssignAsync(
        AppDbContext db,
        Operator op,
        IReadOnlyCollection<Guid> barangayIds,
        CancellationToken cancellationToken)
    {
        var unique = barangayIds.Distinct().ToList();
        if (unique.Count == 0)
        {
            return (false, "Assign at least one barangay to the area of operation.");
        }

        var barangays = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Where(x => unique.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (barangays.Count != unique.Count)
        {
            return (false, "One or more barangays are invalid.");
        }

        var current = await db.OperatorBarangays
            .Where(x => x.OperatorId == op.Id)
            .ToListAsync(cancellationToken);
        db.OperatorBarangays.RemoveRange(current);

        foreach (var barangay in barangays)
        {
            db.OperatorBarangays.Add(new OperatorBarangay
            {
                OperatorId = op.Id,
                BarangayId = barangay.Id
            });
        }

        op.AreaOfOperation = Summarize(barangays);
        return (true, null);
    }

    public static string Summarize(IEnumerable<Barangay> barangays)
    {
        var text = string.Join(", ", barangays
            .GroupBy(x => x.Municipality.Name)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key} ({g.Count()})"));

        return text.Length <= 400 ? text : text[..397] + "...";
    }

    public static IReadOnlyList<OperatorAreaItem> Map(IEnumerable<OperatorBarangay> areas) =>
        areas
            .OrderBy(x => x.Barangay.Municipality.Province.Name)
            .ThenBy(x => x.Barangay.Municipality.Name)
            .ThenBy(x => x.Barangay.Name)
            .Select(x => new OperatorAreaItem(
                x.BarangayId,
                x.Barangay.Name,
                x.Barangay.Municipality.Name,
                x.Barangay.Municipality.Province.Name))
            .ToList();

    public static Task<bool> ContainsBarangayAsync(
        AppDbContext db,
        Guid operatorId,
        Guid barangayId,
        CancellationToken cancellationToken) =>
        db.OperatorBarangays.AnyAsync(
            x => x.OperatorId == operatorId && x.BarangayId == barangayId,
            cancellationToken);

    public static async Task<bool> CoversAsync(
        AppDbContext db,
        Guid operatorId,
        Guid? barangayId,
        CancellationToken cancellationToken)
    {
        if (barangayId is not Guid id)
        {
            return false;
        }

        if (await ContainsBarangayAsync(db, operatorId, id, cancellationToken))
        {
            return true;
        }

        var municipalityId = await db.Barangays
            .Where(x => x.Id == id)
            .Select(x => (Guid?)x.MunicipalityId)
            .FirstOrDefaultAsync(cancellationToken);
        if (municipalityId is null)
        {
            return false;
        }

        return await db.OperatorBarangays.AnyAsync(
            x => x.OperatorId == operatorId && x.Barangay.MunicipalityId == municipalityId,
            cancellationToken);
    }

    public static async Task<string?> CoverageErrorAsync(
        AppDbContext db,
        Guid operatorId,
        Guid? pickupBarangayId,
        CancellationToken cancellationToken)
    {
        if (!await CoversAsync(db, operatorId, pickupBarangayId, cancellationToken))
        {
            return "Pickup is outside this operator's service area.";
        }

        return null;
    }
}
