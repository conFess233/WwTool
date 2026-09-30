using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 文件读取工具类
    /// </summary>
    public static class ReadLines
    {
        /// <summary>
        /// 以共享读写的方式逐行读取文本文件
        /// </summary>
        /// <param name="path">文件绝对路径</param>
        /// <returns>日志行</returns>
        public static IEnumerable<string> ReadLinesShared(string path)
        {
            using FileStream fs =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);

            using StreamReader reader = new StreamReader(fs);

            while (!reader.EndOfStream)
            {
                yield return reader.ReadLine()!;
            }
        }

        /// <summary>
        /// 读取并解密加密的日志文件，以迭代器形式返回解密后的每一行文本
        /// </summary>
        /// <param name="path">加密的日志文件路径</param>
        /// <returns>解密后的明文日志行</returns>
        public static IEnumerable<string> ReadLinesDecrypt(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path)) yield break;
            using var stream = new DecryptStream(path, cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return line;
            }
        }

        /// <summary>随 StreamReader 分块读取并原地变换，不复制整个日志。</summary>
        private sealed class DecryptStream(string path, CancellationToken token)
            : Stream
        {
            private readonly FileStream _source = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
            public override int Read(Span<byte> buffer)
            {
                token.ThrowIfCancellationRequested();
                int read = _source.Read(buffer);
                for (int i = 0; i < read; i++)
                    buffer[i] = (byte)(buffer[i] ^ ((buffer[i] & 1) != 0 ? 0xA5 : 0xEF));
                return read;
            }
        }
    }
}
