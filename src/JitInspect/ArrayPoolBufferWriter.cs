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
        T[]? array = this.array;

        if (array is null)
        {
            ThrowObjectDisposedException();
        }

        if (count < 0)
        {
            ThrowArgumentOutOfRangeExceptionForNegativeCount();
        }

        if (this.index > array!.Length - count)
        {
            ThrowArgumentExceptionForAdvancedTooFar();
        }

        this.index += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        CheckBufferAndEnsureCapacity(sizeHint);

        return this.array.AsMemory(this.index);
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        CheckBufferAndEnsureCapacity(sizeHint);

        return this.array.AsSpan(this.index);
    }
    
    public Span<T> AsSpan() => this.array.AsSpan(0, this.index);

    public void Dispose()
    {
        T[]? array = this.array;

        if (array is null)
        {
            return;
        }

        this.array = null;

        pool.Return(array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void CheckBufferAndEnsureCapacity(int sizeHint)
    {
        T[]? array = this.array;

        if (array is null)
        {
            ThrowObjectDisposedException();
        }

        if (sizeHint < 0)
        {
            ThrowArgumentOutOfRangeExceptionForNegativeSizeHint();
        }

        if (sizeHint == 0)
        {
            sizeHint = 1;
        }

        if (sizeHint > array!.Length - this.index)
        {
            ResizeBuffer(sizeHint);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void ResizeBuffer(int sizeHint)
    {
        uint minimumSize = (uint)this.index + (uint)sizeHint;
        if (minimumSize > 1024 * 1024)
        {
            var newMinimumSize = 1024u * 1024u;
            while (newMinimumSize < minimumSize)
            {
                newMinimumSize <<= 1;
            }

            minimumSize = newMinimumSize;
        }

        var newBuffer = pool.Rent((int)minimumSize);
        Array.Copy(this.array!, newBuffer, this.index);
        pool.Return(this.array!);
        this.array = newBuffer;
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