using System.IO;
using System.Text;

namespace Jester;

/// <summary>
/// The file system calls a save makes: the temp file it writes and the replace or move
/// that puts it in place. Tests override single calls to see their order or make them fail.
/// </summary>
internal class FileOps
{
    public static FileOps Real { get; } = new();

    public virtual FileStream Create(string path) =>
        new(path, FileMode.Create, FileAccess.Write, FileShare.Read);

    public virtual void Replace(string source, string target) =>
        File.Replace(source, target, destinationBackupFileName: null);

    public virtual void Move(string source, string target, bool overwrite) =>
        File.Move(source, target, overwrite);

    /// <summary>Writes the text to a new file as UTF-8 without a byte-order mark.</summary>
    public void WriteText(string path, string text) =>
        WriteText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>Writes the text to a new file, with the encoding's byte-order mark as
    /// <see cref="File.WriteAllText(string, string, Encoding)"/> writes it, and flushes it
    /// to the disk. NTFS journals the rename but not the data, so without the flush a power
    /// cut after the replace or move can leave the file empty.</summary>
    public void WriteText(string path, string text, Encoding encoding)
    {
        using var stream = Create(path);
        using (var writer = new StreamWriter(stream, encoding, bufferSize: -1, leaveOpen: true))
            writer.Write(text);
        stream.Flush(flushToDisk: true);
    }
}
