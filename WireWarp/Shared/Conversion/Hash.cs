using System.Buffers.Binary;
using System.Security.Cryptography;
using WireWarp.Frontend.Shared.Terraria;

namespace WireWarp.Frontend.Shared.Conversion;

public static class Hash
{
    public static byte[] Execute()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> cell = stackalloc byte[15];

        var w = Access.Instance.MaxTilesX;
        var h = Access.Instance.MaxTilesY;

        for (var x = 0; x < w; x++)
        {
            if (x % Math.Max(1, w / 100) == 0)
                Access.Instance.Status($"Hashing tiles {x * 100 / w}%");
            for (var y = 0; y < h; y++)
            {
                var tile = Access.Instance.GetTile(x, y);
                if (!Detector.HasWiring(tile)) continue;

                byte flags = 0;

                if (tile.HasTile) flags |= 1 << 0;
                if (tile.HasActuator) flags |= 1 << 1;
                if (tile.IsActuated) flags |= 1 << 2;
                if (tile.RedWire) flags |= 1 << 3;
                if (tile.BlueWire) flags |= 1 << 4;
                if (tile.GreenWire) flags |= 1 << 5;
                if (tile.YellowWire) flags |= 1 << 6;

                cell[0] = flags;
                BinaryPrimitives.WriteInt32LittleEndian(cell[1..], x);
                BinaryPrimitives.WriteInt32LittleEndian(cell[5..], y);
                BinaryPrimitives.WriteUInt16LittleEndian(cell[9..], tile.type);
                BinaryPrimitives.WriteInt16LittleEndian(cell[11..], tile.frameX);
                BinaryPrimitives.WriteInt16LittleEndian(cell[13..], tile.frameY);

                hash.AppendData(cell);
            }
        }

        return hash.GetHashAndReset();
    }
}
