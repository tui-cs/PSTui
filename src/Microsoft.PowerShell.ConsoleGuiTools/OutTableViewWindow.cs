// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private readonly OutTableViewDataSource _masterDataSource;

    private OutTableViewDataSource? _filteredDataSource;
    private View? _filterErrorView;
    private TextField? _filterField;
    private Label? _filterLabel;
    private bool _isLoading = true;

    private StatusBar? _statusBar;
    private TableView? _tableView;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OutTableViewWindow" /> class.
    /// </summary>
    /// <param name="applicationData">The configuration and data to display.</param>
    /// <param name="dataSource">The data source (may grow during streaming).</param>
    public OutTableViewWindow(ApplicationData applicationData, OutTableViewDataSource dataSource)
    {
        _applicationData = applicationData;
        _masterDataSource = dataSource;

        Title = _applicationData.Title ?? "Out-ConsoleTableView";
        SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Base);
        BorderStyle = FrameView.DefaultBorderStyle;

        switch (_applicationData.MinUI)
        {
            case true:
                BorderStyle = LineStyle.None;
                if (!string.IsNullOrEmpty(_applicationData.Filter)) AddFilter();
                break;
            case false:
                Border.Thickness = new Thickness(0, 2, 0, 0);
                AddFilter();
                break;
        }

        AddStatusBar();
        AddTableView();
        ApplyFilter();
    }

    /// <summary>
    ///     Optional callback invoked when the window starts running (UI is ready for interaction).
    /// </summary>
    internal Action? OnRunning { get; set; }

    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (!newIsRunning) return;

        OnRunning?.Invoke();
        _tableView?.SetFocus();
    }

    /// <summary>
    ///     Called (via Application.Invoke) when new rows have been added to the master data source during streaming.
    /// </summary>
    public void OnDataChanged()
    {
        ApplyFilter();
        if (_isLoading)
            _rowsShortcut.Text = $"{_masterDataSource.Rows} rows";
    }

    /// <summary>
    ///     Called (via Application.Invoke) when the pipeline has finished sending objects.
    /// </summary>
    public void OnPipelineComplete()
    {
        _isLoading = false;
        OnLoadingComplete();
    }

    /// <summary>
    ///     Gets the original indexes of all selected rows using TableView's native multi-selection.
    /// </summary>
    public HashSet<int> GetSelectedIndexes()
    {
        if (_tableView == null || _filteredDataSource == null)
            return [];

        var selectedRows = new HashSet<int>();
        foreach (var cell in _tableView.GetAllSelectedCells())
        {
            var origIdx = _filteredDataSource.GetOriginalObjectIndex(cell.Y);
            if (origIdx >= 0)
                selectedRows.Add(origIdx);
        }

        return selectedRows;
    }

    #region User Actions

    private void Accept()
    {
        Result = GetSelectedIndexes();
        App?.RequestStop();
    }

    #endregion

    #region Filtering

    private void ApplyFilter()
    {
        // Save the currently selected row's original index so we can restore position
        int? selectedOriginalIndex = null;
        if (_filteredDataSource != null && _tableView is { SelectedRow: >= 0 } &&
            _tableView.SelectedRow < _filteredDataSource.Rows)
            selectedOriginalIndex = _filteredDataSource.GetOriginalObjectIndex(_tableView.SelectedRow);

        try
        {
            if (_filterErrorView != null) _filterErrorView.Text = string.Empty;
            _filteredDataSource = _masterDataSource.Filter(_applicationData.Filter ?? string.Empty);
        }
        catch (RegexParseException ex)
        {
            if (_filterErrorView != null) _filterErrorView.Text = ex.Message;
            return;
        }

        RebuildTableSource();

        // Restore selection position
        if (selectedOriginalIndex.HasValue && _filteredDataSource != null && _tableView != null)
            for (var i = 0; i < _filteredDataSource.Rows; i++)
                if (_filteredDataSource.GetOriginalObjectIndex(i) == selectedOriginalIndex.Value)
                {
                    _tableView.SetSelection(0, i, false);
                    break;
                }

        _tableView?.Update();
    }

    private void RebuildTableSource()
    {
        if (_filteredDataSource == null || _tableView == null) return;
        _tableView.Table = _filteredDataSource;
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
                _filterErrorView?.Text = string.Empty;
                _applicationData.Filter = _filterField.Text;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _filterErrorView?.Text = ex.Message;
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
            Y = _filterErrorView is not null ? Pos.Bottom(_filterErrorView) : 1,
            Width = Dim.Fill(),
            Height = _statusBar is not null ? Dim.Fill(_statusBar) : Dim.Fill(),
            FullRowSelect = true,
            MultiSelect = _applicationData.OutputMode == OutputModeOption.Multiple,
            Style = new TableStyle
            {
                ShowHeaders = true,
                AlwaysShowHeaders = true,
                ExpandLastColumn = true,
                ShowHorizontalHeaderUnderline = false,
                ShowVerticalHeaderLines = false,
                ShowHorizontalHeaderOverline = false,
                ShowVerticalCellLines = false,
                SmoothHorizontalScrolling = true
            },
            ViewportSettings = ViewportSettingsFlags.HasScrollBars
        };

        // Enter key activates selection
        if (_applicationData.OutputMode != OutputModeOption.None) _tableView.Accepted += (_, _) => Accept();

        Add(_tableView);
    }

    private SpinnerView? _spinnerView;

    private readonly Shortcut _rowsShortcut = new()
    {
        Text = "Rows:",
        CanFocus = false,
        MouseHighlightStates = MouseState.None
    };

    private void AddStatusBar()
    {
        if (_applicationData.MinUI) return;

        var shortcuts = new List<Shortcut>();

        // Spinner as 1st item while loading (streaming or reloading)
        _spinnerView = new() { Style = new SpinnerStyle.Aesthetic(), Width = 8 };
        _spinnerView.AutoSpin = _isLoading;
        _rowsShortcut.CommandView = _spinnerView;
        shortcuts.Add(_rowsShortcut);

        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Enter, "Accept", null));

        if (_applicationData.OutputMode == OutputModeOption.Multiple)
            shortcuts.Add(new Shortcut(Key.A.WithCtrl, "Sel. All", () => _tableView?.SelectAll()));

        shortcuts.Add(new Shortcut(Key.Esc, "Close", RequestStop));

        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            var tgFileVersionInfo =
                FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location);
            var tgVersion = tgFileVersionInfo.FileVersion ?? "no version found";
            {
                tgVersion = tgFileVersionInfo.ProductVersion?[..tgFileVersionInfo.ProductVersion.IndexOf('+')] ??
                            tgVersion;
            }
            shortcuts.Add(new Shortcut(Key.Empty, $"{App?.Driver?.GetName()} v{tgVersion}", null));
        }

        _statusBar = new StatusBar(shortcuts);
        Add(_statusBar);
        MoveSubViewToEnd(_statusBar);
    }

    private void OnLoadingComplete()
    {
        if (_applicationData.MinUI) return;
        if (_statusBar == null) return;

        // Stop spinner and replace with final row count
        // Setting CommandView disposes the old view
        _spinnerView = null;
        _rowsShortcut.CommandView = new View(){
            Width = Dim.Auto(),
            Height = Dim.Fill()
        };

        _rowsShortcut.Title = $"{_masterDataSource.Rows}";
        _rowsShortcut.Text = "Rows: ";
    }

    #endregion
}