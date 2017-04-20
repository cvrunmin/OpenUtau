using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenUtau.Lang
{
    static class LanguageManager
    {
        private static readonly Dictionary<string, ResourceDictionary> Languages = new Dictionary<string, ResourceDictionary>();
        static private readonly ResourceDictionary DefaultLanguage = LoadLangFromResource("pack://application:,,,/Lang/en-us.xaml");

        private static ResourceDictionary LoadLangFromResource(string uri)
        {
            return new ResourceDictionary() { Source = new Uri(uri) };
        }

        static public void Add(string languageName, string languageUrl)
        {
            if (Languages.ContainsKey(languageName))
            {
                Languages[languageName] = LoadLangFromResource(languageUrl);
                return;
            }
            Languages.Add(languageName, LoadLangFromResource(languageUrl));

        }
        static public void Clear()
        {
            Languages.Clear();
        }
        static public string GetLocalized(string key)
        {
            if (Application.Current.Resources.Contains(key))
                return Application.Current.Resources[key].ToString();
            if (DefaultLanguage.Contains(key))
                return DefaultLanguage[key].ToString();
            return key;
        }

        static public void UseLanguage(string languageName)
        {
            if (!Languages.ContainsKey(languageName))
            {
                Application.Current.Resources.MergedDictionaries.Remove(Languages[Core.Util.Preferences.Default.Language]);
                Application.Current.Resources.MergedDictionaries.Add(DefaultLanguage);
                return;
            }
            var langType = Languages[languageName];
            if (langType != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(Languages[Core.Util.Preferences.Default.Language]);
                Application.Current.Resources.MergedDictionaries.Add(langType);
            }
        }

        public static string[] ListLanuage()
        {
            var langs = new string[Languages.Count];
            var i = 0;
            foreach (var lang in Languages)
            {
                langs[i] = (string)lang.Value["DisplayName"];
                i++;
            }
            return langs;
        }
    }
}
