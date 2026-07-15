using WireWarp.Frontend.Shared.Data;
using WireWarp.Frontend.Shared.Interfaces;

namespace WireWarp.Frontend.Shared.IO;

public interface IOutputProcessor
{
    void Process(WiringGraph graph, Output output, ITileAccessor world);
    void Clear();
}
