using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;

namespace YaPasakay.Infrastructure.Persistence;

public static class OperatorAddressSync
{
    public static async Task<(bool Ok, string? Error)> AssignAsync(
        AppDbContext db,
        Operator op,
        Guid barangayId,
        string details,
        CancellationToken cancellationToken)
    {
        if (barangayId == Guid.Empty)
        {
            return (false, "Choose a province, municipality, and barangay for the full address.");
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            return (false, "Add specific address details such as street, building, or unit.");
        }

        var barangay = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.Id == barangayId, cancellationToken);

        if (barangay is null)
        {
            return (false, "The selected address barangay is invalid.");
        }

        op.AddressBarangayId = barangay.Id;
        op.AddressDetails = details.Trim();
        op.FullAddress = Format(op.AddressDetails, barangay);
        return (true, null);
    }

    public static async Task<(bool Ok, string? Error)> AssignAsync(
        AppDbContext db,
        RiderProfile rider,
        Guid barangayId,
        string details,
        CancellationToken cancellationToken)
    {
        if (barangayId == Guid.Empty)
        {
            return (false, "Choose a province, municipality, and barangay for the full address.");
        }

        if (string.IsNullOrWhiteSpace(details))
        {
            return (false, "Add specific address details such as street, building, or unit.");
        }

        var barangay = await db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.Id == barangayId, cancellationToken);

        if (barangay is null)
        {
            return (false, "The selected address barangay is invalid.");
        }

        rider.AddressBarangayId = barangay.Id;
        rider.AddressDetails = details.Trim();
        rider.FullAddress = Format(rider.AddressDetails, barangay);
        return (true, null);
    }

    public static string Format(string details, Barangay barangay)
    {
        var text = string.Join(", ", new[]
        {
            details.Trim(),
            barangay.Name,
            barangay.Municipality.Name,
            barangay.Municipality.Province.Name
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return text.Length <= 400 ? text : text[..397] + "...";
    }

    public static OperatorAddressItem Map(Operator op) =>
        Map(op.AddressBarangayId, op.AddressDetails, op.FullAddress, op.AddressBarangay);

    public static OperatorAddressItem Map(RiderProfile rider) =>
        Map(rider.AddressBarangayId, rider.AddressDetails, rider.FullAddress, rider.AddressBarangay);

    private static OperatorAddressItem Map(
        Guid? barangayId,
        string details,
        string fullAddress,
        Barangay? barangay) =>
        new(
            barangayId,
            barangay?.MunicipalityId,
            barangay?.Municipality.ProvinceId,
            barangay?.Name ?? string.Empty,
            barangay?.Municipality.Name ?? string.Empty,
            barangay?.Municipality.Province.Name ?? string.Empty,
            details,
            fullAddress);
}
