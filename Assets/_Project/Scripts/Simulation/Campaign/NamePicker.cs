using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Simulation.Campaign
{
    public enum NameKeyKind
    {
        /// <summary>Keep the lot's old name, and with it its old reviews.</summary>
        Keep,
        Suggestion,
        Letter,
        Space,
        Delete,
        Done,
    }

    /// <summary>One key of the name picker; <see cref="Text"/> is the letter, the name, or empty.</summary>
    public readonly struct NameKey
    {
        public NameKey(NameKeyKind kind, string text = "")
        {
            Kind = kind;
            Text = text ?? "";
        }

        public NameKeyKind Kind { get; }
        public string Text { get; }
    }

    /// <summary>
    /// Naming a restaurant with a controller (user's call, 2026-11-10): the old name on the top row,
    /// a few suggestions on the next, then a letter grid, then space, delete and done. The stick moves a cursor
    /// over the keys and A presses one. Choosing the old name or a suggestion is the answer at once;
    /// typed names finish with Done. Letters are capitalised at the start of each word, so the grid
    /// shows only capitals and names come out as "Casa Lucia".
    /// </summary>
    public sealed class NamePicker
    {
        /// <summary>The letter rows: 7 keys each, so the grid is wider than tall and reads at a glance.</summary>
        public static readonly string[] LetterRows = { "ABCDEFG", "HIJKLMN", "OPQRSTU", "VWXYZ&'" };

        private readonly List<List<NameKey>> _rows = new List<List<NameKey>>();
        private readonly string _oldName;
        private readonly int _maxLength;
        private string _text = "";

        /// <param name="oldName">The lot's name; empty for none, which leaves out the keep key.</param>
        /// <param name="maxLength">The longest name the sign and the card fit.</param>
        public NamePicker(string oldName, IEnumerable<string> suggestions, int maxLength)
        {
            _oldName = RestaurantNaming.Clean(oldName);
            _maxLength = Math.Max(1, maxLength);
            _rows.Add(new List<NameKey>()); // the old name
            _rows.Add(new List<NameKey>()); // suggestions
            foreach (var letters in LetterRows)
                _rows.Add(letters.Select(c => new NameKey(NameKeyKind.Letter, c.ToString())).ToList());
            _rows.Add(new List<NameKey> { new NameKey(NameKeyKind.Space), new NameKey(NameKeyKind.Delete), new NameKey(NameKeyKind.Done) });
            SetSuggestions(suggestions);
        }

        /// <summary>
        /// The keys, top row first: the old name, suggestions, the letters, then space, delete and
        /// done. A row with nothing in it (no old name, no suggestions) is empty and skipped.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<NameKey>> Rows => _rows;

        public int Row { get; private set; }
        public int Column { get; private set; }

        public NameKey Selected => _rows[Row][Column];

        /// <summary>What has been typed so far.</summary>
        public string Text => _text;

        public int MaxLength => _maxLength;

        /// <summary>True if Done would give a name: something other than spaces has been typed.</summary>
        public bool CanFinish => RestaurantNaming.Clean(_text).Length > 0;

        /// <summary>Replaces the suggestions (Y rolls new ones); the cursor stays where it was.</summary>
        public void SetSuggestions(IEnumerable<string> suggestions)
        {
            _rows[0].Clear();
            if (_oldName.Length > 0)
                _rows[0].Add(new NameKey(NameKeyKind.Keep, _oldName));
            _rows[1].Clear();
            foreach (var name in (suggestions ?? Enumerable.Empty<string>()).Select(RestaurantNaming.Clean).Where(n => n.Length > 0))
                _rows[1].Add(new NameKey(NameKeyKind.Suggestion, Fit(name)));
            Clamp();
        }

        /// <summary>
        /// Moves the cursor: <paramref name="dx"/> along a row (wrapping round), <paramref name="dy"/>
        /// between rows (+1 is down, stopping at the top and bottom). False if it didn't move.
        /// </summary>
        public bool Move(int dx, int dy)
        {
            var row = Row;
            var column = Column;
            if (dy != 0)
            {
                // Keep the same place across: the middle of a short row lines up with the middle of a long one.
                var from = _rows[Row];
                var across = from.Count > 1 ? (float)Column / (from.Count - 1) : 0.5f;
                var next = Row + Math.Sign(dy);
                while (next >= 0 && next < _rows.Count && _rows[next].Count == 0)
                    next += Math.Sign(dy);
                if (next >= 0 && next < _rows.Count)
                {
                    Row = next;
                    var to = _rows[Row];
                    Column = (int)Math.Round(across * (to.Count - 1));
                }
            }
            if (dx != 0)
            {
                var count = _rows[Row].Count;
                Column = ((Column + Math.Sign(dx)) % count + count) % count;
            }
            Clamp();
            return row != Row || column != Column;
        }

        /// <summary>
        /// Presses the key under the cursor. Returns the chosen name when the choice is made (keep,
        /// a suggestion, or Done with something typed), otherwise null.
        /// </summary>
        public string Press()
        {
            var key = Selected;
            switch (key.Kind)
            {
                case NameKeyKind.Keep:
                case NameKeyKind.Suggestion:
                    return key.Text;
                case NameKeyKind.Letter:
                    Type(key.Text);
                    return null;
                case NameKeyKind.Space:
                    if (_text.Length > 0 && !_text.EndsWith(" ") && _text.Length < _maxLength)
                        _text += " ";
                    return null;
                case NameKeyKind.Delete:
                    Delete();
                    return null;
                case NameKeyKind.Done:
                    return CanFinish ? RestaurantNaming.Clean(_text) : null;
                default:
                    return null;
            }
        }

        /// <summary>Takes the last letter off (X, or the delete key).</summary>
        public bool Delete()
        {
            if (_text.Length == 0)
                return false;
            _text = _text.Substring(0, _text.Length - 1);
            return true;
        }

        /// <summary>Adds a letter, capitalised at the start of a word; nothing past the longest name.</summary>
        public bool Type(string letter)
        {
            if (string.IsNullOrEmpty(letter) || _text.Length + letter.Length > _maxLength)
                return false;
            var wordStart = _text.Length == 0 || _text.EndsWith(" ");
            _text += wordStart ? letter.ToUpperInvariant() : letter.ToLowerInvariant();
            return true;
        }

        private string Fit(string name) => name.Length <= _maxLength ? name : name.Substring(0, _maxLength).TrimEnd();

        private void Clamp()
        {
            Row = Math.Max(0, Math.Min(_rows.Count - 1, Row));
            while (_rows[Row].Count == 0)
                Row++; // nothing to pick here: the next row down (the letters are never empty)
            Column = Math.Max(0, Math.Min(_rows[Row].Count - 1, Column));
        }
    }
}
