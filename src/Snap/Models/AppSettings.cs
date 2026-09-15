using System;
using System.IO;

namespace Snap.Models;

public class AppSettings
{
    public string SaveFolder { get; set; } = GetDefaultSaveFolder();
    public bool CopyPathOnSave { get; set; } = true;

    public static string GetDefaultSaveFolder()
    {
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(pictures, "Snap");
    }
}
