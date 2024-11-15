using Microsoft.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Ryujinx.UI.LocaleGenerator
{
    [Generator]
    public class LocaleGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var englishLocaleFile = context.AdditionalTextsProvider.Where(static x => x.Path.EndsWith("en_US.json"));

            IncrementalValuesProvider<string> contents = englishLocaleFile.Select((text, cancellationToken) => text.GetText(cancellationToken)!.ToString());

            context.RegisterSourceOutput(contents, (spc, content) =>
            {
                var lines = content.Split('\n').Where(x => x.Trim().StartsWith("\"")).Select(x => x.Split(':')[0].Trim().Replace("\"", string.Empty));
                StringBuilder enumSourceBuilder = new();
                enumSourceBuilder.AppendLine("namespace Ryujinx.Ava.Common.Locale;");
                enumSourceBuilder.AppendLine("internal enum LocaleKeys");
                enumSourceBuilder.AppendLine("{");
                foreach (var line in lines)
                {
                    enumSourceBuilder.AppendLine($"    {line},");
                }
                enumSourceBuilder.AppendLine("}");

                spc.AddSource("LocaleKeys", enumSourceBuilder.ToString());
            });
        }
    }
}
