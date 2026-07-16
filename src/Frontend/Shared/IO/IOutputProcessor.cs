using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.IO;

public interface IOutputProcessor
{
    void Process(WiringGraph graph, Output output);
}
