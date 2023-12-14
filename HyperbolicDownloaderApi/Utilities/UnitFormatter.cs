namespace HyperbolicDownloaderApi.Utilities;

internal static class UnitFormatter
{
    public static string TransferRate(long bytesPerSecond)
    {
        string[] ordinals = ["", "K", "M", "G", "T", "P", "E"];

        decimal rate = bytesPerSecond * 8;

        int ordinal = 0;

        while (rate > 1000)
        {
            rate /= 1000;
            ordinal++;
        }

        return $"{Math.Round(rate, 0, MidpointRounding.AwayFromZero)}{ordinals[ordinal]}bps";
    }

    public static string FileSize(long bytes)
    {
        string[] ordinals = ["", "K", "M", "G", "T", "P", "E"];

        decimal rate = bytes;

        int ordinal = 0;

        while (rate > 1000)
        {
            rate /= 1000;
            ordinal++;
        }

        return $"{Math.Round(rate, 0, MidpointRounding.AwayFromZero)}{ordinals[ordinal]}B";
    }
}