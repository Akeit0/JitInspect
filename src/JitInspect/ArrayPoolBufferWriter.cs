// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace JitInspect;

internal sealed class ArrayPoolBufferWriter<T>(ArrayPool<T> pool, int initialCapacity = ArrayPoolBufferWriter<T>.DefaultInitialBufferSize) : IBufferWriter<T>, IDisposable
{
    const int DefaultInitialBufferSize = 256;
    T[]? array = pool.Rent(initialCapacity);

    int index = 0;

    public ArrayPoolBufferWriter(int initialCapacity = DefaultInitialBufferSize) : this(ArrayPool<T>.Shared, initialCapacity)
    {
    }

    public void Advance(int count)
    {
        var array = this.array;

        if (array is null) ThrowObjectDisposedException();

        if (count < 0) ThrowArgumentOutOfRangeExceptionForNegativeCount();

        if (index > array!.Length - count) ThrowArgumentExceptionForAdvancedTooFar();

        index += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        CheckBufferAndEnsureCapacity(sizeHint);

        return array.AsMemory(index);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        CheckBufferAndEnsureCapacity(sizeHint);

        return array.AsSpan(index);
    }

    public Span<T> AsSpan()
    {
        return array.AsSpan(0, index);
    }

    public void Dispose()
    {
        var array = this.array;

        if (array is null) return;

        this.array = null;

        pool.Return(array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void CheckBufferAndEnsureCapacity(int sizeHint)
    {
        var array = this.array;

        if (array is null) ThrowObjectDisposedException();

        if (sizeHint < 0) ThrowArgumentOutOfRangeExceptionForNegativeSizeHint();

        if (sizeHint == 0) sizeHint = 1;

        if (sizeHint > array!.Length - index) ResizeBuffer(sizeHint);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void ResizeBuffer(int sizeHint)
    {
        var minimumSize = (uint)index + (uint)sizeHint;
        if (minimumSize > 1024 * 1024)
        {
            var newMinimumSize = 1024u * 1024u;
            while (newMinimumSize < minimumSize) newMinimumSize <<= 1;

            minimumSize = newMinimumSize;
        }

        var newBuffer = pool.Rent((int)minimumSize);
        Array.Copy(array!, newBuffer, index);
        pool.Return(array!);
        array = newBuffer;
    }

    static void ThrowArgumentOutOfRangeExceptionForNegativeCount()
    {
        throw new ArgumentOutOfRangeException("count", "The count can't be a negative value.");
    }

    static void ThrowArgumentOutOfRangeExceptionForNegativeSizeHint()
    {
        throw new ArgumentOutOfRangeException("sizeHint", "The size hint can't be a negative value.");
    }

    static void ThrowArgumentExceptionForAdvancedTooFar()
    {
        throw new ArgumentException("The buffer writer has advanced too far.");
    }

    static void ThrowObjectDisposedException()
    {
        throw new ObjectDisposedException("The current buffer has already been disposed.");
    }
}