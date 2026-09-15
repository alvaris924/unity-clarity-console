using System;
using System.Collections.Generic;
using System.IO;
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
    /// The console window: a virtualized multi-column list over <see cref="ConsoleViewModel"/>, severity
    /// toggles with live counts, search, collapse, a detail pane and a status bar. The element tree is
    /// built in C# so Unity 2022.3 and 6000.x share one code path.
    /// </summary>
    internal sealed class ClarityConsoleWindow : EditorWindow
    {
        private const string StyleSheetPath = "Packages/com.alvaris.clarity-console/Editor/UI/ClarityConsole.uss";
        private const int RowHeight = 20;
        private const long RefreshDelayMs = 50;
        private const long SearchDelayMs = 100;
        private const long CountsIntervalMs = 500;
        private const double NoticeSeconds = 5;

        private ConsoleViewModel _viewModel;
        private MultiColumnListView _list;
        private ScrollView _listScrollView;
        private ToolbarToggle _collapseToggle;
        private ToolbarToggle _logToggle;
        private ToolbarToggle _warningToggle;
        private ToolbarToggle _errorToggle;
        private ToolbarToggle _errorPauseToggle;
        private ToolbarSearchField _searchField;
        private ChannelBar _channels;
        private DetailView _detail;
        private readonly SourceNavigator _navigator = new SourceNavigator();
        private readonly SourceCache _sources = new SourceCache();
        private Label _status;
        private Texture _logIcon;
        private Texture _warningIcon;
        private Texture _errorIcon;
        private IVisualElementScheduledItem _refresh;
        private IVisualElementScheduledItem _searchDebounce;
        private bool _stickToBottom = true;
        private string _notice;
        private double _noticeUntil;

        [MenuItem("Window/Clarity Console")]
        public static void Open()
        {
            ClarityConsoleWindow window = GetWindow<ClarityConsoleWindow>();
            window.titleContent = new GUIContent("Clarity Console");
            window.Show();
        }

        public ConsoleViewModel ViewModel => _viewModel;

        /// <summary>Applies pending view-model changes to the list immediately. Test hook.</summary>
        internal void RefreshNow()
        {
            FlushRefresh();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Clarity Console");
            _viewModel = new ConsoleViewModel(LogCaptureBootstrap.Store);
            _viewModel.Changed += OnViewChanged;
        }

        private void OnDisable()
        {
            if (_viewModel == null)
            {
                return;
            }

            ClarityConsoleSettings.Changed -= OnSettingsChanged;
            ConsolePreferences.Changed -= OnPreferencesChanged;
            _viewModel.Changed -= OnViewChanged;
            _viewModel.Dispose();
            _viewModel = null;
        }

        private void CreateGUI()
        {
            LoadIcons();

            VisualElement root = rootVisualElement;
            root.AddToClassList("cc-root");
            root.AddToClassList(EditorGUIUtility.isProSkin ? "cc-dark" : "cc-light");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            root.Add(BuildToolbar());

            _channels = new ChannelBar();
            _channels.ChannelToggled += OnChannelToggled;
            root.Add(_channels);

            var split = new TwoPaneSplitView(1, 140, TwoPaneSplitViewOrientation.Vertical);
            split.AddToClassList("cc-split");
            _list = BuildList();
            split.Add(_list);
            _detail = new DetailView();
            _detail.FrameActivated += OpenFrame;
            _detail.SnippetProvider = TryGetSnippet;
            _detail.FrameFilter = ClarityConsoleSettings.instance.CreateFrameFilter();
            _viewModel.IgnoreList = ClarityConsoleSettings.instance.CreateIgnoreList();
            ClarityConsoleSettings.Changed += OnSettingsChanged;
            ConsolePreferences.Changed += OnPreferencesChanged;
            split.Add(_detail);
            root.Add(split);

            _status = new Label();
            _status.AddToClassList("cc-status");
            root.Add(_status);

            _listScrollView = _list.Q<ScrollView>();
            if (_listScrollView != null)
            {
                _listScrollView.verticalScroller.valueChanged += OnListScrolled;
            }

            _list.itemsSource = _viewModel.Visible;
            root.schedule.Execute(UpdateCounts).Every(CountsIntervalMs);
            UpdateCounts();
            RefreshChannels();
            RequestRefresh();
        }

        private Toolbar BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("cc-toolbar");

            toolbar.Add(new ToolbarButton(() => _viewModel.Store.Clear()) { text = "Clear" });

            var clearOptions = new ToolbarMenu { text = string.Empty };
            clearOptions.AddToClassList("cc-clear-options");
            clearOptions.tooltip = "When the console should empty itself.";
            AppendPreferenceToggle(clearOptions, "Clear on Play", () => ConsolePreferences.ClearOnPlay, v => ConsolePreferences.ClearOnPlay = v);
            AppendPreferenceToggle(clearOptions, "Clear on Recompile", () => ConsolePreferences.ClearOnRecompile, v => ConsolePreferences.ClearOnRecompile = v);
            AppendPreferenceToggle(clearOptions, "Clear on Build", () => ConsolePreferences.ClearOnBuild, v => ConsolePreferences.ClearOnBuild = v);
            toolbar.Add(clearOptions);

            _errorPauseToggle = new ToolbarToggle { text = "Error Pause", value = ConsolePreferences.ErrorPause };
            _errorPauseToggle.tooltip = "Pause Play mode as soon as an error, exception or assertion is logged.";
            _errorPauseToggle.RegisterValueChangedCallback(evt => ConsolePreferences.ErrorPause = evt.newValue);
            toolbar.Add(_errorPauseToggle);

            _collapseToggle = new ToolbarToggle { text = "Collapse" };
            _collapseToggle.RegisterValueChangedCallback(evt => _viewModel.Collapse = evt.newValue);
            toolbar.Add(_collapseToggle);

            var export = new ToolbarMenu { text = "Export" };
            export.tooltip = "Write the rows currently shown, filters and all, to a file.";
            export.menu.AppendAction("Save as text…", _ => SaveVisible(ExportFormat.Text));
            export.menu.AppendAction("Save as Markdown…", _ => SaveVisible(ExportFormat.Markdown));
            export.menu.AppendAction("Save as JSON…", _ => SaveVisible(ExportFormat.Json));
            export.menu.AppendSeparator();
            export.menu.AppendAction("Copy all shown", _ => CopyVisible());
            toolbar.Add(export);

            toolbar.Add(new ToolbarSpacer { flex = true });

            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("cc-search");
            _searchField.tooltip =
                "Search terms are joined by AND. \"quoted phrase\" matches exactly, -term excludes, " +
                "/regex/i matches a pattern, sev:error,warn and tag:PlayFab* filter, in:stack also searches " +
                "stack traces, and A OR B matches either.";
            _searchField.RegisterValueChangedCallback(_ => ScheduleSearch());
            toolbar.Add(_searchField);

            _logToggle = BuildSeverityToggle(_logIcon, "cc-toggle-log", visible => _viewModel.SetSeverityVisible(LogSeverity.Log, visible));
            _warningToggle = BuildSeverityToggle(_warningIcon, "cc-toggle-warning", visible => _viewModel.SetSeverityVisible(LogSeverity.Warning, visible));
            _errorToggle = BuildSeverityToggle(_errorIcon, "cc-toggle-error", visible =>
            {
                _viewModel.SetSeverityVisible(LogSeverity.Error, visible);
                _viewModel.SetSeverityVisible(LogSeverity.Exception, visible);
                _viewModel.SetSeverityVisible(LogSeverity.Assert, visible);
            });
            toolbar.Add(_logToggle);
            toolbar.Add(_warningToggle);
            toolbar.Add(_errorToggle);

            return toolbar;
        }

        /// <summary>A menu item that reads and writes a preference, showing a tick when it is on.</summary>
        private static void AppendPreferenceToggle(ToolbarMenu menu, string label, Func<bool> get, Action<bool> set)
        {
            menu.menu.AppendAction(
                label,
                _ => set(!get()),
                _ => get() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private static ToolbarToggle BuildSeverityToggle(Texture icon, string className, Action<bool> onChanged)
        {
            var toggle = new ToolbarToggle { value = true, text = "0" };
            toggle.AddToClassList("cc-toggle");
            toggle.AddToClassList(className);
            if (icon != null)
            {
                var image = new Image { image = icon };
                image.AddToClassList("cc-toggle-icon");
                toggle.Insert(0, image);
            }

            toggle.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return toggle;
        }

        private MultiColumnListView BuildList()
        {
            var list = new MultiColumnListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.AddToClassList("cc-list");

            list.columns.Add(new Column
            {
                name = "severity",
                title = string.Empty,
                width = 24,
                resizable = false,
                makeCell = MakeIconCell,
                bindCell = BindSeverityCell,
            });
            list.columns.Add(new Column
            {
                name = "time",
                title = "Time",
                width = 96,
                makeCell = MakeLabelCell,
                bindCell = (cell, index) => ((Label)cell).text = _viewModel.Visible[index].TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
            });
            list.columns.Add(new Column
            {
                name = "frame",
                title = "Frame",
                width = 64,
                makeCell = MakeLabelCell,
                bindCell = BindFrameCell,
            });
            list.columns.Add(new Column
            {
                name = "message",
                title = "Message",
                minWidth = 200,
                stretchable = true,
                makeCell = MakeMessageCell,
                bindCell = BindMessageCell,
            });
            list.columns.Add(new Column
            {
                name = "count",
                title = string.Empty,
                width = 48,
                resizable = false,
                makeCell = MakeLabelCell,
                bindCell = BindCountCell,
            });

            list.selectionChanged += OnSelectionChanged;
            list.itemsChosen += OnItemsChosen;
            return list;
        }

        private static VisualElement MakeLabelCell()
        {
            var label = new Label();
            label.AddToClassList("cc-cell");
            return label;
        }

        /// <summary>
        /// A message cell carries its entry in <c>userData</c> so the context menu, which is attached once
        /// per recycled cell, always acts on the row under the pointer.
        /// </summary>
        private VisualElement MakeMessageCell()
        {
            var label = new Label();
            label.AddToClassList("cc-cell");
            label.AddManipulator(new ContextualMenuManipulator(evt => BuildRowMenu(evt, label.userData as LogEntry)));
            return label;
        }

        private void BuildRowMenu(ContextualMenuPopulateEvent evt, LogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            evt.menu.AppendAction("Copy message", _ => EditorGUIUtility.systemCopyBuffer = entry.Message);
            evt.menu.AppendAction(
                "Copy message and stack",
                _ => EditorGUIUtility.systemCopyBuffer = entry.StackTrace.Length == 0
                    ? entry.Message
                    : entry.Message + Environment.NewLine + Environment.NewLine + entry.StackTrace);

            if (entry.Kind == LogEntryKind.Marker)
            {
                return;
            }

            evt.menu.AppendAction("Copy for a bug report", _ => CopyForBugReport(entry));

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Ignore this message", _ => AddIgnoreRule(IgnoreMatch.Message, entry.Message));
            if (entry.Channel.Length > 0)
            {
                evt.menu.AppendAction("Ignore channel " + entry.Channel, _ => AddIgnoreRule(IgnoreMatch.Channel, entry.Channel));
            }

            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Manage ignore rules…", _ => SettingsService.OpenProjectSettings("Project/Clarity Console"));
        }

        /// <summary>Writes the rows currently shown to a file the user picks.</summary>
        private void SaveVisible(ExportFormat format)
        {
            string extension = LogExporter.ExtensionFor(format);
            string suggested = "console-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "." + extension;
            string path = EditorUtility.SaveFilePanel("Export console", string.Empty, suggested, extension);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.WriteAllText(path, Export(format));
                ShowNotice("Exported " + _viewModel.Visible.Count + " rows to " + Path.GetFileName(path));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowNotice("Could not write " + Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        private void CopyVisible()
        {
            EditorGUIUtility.systemCopyBuffer = Export(ExportFormat.Text);
            ShowNotice("Copied " + _viewModel.Visible.Count + " rows to the clipboard.");
        }

        private void CopyForBugReport(LogEntry entry)
        {
            var options = new ExportOptions
            {
                Format = ExportFormat.Text,
                IncludeStackTraces = true,
                Header = ExportContext.Describe(_viewModel),
            };

            EditorGUIUtility.systemCopyBuffer = LogExporter.Export(new[] { entry }, options);
            ShowNotice("Copied the entry with its stack and this Editor's details.");
        }

        private string Export(ExportFormat format)
        {
            return LogExporter.Export(_viewModel.Visible, new ExportOptions
            {
                Format = format,
                IncludeStackTraces = true,
                Header = ExportContext.Describe(_viewModel),
            });
        }

        private void AddIgnoreRule(IgnoreMatch match, string pattern)
        {
            if (ClarityConsoleSettings.instance.AddIgnoreRule(match, pattern))
            {
                ShowNotice("Ignoring " + new IgnoreRule(match, pattern).Describe() + ". Manage rules in Project Settings.");
            }
        }

        private static VisualElement MakeIconCell()
        {
            var image = new Image();
            image.AddToClassList("cc-cell-icon");
            return image;
        }

        private void BindSeverityCell(VisualElement cell, int index)
        {
            LogEntry entry = _viewModel.Visible[index];
            ((Image)cell).image = entry.Kind == LogEntryKind.Marker ? null : IconFor(entry.Severity);
        }

        private void BindFrameCell(VisualElement cell, int index)
        {
            LogEntry entry = _viewModel.Visible[index];
            ((Label)cell).text = entry.Kind == LogEntryKind.Marker ? string.Empty : entry.Frame.ToString();
        }

        private void BindMessageCell(VisualElement cell, int index)
        {
            LogEntry entry = _viewModel.Visible[index];
            var label = (Label)cell;
            label.userData = entry;
            label.text = FirstLine(entry.Message);
            label.EnableInClassList("cc-marker", entry.Kind == LogEntryKind.Marker);
            label.EnableInClassList("cc-msg-warning", entry.Kind == LogEntryKind.Log && entry.Severity == LogSeverity.Warning);
            label.EnableInClassList("cc-msg-error", entry.Kind == LogEntryKind.Log && entry.Severity >= LogSeverity.Error);
        }

        private void BindCountCell(VisualElement cell, int index)
        {
            int count = _viewModel.CountAt(index);
            ((Label)cell).text = count > 1 ? "×" + count : string.Empty;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            LogEntry entry = FirstEntry(selection);
            _detail.Show(entry);
            if (entry != null)
            {
                SourceNavigator.Ping(entry.Context);
            }
        }

        private void OnItemsChosen(IEnumerable<object> chosen)
        {
            LogEntry entry = FirstEntry(chosen);
            if (entry == null || entry.Kind == LogEntryKind.Marker)
            {
                return;
            }

            TraceFrame frame = FrameGrouper.FindEntryFrame(entry.Trace, _detail.FrameFilter);
            if (frame == null)
            {
                ShowNotice("No source location in this entry's stack trace.");
            }
            else
            {
                OpenFrame(frame);
            }
        }

        /// <summary>Resolves a frame to a file and reads the lines around it, or null when it cannot.</summary>
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
                ShowNotice("Could not open " + frame.FilePath + ":" + frame.Line);
            }
        }

        private static LogEntry FirstEntry(IEnumerable<object> items)
        {
            foreach (object item in items)
            {
                return item as LogEntry;
            }

            return null;
        }

        private void ShowNotice(string text)
        {
            _notice = text;
            _noticeUntil = EditorApplication.timeSinceStartup + NoticeSeconds;
            UpdateStatus();
        }

        private void OnChannelToggled(string channel, bool selected)
        {
            _viewModel.SetChannelSelected(channel, selected);
            RefreshChannels();
        }

        /// <summary>Redraws the chip row; a no-op unless the channels, counts or selection changed.</summary>
        private void RefreshChannels()
        {
            _channels?.Refresh(_viewModel.Store.ChannelCounts, _viewModel.IsChannelSelected);
        }

        private void OnListScrolled(float value)
        {
            _stickToBottom = value >= _listScrollView.verticalScroller.highValue - RowHeight;
        }

        private void OnPreferencesChanged()
        {
            _errorPauseToggle?.SetValueWithoutNotify(ConsolePreferences.ErrorPause);
        }

        private void OnSettingsChanged()
        {
            if (_detail == null)
            {
                return;
            }

            _detail.FrameFilter = ClarityConsoleSettings.instance.CreateFrameFilter();
            _viewModel.IgnoreList = ClarityConsoleSettings.instance.CreateIgnoreList();
            _sources.Clear();
            _detail.Show(FirstEntry(_list.selectedItems));
        }

        private void OnViewChanged(ViewChange change)
        {
            RequestRefresh();
        }

        private void RequestRefresh()
        {
            if (_list == null)
            {
                return;
            }

            if (_refresh == null)
            {
                _refresh = rootVisualElement.schedule.Execute(FlushRefresh);
                _refresh.Pause();
            }

            _refresh.ExecuteLater(RefreshDelayMs);
        }

        private void FlushRefresh()
        {
            if (_list == null || _viewModel == null)
            {
                return;
            }

            _viewModel.Flush();
            _list.Rebuild();
            if (_stickToBottom && _viewModel.Visible.Count > 0)
            {
                _list.schedule.Execute(() => _list.ScrollToItem(-1));
            }

            UpdateStatus();
        }

        private void ScheduleSearch()
        {
            if (_searchDebounce == null)
            {
                _searchDebounce = rootVisualElement.schedule.Execute(ApplySearch);
                _searchDebounce.Pause();
            }

            _searchDebounce.ExecuteLater(SearchDelayMs);
        }

        private void ApplySearch()
        {
            _viewModel.Search = _searchField.value;
            _searchField.EnableInClassList("cc-search-invalid", _viewModel.QueryError != null);
            UpdateStatus();
        }

        private void UpdateCounts()
        {
            if (_viewModel == null || _logToggle == null)
            {
                return;
            }

            LogStore store = _viewModel.Store;
            _logToggle.text = store.CountOf(LogSeverity.Log).ToString();
            _warningToggle.text = store.CountOf(LogSeverity.Warning).ToString();
            _errorToggle.text = (store.CountOf(LogSeverity.Error) + store.CountOf(LogSeverity.Exception) + store.CountOf(LogSeverity.Assert)).ToString();
            RefreshChannels();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_status == null || _viewModel == null)
            {
                return;
            }

            if (_notice != null && EditorApplication.timeSinceStartup < _noticeUntil)
            {
                _status.text = _notice;
                return;
            }

            _notice = null;

            if (_viewModel.QueryError != null)
            {
                _status.text = _viewModel.QueryError;
                _status.AddToClassList("cc-status-error");
                return;
            }

            _status.RemoveFromClassList("cc-status-error");
            LogStore store = _viewModel.Store;
            double journalMb = LogCaptureBootstrap.Journal.SizeBytes / (1024.0 * 1024.0);
            string ignored = _viewModel.IgnoredCount > 0 ? $"   ·   {_viewModel.IgnoredCount:N0} ignored" : string.Empty;
            _status.text = $"{store.Count:N0} of {store.Capacity:N0} entries   ·   {_viewModel.Visible.Count:N0} shown{ignored}   ·   session {store.CurrentSession}   ·   journal {journalMb:0.0} MB";
        }

        private void LoadIcons()
        {
            _logIcon = EditorGUIUtility.IconContent("console.infoicon.sml")?.image;
            _warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml")?.image;
            _errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml")?.image;
        }

        private Texture IconFor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Warning:
                    return _warningIcon;
                case LogSeverity.Error:
                case LogSeverity.Exception:
                case LogSeverity.Assert:
                    return _errorIcon;
                default:
                    return _logIcon;
            }
        }

        private static string FirstLine(string message)
        {
            int newline = message.IndexOf('\n');
            return newline < 0 ? message : message.Substring(0, newline).TrimEnd('\r');
        }
    }
}
