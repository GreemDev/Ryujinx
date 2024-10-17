using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Icon
{
    internal class IconExtension(string iconString) : MarkupExtension
    {
        private ClrPropertyInfo PropertyInfo
            => new(
                "Item",
                _ => new Projektanker.Icons.Avalonia.Icon { Value = iconString },
                null,
                typeof(Projektanker.Icons.Avalonia.Icon)
            );

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new CompiledBindingExtension(
                new CompiledBindingPathBuilder()
                    .Property(PropertyInfo, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                    .Build()
            ).ProvideValue(serviceProvider);
    }
}
