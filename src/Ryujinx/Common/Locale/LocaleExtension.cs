using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Locale
{
    internal class LocaleExtension(LocaleKeys key) : MarkupExtension
    {
        public LocaleKeys Key { get; } = key;

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var builder = new CompiledBindingPathBuilder();

            builder.Property(
                new ClrPropertyInfo("Item", 
                    _ => LocaleManager.Instance[Key], 
                    null, 
                    typeof(string)
                    ),
                PropertyInfoAccessorFactory.CreateInpcPropertyAccessor);

            var binding = new CompiledBindingExtension(builder.Build())
            {
                Source = LocaleManager.Instance
            };

            return binding.ProvideValue(serviceProvider);
        }
    }
}
