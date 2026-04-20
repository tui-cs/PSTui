// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the Terminal.Gui Window implementation for displaying tabular data using <see cref="TableView" />
///     with filtering, marking, and streaming support.
/// </summary>
internal sealed class OutTableViewWindow : Runnable<HashSet<int>>
{
    private const string FILTER_LABEL = "_Filter:";

    private readonly ApplicationData _applicationData;
    private OutTableViewDataSource _masterDataSource;
    private OutTableViewDataSource? _filteredDataSource;
    private TableView? _tableView;
    private TextField? _filterField;
    private Label? _filterLabel;
    private View? _filterErrorView;
    private StatusBar? _statusBar;

    /// <summary>
    ///     Tracks marked rows by their original object index. Persists across filter changes.
    /// </summary>
    private readonly HashSet<int> _markedOriginalIndexes = [];

    private bool _isLoading = true;
    private readonly bool _useMarks;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OutTableViewWindow" /> class.
    /// </summary>
    /// <param name="applicationData">The configuration and data to display.</param>
    /// <param name="dataSource">The data source (may grow during streaming).</param>
    public OutTableViewWindow(ApplicationData applicationData, OutTableViewDataSource dataSource)
    {
        _applicationData = applicationData;
        _masterDataSource = dataSource;
        _useMarks = _applicationData.OutputMode != OutputModeOption.None;

        Title = _applicationData.Title ?? "Out-ConsoleTableView";
        SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Base);
        BorderStyle = FrameView.DefaultBorderStyle;

        if (_applicationData.MinUI)
        {
            BorderStyle = LineStyle.None;
        }
        else
        {
            AddFilter();
        }

