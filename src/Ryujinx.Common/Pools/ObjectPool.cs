using System;
using System.Threading;

namespace Ryujinx.Common
{
    public class ObjectPool<T>(Func<T> factory, int size)
        where T : class
    {
        private T _firstItem;
        private readonly T[] _items = new T[size - 1];

        public T Allocate()
        {
            T instance = _firstItem;

            if (instance == null || instance != Interlocked.CompareExchange(ref _firstItem, null, instance))
            {
                instance = AllocateInternal();
            }

            return instance;
        }

        private T AllocateInternal()
        {
            T[] items = _items;

            for (int i = 0; i < items.Length; i++)
            {
                T instance = items[i];

                if (instance != null && instance == Interlocked.CompareExchange(ref items[i], null, instance))
                {
                    return instance;
                }
            }

            return factory();
        }

        public void Release(T obj)
        {
            if (_firstItem == null)
            {
                _firstItem = obj;
            }
            else
            {
                ReleaseInternal(obj);
            }
        }

        private void ReleaseInternal(T obj)
        {
            T[] items = _items;

            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null)
                {
                    items[i] = obj;
                    break;
                }
            }
        }
    }
}
