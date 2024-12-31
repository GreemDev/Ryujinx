using Gommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.Common.Configuration
{
    [Flags]
    public enum DirtyHacks : byte
    {
        Xc2MenuSoftlockFix = 1,
        ShaderCompilationThreadSleep = 2
    }

    public record EnabledDirtyHack(DirtyHacks Hack, int Value)
    {
        public static readonly byte[] PackedFormat = [8, 32];

        private uint[] Raw => [(uint)Hack, (uint)Value.CoerceAtLeast(0)];

        public ulong Pack() => Raw.PackBitFields(PackedFormat);

        public static EnabledDirtyHack Unpack(ulong packedHack)
        {
            var unpackedFields = packedHack.UnpackBitFields(PackedFormat);
            if (unpackedFields is not [var hack, var value])
                throw new ArgumentException(nameof(packedHack));
            
            return new EnabledDirtyHack((DirtyHacks)hack, (int)value);
        }
    }

    public class DirtyHackCollection : Dictionary<DirtyHacks, int>
    {
        public DirtyHackCollection(IEnumerable<EnabledDirtyHack> hacks) 
            => hacks.ForEach(edh => Add(edh.Hack, edh.Value));

        public DirtyHackCollection(ulong[] packedHacks) : this(packedHacks.Select(EnabledDirtyHack.Unpack)) {}

        public ulong[] PackEntries() 
            => Entries.Select(it => it.Pack()).ToArray();
        
        public EnabledDirtyHack[] Entries 
            => this
                .Select(it => new EnabledDirtyHack(it.Key, it.Value))
                .ToArray();

        public static implicit operator DirtyHackCollection(EnabledDirtyHack[] hacks) => new(hacks);
        public static implicit operator DirtyHackCollection(ulong[] packedHacks) => new(packedHacks);

        public new int this[DirtyHacks hack] => TryGetValue(hack, out var value) ? value : -1;

        public bool IsEnabled(DirtyHacks hack) => ContainsKey(hack);
    }
}
