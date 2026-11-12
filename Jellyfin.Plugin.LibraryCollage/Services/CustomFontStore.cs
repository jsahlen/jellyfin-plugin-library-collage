using System;
using System.IO;
using SkiaSharp;

namespace Jellyfin.Plugin.LibraryCollage.Services;

/// <summary>
/// Keeps the uploaded font that replaces the built-in fonts of every text style.
/// Stored as a file in the plugin data folder, so saving the settings page doesn't touch it.
/// </summary>
public class CustomFontStore
{
    /// <summary>The largest font file accepted, in bytes.</summary>
    public const long MaxBytes = 20 * 1024 * 1024;

    private readonly object _lock = new();

    private static string FilePath
    {
        get
        {
            var folder = Plugin.Instance?.DataFolderPath ?? throw new InvalidOperationException("Plugin not initialized.");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "custom-font");
        }
    }

    /// <summary>
    /// Gets the path of the uploaded font.
    /// </summary>
    /// <returns>The path, or <c>null</c> if no font was uploaded.</returns>
    public string? GetPath()
    {
        var path = FilePath;
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Gets the family name of the uploaded font.
    /// </summary>
    /// <returns>The family name, or <c>null</c> if no usable font was uploaded.</returns>
    public string? GetFamilyName()
    {
        lock (_lock)
        {
            var path = GetPath();
            if (path is null)
            {
                return null;
            }

            using var data = SKData.CreateCopy(File.ReadAllBytes(path));
            using var typeface = SKTypeface.FromData(data);
            return typeface?.FamilyName;
        }
    }

    /// <summary>
    /// Replaces the uploaded font.
    /// </summary>
    /// <param name="data">The font file's contents.</param>
    /// <returns>The family name, or <c>null</c> if the data is not a font Skia can read.</returns>
    public string? Save(byte[] data)
    {
        using var skData = SKData.CreateCopy(data);
        using var typeface = SKTypeface.FromData(skData);
        if (typeface is null)
        {
            return null;
        }

        lock (_lock)
        {
            var path = FilePath;
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, data);
            File.Move(temp, path, overwrite: true);
        }

        return typeface.FamilyName;
    }

    /// <summary>
    /// Removes the uploaded font so the built-in fonts are used again.
    /// </summary>
    public void Remove()
    {
        lock (_lock)
        {
            File.Delete(FilePath);
        }
    }
}
