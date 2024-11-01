using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using System;

namespace Ryujinx.Ava.Common.Markup
{
    internal abstract class BasicMarkupExtension : MarkupExtension 
    {
        protected abstract ClrPropertyInfo PropertyInfo { get; }
        
        public override object ProvideValue(IServiceProvider serviceProvider) =>
            new CompiledBindingExtension(
                new CompiledBindingPathBuilder()
                    .Property(PropertyInfo, PropertyInfoAccessorFactory.CreateInpcPropertyAccessor)
                    .Build()
            ).ProvideValue(serviceProvider);
    }
}
