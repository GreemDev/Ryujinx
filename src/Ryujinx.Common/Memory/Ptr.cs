using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Ryujinx.Common.Memory
{
    /// <summary>
    /// Represents a pointer to an unmanaged resource.
    /// </summary>
    /// <typeparam name="T">Type of the unmanaged resource</typeparam>
    public unsafe struct Ptr<T> : IEquatable<Ptr<T>> where T : unmanaged
    {
        private nint _ptr;

        /// <summary>
        /// Null pointer.
        /// </summary>
        public static Ptr<T> Null => new() { _ptr = nint.Zero };

        /// <summary>
        /// True if the pointer is null, false otherwise.
        /// </summary>
        public readonly bool IsNull => _ptr == nint.Zero;

        /// <summary>
        /// Gets a reference to the value.
        /// </summary>
        public readonly ref T Value => ref Unsafe.AsRef<T>((void*)_ptr);

        /// <summary>
        /// Creates a new pointer to an unmanaged resource.
        /// </summary>
        /// <remarks>
        /// For data on the heap, proper pinning is necessary during
        /// use. Failure to do so will result in memory corruption and crashes.
        /// </remarks>
        /// <param name="value">Reference to the unmanaged resource</param>
        public Ptr(ref T value)
        {
            _ptr = (nint)Unsafe.AsPointer(ref value);
        }

        public readonly override bool Equals(object obj)
        {
            return obj is Ptr<T> other && Equals(other);
        }

        public readonly bool Equals([AllowNull] Ptr<T> other)
        {
            return _ptr == other._ptr;
        }

        public readonly override int GetHashCode()
        {
            return _ptr.GetHashCode();
        }

        public static bool operator ==(Ptr<T> left, Ptr<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Ptr<T> left, Ptr<T> right)
        {
            return !(left == right);
        }
    }
}
