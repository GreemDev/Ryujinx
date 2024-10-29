using ARMeilleure.IntermediateRepresentation;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ARMeilleure.CodeGen.RegisterAllocators
{
    unsafe readonly struct LiveInterval : IComparable<LiveInterval>
    {
        public const int NotFound = -1;

        private struct Data
        {
            public int End;
            public int SpillOffset;

            public LiveRange FirstRange;
            public LiveRange PrevRange;
            public LiveRange CurrRange;

            public LiveInterval Parent;
            public LiveInterval CopySource;

            public UseList Uses;
            public LiveIntervalList Children;

            public Operand Local;
            public Register Register;

            public bool IsFixed;
            public bool IsFixedAndUsed;
        }

        private readonly Data* _data;

        private ref int End => ref _data->End;
        private ref LiveRange FirstRange => ref _data->FirstRange;
        private ref LiveRange CurrRange => ref _data->CurrRange;
        private ref LiveRange PrevRange => ref _data->PrevRange;
        private ref LiveInterval Parent => ref _data->Parent;
        private ref LiveInterval CopySource => ref _data->CopySource;
        private ref UseList Uses => ref _data->Uses;
        private ref LiveIntervalList Children => ref _data->Children;

        public Operand Local => _data->Local;
        public ref Register Register => ref _data->Register;
        public ref int SpillOffset => ref _data->SpillOffset;

        public bool IsFixed => _data->IsFixed;
        public ref bool IsFixedAndUsed => ref _data->IsFixedAndUsed;
        public bool IsEmpty => FirstRange == default;
        public bool IsSplit => Children.Count != 0;
        public bool IsSpilled => SpillOffset != -1;

        public int UsesCount => Uses.Count;

        public LiveInterval(Operand local = default, LiveInterval parent = default)
        {
            _data = Allocators.LiveIntervals.Allocate<Data>();
            *_data = default;

            _data->IsFixed = false;
            _data->Local = local;

            Parent = parent == default ? this : parent;
            Uses = new UseList();
            Children = new LiveIntervalList();

            FirstRange = default;
            CurrRange = default;
            PrevRange = default;

            SpillOffset = -1;
        }

        public LiveInterval(Register register) : this(local: default, parent: default)
        {
            _data->IsFixed = true;

            Register = register;
        }

        public void SetCopySource(LiveInterval copySource)
        {
            CopySource = copySource;
        }

        public bool TryGetCopySourceRegister(out int copySourceRegIndex)
        {
            if (CopySource._data != null)
            {
                copySourceRegIndex = CopySource.Register.Index;

                return true;
            }

            copySourceRegIndex = 0;

            return false;
        }

        public void Reset()
        {
            PrevRange = default;
            CurrRange = FirstRange;
        }

        public void Forward(int position)
        {
            LiveRange prev = PrevRange;
            LiveRange curr = CurrRange;

            while (curr != default && curr.Start < position && !curr.Overlaps(position))
            {
                prev = curr;
                curr = curr.Next;
            }

            PrevRange = prev;
            CurrRange = curr;
        }

        public int GetStart()
        {
            Debug.Assert(!IsEmpty, "Empty LiveInterval cannot have a start position.");

            return FirstRange.Start;
        }

        public void SetStart(int position)
        {
            if (FirstRange != default)
            {
                Debug.Assert(position != FirstRange.End);

                FirstRange.Start = position;
            }
            else
            {
                FirstRange = new LiveRange(position, position + 1);
                End = position + 1;
            }
        }

        public int GetEnd()
        {
            Debug.Assert(!IsEmpty, "Empty LiveInterval cannot have an end position.");

            return End;
        }

        public void AddRange(int start, int end)
        {
            Debug.Assert(start < end, $"Invalid range start position {start}, {end}");

            if (FirstRange != default)
            {
                // If the new range ends exactly where the first range start, then coalesce together.
                if (end == FirstRange.Start)
                {
                    FirstRange.Start = start;

                    return;
                }
                // If the new range is already contained, then coalesce together.
                else if (FirstRange.Overlaps(start, end))
                {
                    FirstRange.Start = Math.Min(FirstRange.Start, start);
                    FirstRange.End = Math.Max(FirstRange.End, end);
                    End = Math.Max(End, end);

                    Debug.Assert(FirstRange.Next == default || !FirstRange.Overlaps(FirstRange.Next));
                    return;
                }
            }

            FirstRange = new LiveRange(start, end, FirstRange);
            End = Math.Max(End, end);

            Debug.Assert(FirstRange.Next == default || !FirstRange.Overlaps(FirstRange.Next));
        }

        public void AddUsePosition(int position)
        {
            Uses.Add(position);
        }

        public bool Overlaps(int position)
        {
            LiveRange curr = CurrRange;

            while (curr != default && curr.Start <= position)
            {
                if (curr.Overlaps(position))
                {
                    return true;
                }

                curr = curr.Next;
            }

            return false;
        }

        public bool Overlaps(LiveInterval other)
        {
            return GetOverlapPosition(other) != NotFound;
        }

        public int GetOverlapPosition(LiveInterval other)
        {
            LiveRange a = CurrRange;
            LiveRange b = other.CurrRange;

            while (a != default)
            {
                while (b != default && b.Start < a.Start)
                {
                    if (a.Overlaps(b))
                    {
                        return a.Start;
                    }

                    b = b.Next;
                }

                if (b == default)
                {
                    break;
                }
                else if (a.Overlaps(b))
                {
                    return a.Start;
                }

                a = a.Next;
            }

            return NotFound;
        }

        public ReadOnlySpan<LiveInterval> SplitChildren()
        {
            return Parent.Children.Span;
        }

        public ReadOnlySpan<int> UsePositions()
        {
            return Uses.Span;
        }

        public int FirstUse()
        {
            return Uses.FirstUse;
        }

        public int NextUseAfter(int position)
        {
            return Uses.NextUse(position);
        }

        public LiveInterval Split(int position)
        {
            LiveInterval result = new(Local, Parent)
            {
                End = End,
            };

            LiveRange prev = PrevRange;
            LiveRange curr = CurrRange;

            while (curr != default && curr.Start < position && !curr.Overlaps(position))
            {
                prev = curr;
                curr = curr.Next;
            }

            if (curr.Start >= position)
            {
                prev.Next = default;

                result.FirstRange = curr;

                End = prev.End;
            }
            else
            {
                result.FirstRange = new LiveRange(position, curr.End, curr.Next);

                curr.End = position;
                curr.Next = default;

                End = curr.End;
            }

            result.Uses = Uses.Split(position);

            AddSplitChild(result);

            Debug.Assert(!IsEmpty, "Left interval is empty after split.");
            Debug.Assert(!result.IsEmpty, "Right interval is empty after split.");

            // Make sure the iterator in the new split is pointing to the start.
            result.Reset();

            return result;
        }

        private void AddSplitChild(LiveInterval child)
        {
            Debug.Assert(!child.IsEmpty, "Trying to insert an empty interval.");

            Parent.Children.Add(child);
        }

        public LiveInterval GetSplitChild(int position)
        {
            if (Overlaps(position))
            {
                return this;
            }

            foreach (LiveInterval splitChild in SplitChildren())
            {
                if (splitChild.Overlaps(position))
                {
                    return splitChild;
                }
                else if (splitChild.GetStart() > position)
                {
                    break;
                }
            }

            return default;
        }

        public bool TrySpillWithSiblingOffset()
        {
            foreach (LiveInterval splitChild in SplitChildren())
            {
                if (splitChild.IsSpilled)
                {
                    Spill(splitChild.SpillOffset);

                    return true;
                }
            }

            return false;
        }

        public void Spill(int offset)
        {
            SpillOffset = offset;
        }

        public int CompareTo(LiveInterval interval)
        {
            if (FirstRange == default || interval.FirstRange == default)
            {
                return 0;
            }

            return GetStart().CompareTo(interval.GetStart());
        }

        public bool Equals(LiveInterval interval)
        {
            return interval._data == _data;
        }

        public override bool Equals(object obj)
        {
            return obj is LiveInterval interval && Equals(interval);
        }

        public static bool operator ==(LiveInterval a, LiveInterval b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(LiveInterval a, LiveInterval b)
        {
            return !a.Equals(b);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((nint)_data);
        }

        public override string ToString()
        {
            LiveInterval self = this;

            IEnumerable<string> GetRanges()
            {
                LiveRange curr = self.CurrRange;

                while (curr != default)
                {
                    if (curr == self.CurrRange)
                    {
                        yield return "*" + curr;
                    }
                    else
                    {
                        yield return curr.ToString();
                    }

                    curr = curr.Next;
                }
            }

            return string.Join(", ", GetRanges());
        }
    }
}