        AddTableView();
        ApplyFilter();
    }

    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (!newIsRunning) return;

        AddStatusBar();
        _tableView?.SetFocus();
    }

    /// <summary>
    ///     Called (via Application.Invoke) when new rows have been added to the master data source during streaming.
    /// </summary>
    public void OnDataChanged()
    {
        ApplyFilter();
        UpdateStreamingStatus();
    }

    /// <summary>
    ///     Called (via Application.Invoke) when the pipeline has finished sending objects.
    /// </summary>
    public void OnPipelineComplete()
    {
        _isLoading = false;
        UpdateStreamingStatus();
    }

    /// <summary>
    ///     Gets the original indexes of all marked rows.
    /// </summary>
    public HashSet<int> GetSelectedIndexes() => new(_markedOriginalIndexes);

    #region Filtering

    private void ApplyFilter()
    {
        // Save the currently selected row's original index so we can restore position
        int? selectedOriginalIndex = null;
        if (_filteredDataSource != null && _tableView != null && _tableView.SelectedRow >= 0 &&
            _tableView.SelectedRow < _filteredDataSource.Rows)
        {
            selectedOriginalIndex = _filteredDataSource.GetOriginalObjectIndex(_tableView.SelectedRow);
        }

        try
        {
            if (_filterErrorView != null) _filterErrorView.Text = string.Empty;
            _filteredDataSource = _masterDataSource.Filter(
                _applicationData.Filter ?? string.Empty,
                _markedOriginalIndexes);
        }
        catch (RegexParseException ex)
        {
            if (_filterErrorView != null) _filterErrorView.Text = ex.Message;
            return;
        }

        // Set the table source (with or without mark column)
        RebuildTableSource();

        // Restore selection position
        if (selectedOriginalIndex.HasValue && _filteredDataSource != null && _tableView != null)
        {
            for (var i = 0; i < _filteredDataSource.Rows; i++)
            {
                if (_filteredDataSource.GetOriginalObjectIndex(i) == selectedOriginalIndex.Value)
                {
                    _tableView.SelectedRow = i;
                    break;
                }
            }
        }

        _tableView?.Update();
    }

    private void RebuildTableSource()
    {
        if (_filteredDataSource == null || _tableView == null) return;

        if (_useMarks)
        {
            _tableView.Table = new MarkedTableSource(_filteredDataSource, _markedOriginalIndexes);
        }
        else
        {
            _tableView.Table = _filteredDataSource;
        }
    }

    #endregion

    #region User Actions

    private void ToggleCurrentRowMark()
    {
        if (_tableView == null || _filteredDataSource == null) return;
        var row = _tableView.SelectedRow;
        if (row < 0 || row >= _filteredDataSource.Rows) return;

        var origIdx = _filteredDataSource.GetOriginalObjectIndex(row);
        if (origIdx < 0) return;

        if (_applicationData.OutputMode == OutputModeOption.Single)
        {
            // In single mode, clear others first
            _markedOriginalIndexes.Clear();
            _markedOriginalIndexes.Add(origIdx);
        }
        else
        {
            if (!_markedOriginalIndexes.Remove(origIdx))
                _markedOriginalIndexes.Add(origIdx);
        }

        RebuildTableSource();
        _tableView.Update();
    }

    private void MarkAll()
    {
        if (_filteredDataSource == null) return;

        for (var i = 0; i < _filteredDataSource.Rows; i++)
            _markedOriginalIndexes.Add(_filteredDataSource.GetOriginalObjectIndex(i));

        RebuildTableSource();
        _tableView?.Update();
    }

    private void UnmarkAll()
    {
        _markedOriginalIndexes.Clear();
        RebuildTableSource();
        _tableView?.Update();
    }

    private void Accept()
    {
        Result = GetSelectedIndexes();
        App?.RequestStop();
    }

    private void Close()
    {
        Result = null;
        App?.RequestStop();
    }

    #endregion

    #region UI Construction

    private void AddFilter()
    {
        _filterLabel = new Label
        {
            Text = FILTER_LABEL,
            X = 0,
            Y = 0
        };

        _filterField = new TextField
        {
            Text = _applicationData.Filter ?? string.Empty,
            X = Pos.Right(_filterLabel) + 1,
            Y = Pos.Top(_filterLabel),
            CanFocus = true,
            Width = Dim.Fill() - 1
        };

        _filterField.KeyBindings.Remove(Key.A.WithCtrl);
        _filterField.KeyBindings.Remove(Key.D.WithCtrl);

        _filterErrorView = new View
        {
            Text = string.Empty,
            X = Pos.Right(_filterLabel) + 1,
            Y = Pos.Top(_filterLabel) + 1,
            Width = Dim.Fill() - _filterLabel.Text.Length,
            Height = Dim.Auto(DimAutoStyle.Text),
            SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Error)
        };

        _filterField.TextChanged += (_, _) =>
        {
            try
            {
                if (_filterErrorView != null) _filterErrorView.Text = string.Empty;
                _applicationData.Filter = _filterField.Text;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                if (_filterErrorView != null) _filterErrorView.Text = ex.Message;
            }
        };

        Add(_filterLabel, _filterField, _filterErrorView);
        _filterField.Text = _applicationData.Filter ?? string.Empty;
        _filterField.InsertionPoint = _filterField.Text.Length;
    }

    private void AddTableView()
    {
        _tableView = new TableView
        {
            X = 0,
            Y = _filterErrorView is { } ? Pos.Bottom(_filterErrorView) : 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            FullRowSelect = true,
            MultiSelect = false,
            Style = new TableStyle
            {
                ShowHeaders = true,
                AlwaysShowHeaders = true,
                ExpandLastColumn = true,
                ShowHorizontalHeaderUnderline = true,
                SmoothHorizontalScrolling = true
            },
            ViewportSettings = ViewportSettingsFlags.HasScrollBars
        };

        // Handle keyboard shortcuts via KeyDown event
        _tableView.KeyDown += OnTableViewKeyDown;

        // Enter key activates selection
        if (_applicationData.OutputMode != OutputModeOption.None)
        {
            _tableView.CellActivated += (_, _) => Accept();
        }

        Add(_tableView);
    }

    private void OnTableViewKeyDown(object? sender, Key e)
    {
        if (_useMarks && e == Key.Space)
        {
            ToggleCurrentRowMark();
            e.Handled = true;
        }
        else if (_applicationData.OutputMode == OutputModeOption.Multiple && e == Key.A.WithCtrl)
        {
            MarkAll();
            e.Handled = true;
        }
        else if (_applicationData.OutputMode == OutputModeOption.Multiple && e == Key.D.WithCtrl)
        {
            UnmarkAll();
            e.Handled = true;
        }
        else if (e == Key.Esc)
        {
            Close();
            e.Handled = true;
        }
        else if (_applicationData.OutputMode != OutputModeOption.None && e == Key.Enter)
        {
            if (MostFocused == _filterField)
            {
                _tableView?.SetFocus();
            }
            else
            {
                Accept();
            }

            e.Handled = true;
        }
    }

    private void AddStatusBar()
    {
        if (_applicationData.MinUI) return;

        var shortcuts = new List<Shortcut>();

        // Spinner as 1st item while loading (streaming or reloading)
        if (_isLoading)
        {
            var rowCount = _masterDataSource.Rows;
            shortcuts.Add(new Shortcut
            {
                CommandView = new SpinnerView { AutoSpin = true },
                HelpText = $"{rowCount} rows",
                CanFocus = false
            });
        }

        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Space, "Mark", null));

        if (_applicationData.OutputMode == OutputModeOption.Multiple)
        {
            shortcuts.Add(new Shortcut(Key.A.WithCtrl, "Sel. All", null));
            shortcuts.Add(new Shortcut(Key.D.WithCtrl, "Sel. None", null));
        }

        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Enter, "Accept", null));

        shortcuts.Add(new Shortcut(Key.Esc, "Close", null));

        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            var tgFileVersionInfo =
                FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location);
            var tgVersion = tgFileVersionInfo?.FileVersion ?? "no version found";
            {
                tgVersion = tgFileVersionInfo?.ProductVersion?[..tgFileVersionInfo.ProductVersion.IndexOf('+')] ??
                            tgVersion;
            }
            shortcuts.Add(new Shortcut(Key.Empty, $"{App?.Driver?.GetName()} v{tgVersion}", null));
        }

        _statusBar = new StatusBar(shortcuts);
        Add(_statusBar);
    }

    private void UpdateStatusBar()
    {
        if (_statusBar == null || _applicationData.MinUI) return;

        Remove(_statusBar);
        _statusBar.Dispose();
        _statusBar = null;
        AddStatusBar();
    }

    private void UpdateStreamingStatus()
    {
        if (_applicationData.MinUI) return;
        UpdateStatusBar();
    }

    #endregion

    #region MarkedTableSource

    /// <summary>
    ///     Wraps an <see cref="OutTableViewDataSource" /> and prepends a mark indicator column.
    ///     Marked rows show ">" in the first column.
    /// </summary>
    private sealed class MarkedTableSource : ITableSource
    {
        private readonly OutTableViewDataSource _inner;
        private readonly HashSet<int> _markedOriginalIndexes;

        public MarkedTableSource(OutTableViewDataSource inner, HashSet<int> markedOriginalIndexes)
        {
            _inner = inner;
            _markedOriginalIndexes = markedOriginalIndexes;
        }

        public string[] ColumnNames
        {
            get
            {
                var inner = _inner.ColumnNames;
                var result = new string[inner.Length + 1];
                result[0] = " ";
                Array.Copy(inner, 0, result, 1, inner.Length);
                return result;
            }
        }

        public int Columns => _inner.Columns + 1;
        public int Rows => _inner.Rows;

        public object this[int row, int col]
        {
            get
            {
                if (col == 0)
                {
                    var origIdx = _inner.GetOriginalObjectIndex(row);
                    return _markedOriginalIndexes.Contains(origIdx) ? ">" : " ";
                }

                return _inner[row, col - 1];
            }
        }
    }

    #endregion
}
