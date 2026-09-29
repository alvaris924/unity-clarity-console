using System.Collections.Generic;
using System.Linq;
using ClarityConsole.Capture;
using ClarityConsole.Core;
using ClarityConsole.Settings;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ClarityConsole.UI
{
    /// <summary>
    /// The window behind File > Session report: a <see cref="SessionReportView"/> for one Play session,
    /// the latest by default, with a picker for the earlier ones the console still holds. It opens by
    /// itself as Play mode ends when the "Report after Play" preference is on.
    /// </summary>
    internal sealed class SessionReportWindow : EditorWindow
    {
        [SerializeField]
        private int _session;

        private SessionReportView _view;
        private DropdownField _picker;
        private WindowTheme _windowTheme;

        /// <summary>Opens the report on the latest Play session the console holds.</summary>
        public static SessionReportWindow OpenLatest()
        {
            var window = GetWindow<SessionReportWindow>();
            window.titleContent = new GUIContent("Session report");
            window.minSize = new Vector2(320, 240);
            window.ShowSession(0);
            window.Show();
            return window;
        }

        /// <summary>The view inside the window, for tests.</summary>
        internal SessionReportView View => _view;

        /// <summary>Shows a session; zero means the latest.</summary>
        public void ShowSession(int session)
        {
            _session = session;
            Refresh();
        }

        private void OnDisable()
        {
            _windowTheme?.Detach();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.AddToClassList("cc-report-root");
            _windowTheme = new WindowTheme(root);

            var toolbar = new Toolbar();
            _picker = new DropdownField { name = "session-picker" };
            _picker.AddToClassList("cc-report-picker");
            _picker.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue.Replace("Session ", string.Empty), out int chosen))
                {
                    _session = chosen;
                    Refresh();
                }
            });
            toolbar.Add(_picker);
            toolbar.Add(new ToolbarSpacer { flex = true });
            var refresh = new ToolbarButton(Refresh) { text = "Refresh" };
            refresh.tooltip = "Read the session again, for a run that is still going.";
            toolbar.Add(refresh);
            root.Add(toolbar);

            _view = new SessionReportView();
            _view.EntryRequested += entry => ClarityConsoleWindow.Reveal(entry);
            _view.FlowRequested += entry => CallFlowWindow.Open(entry);
            root.Add(_view);
            Refresh();
        }

        private void Refresh()
        {
            if (_view == null)
            {
                return;
            }

            LogStore store = LogCaptureBootstrap.Store;
            List<int> sessions = SessionReport.SessionsIn(store.Entries);
            int session = _session > 0 && sessions.Contains(_session) ? _session : sessions.FirstOrDefault();

            _picker.choices = sessions.Select(s => "Session " + s).ToList();
            _picker.SetValueWithoutNotify(session > 0 ? "Session " + session : "No sessions");
            _picker.SetEnabled(sessions.Count > 1);

            _view.Show(session > 0 ? SessionReport.Build(store.Entries, session) : null);
        }
    }

    /// <summary>Opens the session report as Play mode ends, when the preference asks for it.</summary>
    [InitializeOnLoad]
    internal static class SessionReportAfterPlay
    {
        static SessionReportAfterPlay()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode && ConsolePreferences.ReportAfterPlay)
                {
                    // After the change settles, so the window does not open in the middle of the transition.
                    EditorApplication.delayCall += () => SessionReportWindow.OpenLatest();
                }
            };
        }
    }
}
