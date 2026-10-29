namespace AiDesigner.Tests.Data;


internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string WebRoot => Path.Combine(Root, "wwwroot");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AiDesigner.csproj")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Не знайдено корінь репозиторію (AiDesigner.csproj).");
    }
}
