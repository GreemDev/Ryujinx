namespace Ryujinx.HLE.HOS.SystemState
{
    // nn::settings::KeyboardLayout
    public enum KeyboardLayout
    {
        Default = 0,
        EnglishUs,
        EnglishUsInternational,
        EnglishUk,
        French,
        FrenchCa,
        Spanish,
        SpanishLatin,
        German,
        Italian,
        Portuguese,
        Russian,
        Korean,
        ChineseSimplified,
        ChineseTraditional,
        Swedish,
        Finnish,

        Min = Default,
        Max = ChineseTraditional,
    }
}
