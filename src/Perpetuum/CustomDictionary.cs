using Newtonsoft.Json;
using Perpetuum.Log;

namespace Perpetuum
{
    public interface ICustomDictionary
    {
        Dictionary<string, object> GetDictionary(int language);
    }

    public class CustomDictionary : ICustomDictionary
    {
        /*
        Language IDs:
            0 = English
            1 = Hungarian
            2 = German
            3 = Portuguese
            4 = Russian
            5 = French
            6 = Spanish
            7 = Polish
            8 = Slovenian
            9 = Romanian
            10 = Norwegian
            11 = Greek
            12 = Finnish
            13 = Italian
            14 = Turkish
            15 = Estonian
            16 = Swedish
            17 = Dutch
        */

        private readonly int _defaultLanguage = 0;
        public Dictionary<int, Dictionary<string, object>> _dictionaries;

        public CustomDictionary(IO.IFileSystem fileManager)
        {
            _dictionaries = [];

            // if the directory doesn't exist or the JSON is malformed just throw an exception, log it and move on.
            // Otherwise we get a blank screen on the client.
            try
            {
                IEnumerable<string> files = fileManager.GetFiles("customDictionary", "*.json");

                foreach (string file in files)
                {
                    int languageID = Convert.ToInt32(System.IO.Path.GetFileNameWithoutExtension(file));
                    string settingsFile = fileManager.ReadAllText(file);
                    Dictionary<string, object>? dictionary = JsonConvert.DeserializeObject<Dictionary<string, object>>(settingsFile);
                    _dictionaries.Add(languageID, dictionary);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        public Dictionary<string, object>? GetDictionary(int language)
        {
            if (_dictionaries.ContainsKey(language))
            {
                return _dictionaries[language];
            }
            else if (_dictionaries.ContainsKey(_defaultLanguage))
            {
                return _dictionaries[_defaultLanguage];
            }
            return null;
        }
    }
}