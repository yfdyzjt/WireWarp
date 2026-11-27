namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void TrapdoorClosed(int portId, int i, int j)
        => TrapdoorOpen(portId, i, j);
}
