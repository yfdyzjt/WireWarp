using Terraria.GameContent.Events;

namespace WireWarp.Frontend.tModLoader.IO;

partial class RuntimeOutput
{
    private static void PartyMonolith(int portId, int i, int j)
        => BirthdayParty.ToggleManualParty();
}
