using System;
using System.Threading;

namespace Ryujinx.Common
{
    public class ReactiveObject<T>
    {
        private readonly ReaderWriterLockSlim _readerWriterLock = new();
        private bool _isInitialized;
        private T _value;

        public event EventHandler<ReactiveEventArgs<T>> Event;

        public T Value
        {
            get
            {
                _readerWriterLock.EnterReadLock();
                T value = _value;
                _readerWriterLock.ExitReadLock();

                return value;
            }
            set
            {
                _readerWriterLock.EnterWriteLock();

                T oldValue = _value;

                bool oldIsInitialized = _isInitialized;

                _isInitialized = true;
                _value = value;

                _readerWriterLock.ExitWriteLock();

                if (!oldIsInitialized || oldValue == null || !oldValue.Equals(_value))
                {
                    Event?.Invoke(this, new ReactiveEventArgs<T>(oldValue, value));
                }
            }
        }

        public static implicit operator T(ReactiveObject<T> obj) => obj.Value;
    }

    public static class ReactiveObjectHelper
    {
        public static void Toggle(this ReactiveObject<bool> rBoolean) => rBoolean.Value = !rBoolean.Value;
    }

    public class ReactiveEventArgs<T>(T oldValue, T newValue)
    {
        public T OldValue { get; } = oldValue;
        public T NewValue { get; } = newValue;
    }
}
