using System.Buffers;
using System.Collections.Concurrent;

namespace NestProxy.Rtsp.Buffers;

/// <summary>
/// Provides a lightweight reusable pool for RTP packet buffers sized to match network MTU (1500 bytes).
/// For packets exceeding MTU, we fall back to the shared <see cref="ArrayPool{T}"/>.
/// This pool aims to reduce GC pressure and rent/return churn for typical RTP packet sizes.
/// </summary>
/// <remarks>
/// Creates a new <see cref="LargeBufferPool"/>.
/// </remarks>
/// <param name="bufferSize">Size in bytes of each pooled buffer (default 1500 to match Ethernet MTU).</param>
/// <param name="maxRetained">Maximum number of buffers to retain in the internal bag (default 128).</param>
public sealed class LargeBufferPool(int bufferSize = 1500, int maxRetained = 128)
{
    private readonly ConcurrentBag<byte[]> _bag = [];
    private readonly int _bufferSize = bufferSize;
    private readonly int _maxRetained = maxRetained;

    /// <summary>Gets the fixed size of pooled buffers managed by this instance.</summary>
    public int BufferSize => _bufferSize;

    /// <summary>
    /// Rents a buffer with at least <paramref name="minimumLength"/> bytes. If the request exceeds the fixed
    /// pool size a buffer is rented from the shared <see cref="ArrayPool{T}"/> instead.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <returns>A byte array whose length is either <see cref="BufferSize"/> or large enough to satisfy the request.</returns>
    public byte[] Rent(int minimumLength)
    {
        if (minimumLength > _bufferSize)
        {
            // Fallback to ArrayPool for jumbo packets exceeding MTU; caller returns via Return when done.
            return ArrayPool<byte>.Shared.Rent(minimumLength);
        }

        // Try to reuse from our bag first, otherwise rent from ArrayPool as fallback.
        if (_bag.TryTake(out byte[]? buf))
        {
            return buf;
        }

        // Bag is empty - rent from ArrayPool and request exactly our buffer size
        // so it can be properly returned to our pool later.
        return ArrayPool<byte>.Shared.Rent(_bufferSize);
    }

    /// <summary>
    /// Rents a buffer and returns a span over the rented region for convenient writing.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <param name="buffer">The rented buffer instance that must be returned via <see cref="Return(byte[])"/>.</param>
    /// <returns>A span covering the rented buffer.</returns>
    public Span<byte> RentSpan(int minimumLength, out byte[] buffer)
    {
        buffer = Rent(minimumLength);
        return buffer.AsSpan(0, buffer.Length);
    }

    /// <summary>
    /// Returns a previously rented buffer to either the internal pool (if it matches size) or the shared pool.
    /// </summary>
    /// <param name="buffer">The buffer to return.</param>
    public void Return(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        if (buffer.Length == _bufferSize && _bag.Count < _maxRetained)
        {
            _bag.Add(buffer);
            return;
        }

        // Return to shared pool if not our managed size.
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
