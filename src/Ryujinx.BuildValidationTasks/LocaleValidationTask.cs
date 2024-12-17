using System;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Build.Framework;

namespace Ryujinx.BuildValidationTasks
{
    public class LocaleValidationTask : Task
    {
        public override bool Execute()
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

            path = new FileInfo(path).Directory.Parent.Parent.Parent.Parent.GetDirectories("Ryujinx")[0].GetDirectories("Assets")[0].GetFiles("locales.json")[0].FullName;

            string data;

            using (StreamReader sr = new StreamReader(path))
            {
                data = sr.ReadToEnd();
            }

            LocalesJSON json = JsonConvert.DeserializeObject<LocalesJSON>(data);

            for (int i = 0; i < json.Locales.Count; i++)
            {
                LocalesEntry locale = json.Locales[i];

                foreach (string language in json.Languages)
                {
                    if (!locale.Translations.ContainsKey(language))
                    {
                        locale.Translations.Add(language, "");
                        Log.LogMessage(MessageImportance.High, $"Added {{{language}}} to Locale {{{locale.ID}}}");
                    }
                }

                locale.Translations = locale.Translations.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                json.Locales[i] = locale;
            }

            string jsonString = JsonConvert.SerializeObject(json, Formatting.Indented);

            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.Write(jsonString);
            }

            return true;
        }

        struct LocalesJSON
        {
            public List<string> Languages { get; set; }
            public List<LocalesEntry> Locales { get; set; }
        }

        struct LocalesEntry
        {
            public string ID { get; set; }
            public Dictionary<string, string> Translations { get; set; }
        }
    }
}
