namespace WireWarp.Frontend.Shared.Conversion;

internal static class Applier
{
    public static void Execute()
    {
        Access.Instance.Status("Applying wiring...");
        IO.ProcessOutput.Execute();
    }
}
