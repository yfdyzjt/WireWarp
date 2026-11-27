namespace WireWarp.Frontend.Shared.File;

public static class WWorldFile
{
    public static string PathName =>
        Path.ChangeExtension(Access.Instance.WorldPathName, ".wwld");

    public static void Save()
    {
        var temp = PathName + ".tmp";
        try
        {
            using (var src = new FileStream(Access.Instance.WorldPathName, FileMode.Open))
            using (var dst = new FileStream(temp, FileMode.Create))
            using (var w = new BinaryWriter(dst))
            {
                HeaderFile.Write(w, Runtime.Hash.Span);
                src.CopyTo(dst);
            }

            System.IO.File.Move(temp, PathName, overwrite: true);
        }
        catch (Exception e)
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw new Exception($"Failed to save world snapshot {PathName}: {e.Message}", e);
        }
    }

    public static void Load()
    {
        var temp = Access.Instance.WorldPathName + ".tmp";
        try
        {
            using (var src = new FileStream(PathName, FileMode.Open))
            using (var dst = new FileStream(temp, FileMode.Create))
            using (var r = new BinaryReader(src))
            {
                var hash = HeaderFile.Read(r);
                src.CopyTo(dst);
            }

            System.IO.File.Move(temp, Access.Instance.WorldPathName, overwrite: true);
        }
        catch (Exception e)
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw new Exception($"Failed to load world snapshot {PathName}: {e.Message}", e);
        }
    }
}
