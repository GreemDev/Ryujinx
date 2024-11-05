using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Projektanker.Icons.Avalonia;
using Ryujinx.Ava.Common.Locale;
using System;

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
    
    internal class LocaleExtension(LocaleKeys key) : MarkupExtension
    {
        private ClrPropertyInfo PropertyInfo
            => new(
                "Item",
                _ => LocaleManager.Instance[key],
                null,
                typeof(string)
            );
        
        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new CompiledBindingExtension(
                new CompiledBindingPathBuilder()
                    .Property(PropertyInfo, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                    .Build()
            ) { Source = LocaleManager.Instance }
                .ProvideValue(serviceProvider);
    }
}
