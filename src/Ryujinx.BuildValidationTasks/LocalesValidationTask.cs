using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace Ryujinx.BuildValidationTasks
{
    public class LocalesValidationTask : ValidationTask
    {
        public static bool Execute(string projectPath)
        {
            string path = projectPath + "src\\Ryujinx\\Assets\\locales.json";
            string data;

            using (StreamReader sr = new(path))
            {
                data = sr.ReadToEnd();
            }

            LocalesJson json;


            try
            {
                json = JsonSerializer.Deserialize<LocalesJson>(data);

            }
            catch (Exception e)
            {
                //Log.LogError($"Json Validation failed! {e.Message}");

                return false;
            }


            for (int i = 0; i < json.Locales.Count; i++)
            {
                LocalesEntry locale = json.Locales[i];

                foreach (string langCode in json.Languages.Where(it => !locale.Translations.ContainsKey(it)))
                {
                    locale.Translations.Add(langCode, string.Empty);
                    //Log.LogMessage(MessageImportance.High, $"Added '{langCode}' to Locale '{locale.ID}'");
                }

                locale.Translations = locale.Translations.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                json.Locales[i] = locale;
            }

            JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonString = JsonSerializer.Serialize(json, jsonOptions);

            using (StreamWriter sw = new(path))
            {
                sw.Write(jsonString);
            }

            return true;
        }

        struct LocalesJson
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
