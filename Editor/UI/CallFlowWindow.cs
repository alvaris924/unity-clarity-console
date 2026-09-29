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
        private WindowTheme _windowTheme;

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

            ClarityConsoleSettings.Changed += OnSettingsChanged;
        }

        private void OnDisable()
        {
            ClarityConsoleSettings.Changed -= OnSettingsChanged;
            _windowTheme?.Detach();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("cc-flow-root");
            _windowTheme = new WindowTheme(root);

            _view = new CallFlowView
            {
                SnippetProvider = TryGetSnippet,
                FrameFilter = ClarityConsoleSettings.instance.CreateFrameFilter(),
            };
            _view.FrameActivated += OpenFrame;
            root.Add(_view);
            _view.Show(_entry);
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
