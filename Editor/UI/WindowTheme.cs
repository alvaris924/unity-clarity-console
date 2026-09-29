using ClarityConsole.Settings;
using UnityEditor;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// Gives a secondary window, such as the flow or the session report, the console's look: the
    /// structure sheet, the chosen theme after it so its rules keep precedence, and the text size, all
    /// following the preferences as they change. The sheets are package assets, so a window restored
    /// while the package is still importing retries for a while, as the console itself does.
    /// </summary>
    internal sealed class WindowTheme
    {
        private const long RetryMs = 250;
        private const int RetryLimit = 480;   // two minutes

        private readonly VisualElement _root;
        private StyleSheet _structureSheet;
        private StyleSheet _themeSheet;
        private ConsoleTheme _theme;
        private int _attempts;

        public WindowTheme(VisualElement root)
        {
            _root = root;
            _root.AddToClassList("cc-root");
            Apply();
            if (!TryLoadStructureSheet())
            {
                _root.schedule.Execute(() => TryLoadStructureSheet()).Every(RetryMs).Until(() => _structureSheet != null || ++_attempts > RetryLimit);
            }

            ConsolePreferences.Changed += Apply;
        }

        /// <summary>The theme currently applied.</summary>
        public ConsoleTheme Theme => _theme;

        /// <summary>Stops following the preferences; call from the window's OnDisable.</summary>
        public void Detach()
        {
            ConsolePreferences.Changed -= Apply;
        }

        private void Apply()
        {
            _root.style.fontSize = ConsolePreferences.TextSize;
            ConsoleTheme wanted = ConsoleThemes.Find(ConsolePreferences.Theme);
            if (ReferenceEquals(wanted, _theme))
            {
                return;
            }

            if (_themeSheet != null && _root.styleSheets.Contains(_themeSheet))
            {
                _root.styleSheets.Remove(_themeSheet);
            }

            if (_theme != null)
            {
                _root.RemoveFromClassList("cc-theme-" + _theme.Id);
            }

            _theme = wanted;
            _themeSheet = wanted.Load();
            if (_themeSheet != null)
            {
                _root.styleSheets.Add(_themeSheet);
            }

            _root.AddToClassList("cc-theme-" + wanted.Id);
        }

        private bool TryLoadStructureSheet()
        {
            if (_structureSheet != null)
            {
                return true;
            }

            _structureSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ClarityConsoleWindow.StyleSheetPath);
            if (_structureSheet == null)
            {
                return false;
            }

            _root.styleSheets.Add(_structureSheet);
            if (_themeSheet == null && _theme != null)
            {
                _themeSheet = _theme.Load();
            }

            if (_themeSheet != null)
            {
                // Re-adding moves the theme after the structure sheet.
                _root.styleSheets.Remove(_themeSheet);
                _root.styleSheets.Add(_themeSheet);
            }

            return true;
        }
    }
}
