using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ARMeilleure.IntermediateRepresentation
{
    /// <summary>
    /// Represents a efficient linked list that stores the pointer on the object directly and does not allocate.
    /// </summary>
    /// <typeparam name="T">Type of the list items</typeparam>
    class IntrusiveList<T> where T : IEquatable<T>, IIntrusiveListNode<T>
    {
        /// <summary>
        /// First item of the list, or null if empty.
        /// </summary>
        public T First { get; private set; }

        /// <summary>
        /// Last item of the list, or null if empty.
        /// </summary>
        public T Last { get; private set; }

        /// <summary>
        /// Total number of items on the list.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntrusiveList{T}"/> class.
        /// </summary>
        /// <exception cref="ArgumentException"><typeparamref name="T"/> is not pointer sized.</exception>
        public IntrusiveList()
        {
            if (Unsafe.SizeOf<T>() != nint.Size)
            {
                throw new ArgumentException("T must be a reference type or a pointer sized struct.");
            }
        }

        /// <summary>
        /// Adds a item as the first item of the list.
        /// </summary>
        /// <param name="newNode">Item to be added</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T AddFirst(T newNode)
        {
            if (!EqualsNull(First))
            {
                return AddBefore(First, newNode);
            }
            else
            {
                Debug.Assert(EqualsNull(newNode.ListPrevious));
                Debug.Assert(EqualsNull(newNode.ListNext));
                Debug.Assert(EqualsNull(Last));

                First = newNode;
                Last = newNode;

                Debug.Assert(Count == 0);

                Count = 1;

                return newNode;
            }
        }

        /// <summary>
        /// Adds a item as the last item of the list.
        /// </summary>
        /// <param name="newNode">Item to be added</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T AddLast(T newNode)
        {
            if (!EqualsNull(Last))
            {
                return AddAfter(Last, newNode);
            }
            else
            {
                Debug.Assert(EqualsNull(newNode.ListPrevious));
                Debug.Assert(EqualsNull(newNode.ListNext));
                Debug.Assert(EqualsNull(First));

                First = newNode;
                Last = newNode;

                Debug.Assert(Count == 0);

                Count = 1;

                return newNode;
            }
        }

        /// <summary>
        /// Adds a item before a existing item on the list.
        /// </summary>
        /// <param name="node">Item on the list that will succeed the new item</param>
        /// <param name="newNode">Item to be added</param>
        /// <returns>New item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T AddBefore(T node, T newNode)
        {
            Debug.Assert(EqualsNull(newNode.ListPrevious));
            Debug.Assert(EqualsNull(newNode.ListNext));

            newNode.ListPrevious = node.ListPrevious;
            newNode.ListNext = node;

            node.ListPrevious = newNode;

            if (!EqualsNull(newNode.ListPrevious))
            {
                newNode.ListPrevious.ListNext = newNode;
            }

            if (Equals(First, node))
            {
                First = newNode;
            }

            Count++;

            return newNode;
        }

        /// <summary>
        /// Adds a item after a existing item on the list.
        /// </summary>
        /// <param name="node">Item on the list that will preceed the new item</param>
        /// <param name="newNode">Item to be added</param>
        /// <returns>New item</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T AddAfter(T node, T newNode)
        {
            Debug.Assert(EqualsNull(newNode.ListPrevious));
            Debug.Assert(EqualsNull(newNode.ListNext));

            newNode.ListPrevious = node;
            newNode.ListNext = node.ListNext;

            node.ListNext = newNode;

            if (!EqualsNull(newNode.ListNext))
            {
                newNode.ListNext.ListPrevious = newNode;
            }

            if (Equals(Last, node))
            {
                Last = newNode;
            }

            Count++;

            return newNode;
        }

        /// <summary>
        /// Removes a item from the list.
        /// </summary>
        /// <param name="node">The item to be removed</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(T node)
        {
            if (!EqualsNull(node.ListPrevious))
            {
                node.ListPrevious.ListNext = node.ListNext;
            }
            else
            {
                Debug.Assert(Equals(First, node));

                First = node.ListNext;
            }

            if (!EqualsNull(node.ListNext))
            {
                node.ListNext.ListPrevious = node.ListPrevious;
            }
            else
            {
                Debug.Assert(Equals(Last, node));

                Last = node.ListPrevious;
            }

            node.ListPrevious = default;
            node.ListNext = default;

            Count--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool EqualsNull(T a)
        {
            return EqualityComparer<T>.Default.Equals(a, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Equals(T a, T b)
        {
            return EqualityComparer<T>.Default.Equals(a, b);
        }
    }
}
