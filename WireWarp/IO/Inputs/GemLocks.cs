namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeInput
{
    private static void GemLocks(int portId, int i, int j) =>
        PressurePlates(portId, i + 1, j + 1);
}
