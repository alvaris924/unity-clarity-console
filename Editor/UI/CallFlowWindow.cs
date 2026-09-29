using System;
using ClarityConsole.Core;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The window behind "Show flow": one <see cref="CallFlowView"/> for the entry it was opened on,
    /// styled with the console's theme and text size. It keeps the entry's message and stack through a
    /// domain reload, so a recompile does not empty it; it stays on its entry while the console's
    /// selection moves, which lets the reader compare the flow with other rows.
    /// </summary>
    internal sealed class CallFlowWindow : EditorWindow
    {
        private const long SheetRetryMs = 250;
        private const int SheetRetryLimit = 480;   // two minutes, as long as the console itself waits

        [SerializeField]
        private string _message;

        [SerializeField]
        private string _stackTrace;

        [SerializeField]
        private LogSeverity _severity;

        [SerializeField]
        private bool _hasEntry;

        private readonly SourceNavigator _navigator = new SourceNavigator();
        private readonly SourceCache _sources = new SourceCache();
        private LogEntry _entry;
        private CallFlowView _view;
        private StyleSheet _structureSheet;
        private StyleSheet _themeSheet;
        private ConsoleTheme _theme;
        private int _sheetAttempts;

        /// <summary>Opens the flow window, or brings it forward, showing <paramref name="entry"/>.</summary>
        public static CallFlowWindow Open(LogEntry entry)
        {
            var window = GetWindow<CallFlowWindow>();
            window.titleContent = new GUIContent("Flow");
            window.minSize = new Vector2(320, 200);
            window.ShowEntry(entry);
            window.Show();
            return window;
        }

        /// <summary>The view inside the window, for tests.</summary>
        internal CallFlowView View => _view;

        public void ShowEntry(LogEntry entry)
        {
            _entry = entry;
            _hasEntry = entry != null;
            _message = entry?.Message;
            _stackTrace = entry?.StackTrace;
            _severity = entry?.Severity ?? LogSeverity.Log;
            _view?.Show(entry);
        }

        private void OnEnable()
        {
            if (_entry == null && _hasEntry)
            {
                // After a domain reload only the serialized text is left; the flow needs nothing else.
                _entry = new LogEntry(LogEntryKind.Log, _severity, _message, _stackTrace, DateTime.UtcNow, 0, 0, true, ObjectRef.None);
            }

            ConsolePreferences.Changed += OnPreferencesChanged;
            ClarityConsoleSettings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            ConsolePreferences.Changed -= OnPreferencesChanged;
            ClarityConsoleSettings.Changed -= OnSettingsChanged;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("cc-root");
            root.AddToClassList("cc-flow-root");
            ApplyTheme(ConsoleThemes.Find(ConsolePreferences.Theme));
            if (!TryLoadStructureSheet())
            {
                root.schedule.Execute(() => TryLoadStructureSheet()).Every(SheetRetryMs).Until(() => _structureSheet != null || ++_sheetAttempts > SheetRetryLimit);
            }

            root.style.fontSize = ConsolePreferences.TextSize;

            _view = new CallFlowView
            {
                SnippetProvider = TryGetSnippet,
                FrameFilter = ClarityConsoleSettings.instance.CreateFrameFilter(),
            };
            _view.FrameActivated += OpenFrame;
            root.Add(_view);
            _view.Show(_entry);
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

            // The theme sheet must come after the structure sheet so its rules keep precedence.
            VisualElement root = rootVisualElement;
            root.styleSheets.Add(_structureSheet);
            if (_themeSheet == null && _theme != null)
            {
                _themeSheet = _theme.Load();
            }

            if (_themeSheet != null)
            {
                root.styleSheets.Remove(_themeSheet);
                root.styleSheets.Add(_themeSheet);
            }

            return true;
        }

        private void ApplyTheme(ConsoleTheme theme)
        {
            VisualElement root = rootVisualElement;
            if (_themeSheet != null && root.styleSheets.Contains(_themeSheet))
            {
                root.styleSheets.Remove(_themeSheet);
            }

            if (_theme != null)
            {
                root.RemoveFromClassList("cc-theme-" + _theme.Id);
            }

            _theme = theme;
            _themeSheet = theme.Load();
            if (_themeSheet != null)
            {
                root.styleSheets.Add(_themeSheet);
            }

            root.AddToClassList("cc-theme-" + theme.Id);
        }

        private void OnPreferencesChanged()
        {
            if (_view == null)
            {
                return;
            }

            rootVisualElement.style.fontSize = ConsolePreferences.TextSize;
            ConsoleTheme wanted = ConsoleThemes.Find(ConsolePreferences.Theme);
            if (!ReferenceEquals(wanted, _theme))
            {
                ApplyTheme(wanted);
            }
        }

        private void OnSettingsChanged()
        {
            if (_view == null)
            {
                return;
            }

            _sources.Clear();
            _view.FrameFilter = ClarityConsoleSettings.instance.CreateFrameFilter();
        }

        private SourceSnippet TryGetSnippet(TraceFrame frame)
        {
            return _navigator.TryResolveAbsolutePath(frame, out string absolutePath)
                ? _sources.TryGet(absolutePath, frame.Line, ClarityConsoleSettings.instance.SourcePreviewRadius)
                : null;
        }

        private void OpenFrame(TraceFrame frame)
        {
            if (!_navigator.TryOpen(frame))
            {
                ShowNotification(new GUIContent("Could not open " + frame.FilePath + ":" + frame.Line));
            }
        }
    }
}
