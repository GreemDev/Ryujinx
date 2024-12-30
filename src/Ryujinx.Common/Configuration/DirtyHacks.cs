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
        
        public ulong Pack() => BitTricks.PackBitFields([(uint)Hack, (uint)Value], PackedFormat);

        public static EnabledDirtyHack Unpack(ulong packedHack)
        {
            var unpackedFields = BitTricks.UnpackBitFields(packedHack, PackedFormat);
            if (unpackedFields is not [var hack, var value])
                throw new ArgumentException(nameof(packedHack));
            
            return new EnabledDirtyHack((DirtyHacks)hack, (int)value);
        }
    }

    public class DirtyHackCollection : Dictionary<DirtyHacks, int>
    {
        public DirtyHackCollection(EnabledDirtyHack[] hacks)
        {
            foreach ((DirtyHacks dirtyHacks, int value) in hacks)
            {
                Add(dirtyHacks, value);
            }
        }
        
        public DirtyHackCollection(ulong[] packedHacks)
        {
            foreach ((DirtyHacks dirtyHacks, int value) in packedHacks.Select(EnabledDirtyHack.Unpack))
            {
                Add(dirtyHacks, value);
            }
        }

        public ulong[] PackEntries() =>
            this
                .Select(it => 
                    BitTricks.PackBitFields([(uint)it.Key, (uint)it.Value], EnabledDirtyHack.PackedFormat))
                .ToArray();

        public static implicit operator DirtyHackCollection(EnabledDirtyHack[] hacks) => new(hacks);
        public static implicit operator DirtyHackCollection(ulong[] packedHacks) => new(packedHacks);

        public new int this[DirtyHacks hack] => TryGetValue(hack, out var value) ? value : -1;

        public bool IsEnabled(DirtyHacks hack) => ContainsKey(hack);
    }
}
