using Windows.Storage.Streams;

namespace DeskTool;

/// <summary>
/// Extension methods for Stream to work with WinRT.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// Convert a .NET Stream to IRandomAccessStream for WinUI.
    /// </summary>
    public static IRandomAccessStream AsRandomAccessStream(this Stream stream)
    {
        return new StreamRandomAccessStream(stream);
    }
}

/// <summary>
/// Wrapper to adapt .NET Stream to IRandomAccessStream.
/// </summary>
internal class StreamRandomAccessStream : IRandomAccessStream
{
    private readonly Stream _stream;

    public StreamRandomAccessStream(Stream stream)
    {
        _stream = stream;
    }

    public bool CanRead => _stream.CanRead;
    public bool CanWrite => _stream.CanWrite;
    public ulong Position => (ulong)_stream.Position;
    public ulong Size
    {
        get => (ulong)_stream.Length;
        set => _stream.SetLength((long)value);
    }

    public IInputStream GetInputStreamAt(ulong position)
    {
        _stream.Seek((long)position, SeekOrigin.Begin);
        return this.AsInputStream();
    }

    public IOutputStream GetOutputStreamAt(ulong position)
    {
        _stream.Seek((long)position, SeekOrigin.Begin);
        return this.AsOutputStream();
    }

    public void Seek(ulong position)
    {
        _stream.Seek((long)position, SeekOrigin.Begin);
    }

    public IRandomAccessStream CloneStream()
    {
        var ms = new MemoryStream();
        _stream.CopyTo(ms);
        ms.Position = 0;
        return new StreamRandomAccessStream(ms);
    }

    public void Dispose()
    {
        // Don't dispose the underlying stream - the caller owns it
    }

    public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
    {
        return _stream.AsInputStream().ReadAsync(buffer, count, options);
    }

    public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
    {
        return _stream.AsOutputStream().WriteAsync(buffer);
    }

    public IAsyncOperation<bool> FlushAsync()
    {
        return _stream.AsOutputStream().FlushAsync();
    }
}
