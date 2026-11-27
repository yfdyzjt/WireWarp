using WireWarp.Frontend.Shared.Data;

namespace WireWarp.Frontend.Shared.File;

public static partial class IOFile
{
    public static string PathName =>
        Path.ChangeExtension(Access.Instance.WorldPathName, ".wwio");

    public static void Save()
    {
        var temp = PathName + ".tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                HeaderFile.Write(w, IOGraph.Hash.Span);
                Serialize(w);
            }

            System.IO.File.Move(temp, PathName, overwrite: true);
        }
        catch (Exception e)
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw new Exception($"Failed to save {PathName}: {e.Message}", e);
        }
    }

    public static void Load()
    {
        try
        {
            IOGraph.Clean();

            using var fs = new FileStream(PathName, FileMode.Open);
            using var r = new BinaryReader(fs);

            IOGraph.SetHash(HeaderFile.Read(r));
            Deserialize(r);
        }
        catch (Exception e)
        {
            IOGraph.Clean();
            throw new Exception($"Failed to load {PathName}: {e.Message}", e);
        }
    }
}
