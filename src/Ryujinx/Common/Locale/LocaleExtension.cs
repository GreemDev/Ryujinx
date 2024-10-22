using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Locale
{
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
                )
            { Source = LocaleManager.Instance }
                .ProvideValue(serviceProvider);
    }
}
