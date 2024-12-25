using System;
using Microsoft.Build.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Microsoft.Build.Framework;
using System.Text.Encodings.Web;

namespace Ryujinx.BuildValidationTasks
{
    public class LocalesValidationTask : Task
    {
        public override bool Execute()
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

            if (path.Split(["src"], StringSplitOptions.None).Length == 1)
            {
                //i assume that we are in a build directory in the solution dir
                path = new FileInfo(path).Directory!.Parent!.GetDirectories("src")[0].GetDirectories("Ryujinx")[0].GetDirectories("Assets")[0].GetFiles("locales.json")[0].FullName;
            }
            else
            {
                path = path.Split(["src"], StringSplitOptions.None)[0];
                path = new FileInfo(path).Directory!.GetDirectories("src")[0].GetDirectories("Ryujinx")[0].GetDirectories("Assets")[0].GetFiles("locales.json")[0].FullName;
            }

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
                Log.LogError($"Json Validation failed! {e.Message}");

                return false;
            }


            for (int i = 0; i < json.Locales.Count; i++)
            {
                LocalesEntry locale = json.Locales[i];

                foreach (string langCode in json.Languages.Where(it => !locale.Translations.ContainsKey(it)))
                {
                    locale.Translations.Add(langCode, string.Empty);
                    Log.LogMessage(MessageImportance.High, $"Added '{langCode}' to Locale '{locale.ID}'");
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
