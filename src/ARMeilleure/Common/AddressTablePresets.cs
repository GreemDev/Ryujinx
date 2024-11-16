namespace ARMeilleure.Common
{
    public static class AddressTablePresets
    {
        private static readonly AddressTableLevel[] _levels64Bit =
            new AddressTableLevel[]
            {
                new(31, 17),
                new(23,  8),
                new(15,  8),
                new( 7,  8),
                new( 2,  5),
            };

        private static readonly AddressTableLevel[] _levels32Bit =
            new AddressTableLevel[]
            {
                new(31, 17),
                new(23,  8),
                new(15,  8),
                new( 7,  8),
                new( 1,  6),
            };

        private static readonly AddressTableLevel[] _levels64BitSparseTiny =
            new AddressTableLevel[]
            {
                new( 11, 28),
                new( 2, 9),
            };

        private static readonly AddressTableLevel[] _levels32BitSparseTiny =
            new AddressTableLevel[]
            {
                new( 10, 22),
                new( 1, 9),
            };

        public static AddressTableLevel[] GetArmPreset(bool for64Bits, bool sparse)
        {
            if (sparse)
            {
                    return for64Bits ? _levels64BitSparseGiant : _levels32BitSparseGiant;
            }
            else
            {
                return for64Bits ? _levels64Bit : _levels32Bit;
            }
        }
    }
}
