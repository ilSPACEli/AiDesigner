using System.Globalization;

namespace AiDesigner.Clients.Design;

public static class DesignDownloadName
{
    public static string For(DateTime moment) =>
        $"design-{moment.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
}
