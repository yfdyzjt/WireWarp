namespace WireWarp.Frontend.Shared;
public static class Runtime
{
    public static string Mode = "reference";
    public static bool FastFrontend => Mode != "reference";
    public static bool CompiledFrontend => Mode == "compiled" || Mode == "async" || Mode == "fast";
    public static bool CompactFrontend => Mode == "fast";
    public static bool AsyncFrontend => Mode == "async" || CompactFrontend;
}
internal static class FrontendDiagnostics { internal static bool ProfileEnabled => true; }
