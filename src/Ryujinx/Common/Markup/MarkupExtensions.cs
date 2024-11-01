using Avalonia.Data.Core;
using Projektanker.Icons.Avalonia;
using Ryujinx.Ava.Common.Locale;

namespace Ryujinx.Ava.Common.Markup
{
    internal class IconExtension(string iconString) : BasicMarkupExtension
    {
        protected override ClrPropertyInfo PropertyInfo
            => new(
                "Item",
                _ => new Icon { Value = iconString },
                null,
                typeof(Icon)
            );
    }
    
    internal class SpinningIconExtension(string iconString) : BasicMarkupExtension
    {
        protected override ClrPropertyInfo PropertyInfo
            => new(
                "Item",
                _ => new Icon { Value = iconString, Animation = IconAnimation.Spin },
                null,
                typeof(Icon)
            );
    }
    
    internal class LocaleExtension(LocaleKeys key) : BasicMarkupExtension
    {
        protected override ClrPropertyInfo PropertyInfo
            => new(
                "Item",
                _ => LocaleManager.Instance[key],
                null,
                typeof(string)
            );
    }
}
