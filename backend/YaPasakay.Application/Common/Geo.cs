namespace YaPasakay.Application.Common;

public static class Geo
{
    public static double? DistanceKm(double? lat1, double? lng1, double? lat2, double? lng2)
    {
        if (lat1 is null || lng1 is null || lat2 is null || lng2 is null)
        {
            return null;
        }

        const double earthKm = 6371;
        var dLat = ToRad(lat2.Value - lat1.Value);
        var dLng = ToRad(lng2.Value - lng1.Value);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(ToRad(lat1.Value)) * Math.Cos(ToRad(lat2.Value))
            * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180;
}
