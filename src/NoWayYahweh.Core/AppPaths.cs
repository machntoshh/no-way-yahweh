namespace NoWayYahweh.Core;

public static class AppPaths
{
    /// <summary>Overrides the data folder; used by tests so they never touch the real state.</summary>
    public const string DataDirVariable = "NOWAYYAHWEH_DATA_DIR";

    public static string DataDir =>
        Environment.GetEnvironmentVariable(DataDirVariable) is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoWayYahweh");

    public static string StateFile => Path.Combine(DataDir, "state.json");

    /// <summary>User-maintained list of additional domains to block, one per line.</summary>
    public static string ExtraDomainsFile => Path.Combine(DataDir, "extra-domains.txt");
}
