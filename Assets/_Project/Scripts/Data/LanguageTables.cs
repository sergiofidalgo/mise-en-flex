using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Data
{
    /// <summary>The languages the game offers: each one's code, its own name for itself, and its table.</summary>
    [CreateAssetMenu(menuName = "Game/Language Tables", fileName = "LanguageTables")]
    public sealed class LanguageTables : ScriptableObject
    {
        [Serializable]
        public sealed class Language
        {
            [Tooltip("ISO code, e.g. es.")]
            [SerializeField] private string _code;
            [Tooltip("The language's own name for itself, as Settings lists it: Español.")]
            [SerializeField] private string _name;
            [Tooltip("CSV: english, translation, note. English needs none.")]
            [SerializeField] private TextAsset _table;

            public Language() { }

            public Language(string code, string name, TextAsset table)
            {
                _code = code;
                _name = name;
                _table = table;
            }

            public string Code => _code;
            public string Name => _name;
            public TextAsset Table => _table;
        }

        [SerializeField] private Language[] _languages = new Language[0];

        private bool _loaded;

        public IReadOnlyList<Language> Languages => _languages;

        /// <summary>The languages players can pick, English first; the pseudo-language too in the Editor and development builds.</summary>
        public IEnumerable<(string Code, string Name)> Choices()
        {
            yield return ("en", "English");
            foreach (var language in _languages.Where(l => l != null && l.Code != "en"))
                yield return (language.Code, language.Name);
            if (Application.isEditor || Debug.isDebugBuild)
                yield return (Translations.Pseudo, "Pseudo (testing)");
        }

        /// <summary>Loads every language's table once.</summary>
        public void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            foreach (var language in _languages)
            {
                if (language?.Table != null)
                    Translations.LoadCsv(language.Code, language.Table.text);
            }
        }

        /// <summary>Loads again, after a table changed (the Editor tool).</summary>
        public void Reload()
        {
            _loaded = false;
            foreach (var language in _languages)
            {
                if (language != null)
                    Translations.Clear(language.Code);
            }
            Load();
        }

        private void OnEnable() => _loaded = false;
    }
}
