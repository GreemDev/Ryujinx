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
            Console.WriteLine("Running Locale Validation Task...");

            string path = projectPath + "src/Ryujinx/Assets/locales.json";
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
            catch (JsonException e)
            {
                throw new JsonException(e.Message); //shorter and easier stacktrace
            }

            bool isGitRunner = path.Contains("runner") || path.Contains("D:\\a\\Ryujinx\\Ryujinx");
            if (isGitRunner)
                Console.WriteLine("Is Git Runner!");
            bool encounteredLanguageIssue = false;

            for (int i = 0; i < json.Locales.Count; i++)
            {
                LocalesEntry locale = json.Locales[i];

                foreach (string langCode in json.Languages.Where(lang => !locale.Translations.ContainsKey(lang)))
                {
                    encounteredLanguageIssue = true;

                    if (!isGitRunner)
                    {
                        locale.Translations.Add(langCode, string.Empty);
                        Console.WriteLine($"Added '{langCode}' to Locale '{locale.ID}'");
                    }
                    else
                    {
                        Console.WriteLine($"Missing '{langCode}' in Locale '{locale.ID}'!");
                    }
                }

                foreach (string langCode in json.Languages.Where(lang => locale.Translations.ContainsKey(lang) && lang != "en_US" && locale.Translations[lang] == locale.Translations["en_US"]))
                {
                    encounteredLanguageIssue = true;

                    if (!isGitRunner)
                    {
                        locale.Translations[langCode] = string.Empty;
                        Console.WriteLine($"Lanugage '{langCode}' is a dupelicate of en_US in Locale '{locale.ID}'! Resetting it...");
                    }
                    else
                    {
                        Console.WriteLine($"Lanugage '{langCode}' is a dupelicate of en_US in Locale '{locale.ID}'!");
                    }
                }

                locale.Translations = locale.Translations.OrderBy(pair => pair.Key).ToDictionary(pair => pair.Key, pair => pair.Value);
                json.Locales[i] = locale;
            }

            if (isGitRunner && encounteredLanguageIssue)
                throw new JsonException("1 or more locales are invalid!");

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

            Console.WriteLine("Finished Locale Validation Task!");

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
