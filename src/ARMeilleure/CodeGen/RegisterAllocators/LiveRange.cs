using System;

namespace ARMeilleure.CodeGen.RegisterAllocators
{
    unsafe readonly struct LiveRange : IEquatable<LiveRange>
    {
        private struct Data
        {
            public int Start;
            public int End;
            public LiveRange Next;
        }

        private readonly Data* _data;

        public ref int Start => ref _data->Start;
        public ref int End => ref _data->End;
        public ref LiveRange Next => ref _data->Next;

        public LiveRange(int start, int end, LiveRange next = default)
        {
            _data = Allocators.LiveRanges.Allocate<Data>();

            Start = start;
            End = end;
            Next = next;
        }

        public bool Overlaps(int start, int end)
        {
            return Start < end && start < End;
        }

        public bool Overlaps(LiveRange range)
        {
            return Start < range.End && range.Start < End;
        }

        public bool Overlaps(int position)
        {
            return position >= Start && position < End;
        }

        public bool Equals(LiveRange range)
        {
            return range._data == _data;
        }

        public override bool Equals(object obj)
        {
            return obj is LiveRange range && Equals(range);
        }

        public static bool operator ==(LiveRange a, LiveRange b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(LiveRange a, LiveRange b)
        {
            return !a.Equals(b);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((nint)_data);
        }

        public override string ToString()
        {
            return $"[{Start}, {End})";
        }
    }
}
