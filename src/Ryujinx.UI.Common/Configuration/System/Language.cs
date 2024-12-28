using Ryujinx.Common.Utilities;
using Ryujinx.HLE.HOS.SystemState;
using System.Text.Json.Serialization;

namespace Ryujinx.UI.Common.Configuration.System
{
    [JsonConverter(typeof(TypedStringEnumConverter<Language>))]
    public enum Language
    {
        Japanese,
        AmericanEnglish,
        French,
        German,
        Italian,
        Spanish,
        Chinese,
        Korean,
        Dutch,
        Portuguese,
        Russian,
        Taiwanese,
        BritishEnglish,
        CanadianFrench,
        LatinAmericanSpanish,
        SimplifiedChinese,
        TraditionalChinese,
        BrazilianPortuguese,
    }
}
