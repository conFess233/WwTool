using System.IO;
using System.Text;

namespace WwTool.Common.Utils;

/// <summary>在同一目录写入并落盘后原子替换，失败时保留原文件。</summary>
public static class AtomicFile
{
    public static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>序列化由调用者完成；文件操作不捕获 UI 同步上下文。</summary>
    public static Task WriteTextAsync(string path, string content, CancellationToken token = default) =>
        WriteAsync(path, (stream, cancellation) => stream.WriteAsync(Encoding.UTF8.GetBytes(content), cancellation).AsTask(), token);

    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await write(stream, token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
                stream.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
