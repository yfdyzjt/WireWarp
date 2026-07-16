using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

internal interface IOutputProcessor
{
    void Process(WiringGraph graph, Output output);
}
