using Microsoft.PowerShell.OutGridView.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the Terminal.Gui Window implementation for displaying tabular data with filtering and selection
///     capabilities.
/// </summary>
internal sealed class OutGridViewWindow : Runnable<HashSet<int>>
{
    private const string FILTER_LABEL = "_Filter:";
    private const int MARGIN_LEFT = 0;
    private const int CHECK_WIDTH = 2;
    private readonly ApplicationData _applicationData;
    private readonly DataTable? _dataTable;
    private readonly GridViewDetails _gridViewDetails;
    private View? _filterErrorView;
    private TextField? _filterField;

    private Label? _filterLabel;
    private Header? _header;
    private GridViewDataSource? _inputSource;
    private ListView? _listView;
    private GridViewDataSource? _listViewSource;
    private int[]? _naturalColumnWidths;
    private StatusBar? _statusBar;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OutGridViewWindow" /> class with the specified application data.
    /// </summary>
    /// <param name="applicationData">The configuration and data to display in the grid view.</param>
    public OutGridViewWindow(ApplicationData applicationData)
    {
        _applicationData = applicationData;
        Title = _applicationData.Title ?? "Out-ConsoleGridView";
        SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Base);
        BorderStyle = Window.DefaultBorderStyle;

        switch (_applicationData.MinUI)
        {
            case true:
                BorderStyle = LineStyle.None;
                break;
            case false:
                AddFilter();
                break;
        }

        _gridViewDetails = new GridViewDetails
        {
            ListViewOffset = _applicationData.OutputMode != OutputModeOption.None
                ? MARGIN_LEFT + CHECK_WIDTH
                : MARGIN_LEFT
        };

        // Convert PSObjects to DataTable using TypeGetter which handles format data properly
        if (_applicationData.PSObjects is { Count: > 0 })
        {
            var psObjects = _applicationData.PSObjects.Cast<PSObject>().ToList();
            _dataTable = TypeGetter.CastObjectsToTableView(psObjects, _applicationData.AllProperties);
        }
        else
        {
            _dataTable = new DataTable([], []);
        }

        AddListView();

        // Copy the input DataTable into our master ListView source list
        _inputSource = LoadData();
        ApplyFilter();
        _gridViewDetails.UsableWidth = _naturalColumnWidths!.Sum();
        var gridHeaders = _dataTable?.DataColumns.Select(c => c.Label).ToList();

        _header?.SetHeaders(gridHeaders, _gridViewDetails.ListViewColumnWidths);
    }

    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (!newIsRunning) return;

        // We do this here, because _statusBar requires the Application to be running to
        // access the driver information.
        AddStatusBar();
        _listView?.SetFocus();
    }

    /// <summary>
    ///     Gets the original indexes of all marked rows.
    /// </summary>
    /// <returns>A set of zero-based indexes from the original data table.</returns>
    public HashSet<int> GetSelectedIndexes()
    {
        if (_inputSource == null) return [];

        var selectedIndexes = new HashSet<int>();
        foreach (var gvr in _inputSource.GridViewRowList.Where(gvr => gvr.IsMarked))
            selectedIndexes.Add(gvr.OriginalIndex);

        return selectedIndexes;
    }

    #region Data Management

    /// <summary>
    ///     Loads data from the application data table into grid view rows.
    /// </summary>
    /// <returns>A data source containing the loaded rows.</returns>
    private GridViewDataSource LoadData()
    {
        var items = new List<GridViewRow>();
        if (_dataTable?.Data.Count == 0)
            return new GridViewDataSource(items);

        // Calculate and cache natural column widths
        _naturalColumnWidths = CalculateNaturalColumnWidths(_dataTable?.DataColumns.Select(c => c.Label).ToList());
        _gridViewDetails.ListViewColumnWidths = _naturalColumnWidths;

        for (var i = 0; i < _dataTable?.Data.Count; i++)
        {
            var dataTableRow = _dataTable.Data[i];
            var valueList = new List<string>();
            foreach (var dataTableColumn in _dataTable.DataColumns)
            {
                var columnKey = dataTableColumn.ToString();

                // Check if the key exists in the dictionary
                valueList.Add(dataTableRow.Values.TryGetValue(columnKey, out var value)
                    ? value.DisplayValue
                    // Key not found - this means the dictionary was populated with different keys
                    // This is a bug - let's add empty string for now to avoid crash
                    : string.Empty);
            }

            var displayString = GridViewHelpers.GetPaddedString(valueList, 0, _gridViewDetails.ListViewColumnWidths);

            items.Add(new GridViewRow
            {
                DisplayString = displayString,
                OriginalIndex = i
            });
        }

        return new GridViewDataSource(items);
    }

    #endregion

    #region Filtering

    /// <summary>
    ///     Applies the current filter to the input data and updates the list view with matching rows.
    /// </summary>
    private void ApplyFilter()
    {
        GridViewRow? selectedItem = null;

        if (_listViewSource != null)
        {
            selectedItem = _listViewSource.GridViewRowList.ElementAtOrDefault(_listView?.SelectedItem ?? 0);
            _listViewSource.MarkChanged -= OnListViewSourceMarkChanged;
            _listViewSource = null;
        }

        _inputSource ??= LoadData();

        try
        {
            _listViewSource = new GridViewDataSource(GridViewHelpers.FilterData(_inputSource.GridViewRowList,
                _applicationData.Filter ?? string.Empty));
        }
        catch (RegexParseException ex)
        {
            _filterErrorView?.Text = ex.Message;
        }

        _listViewSource?.MarkChanged += OnListViewSourceMarkChanged;

        _listView?.Source = _listViewSource;

        if (selectedItem is not null && _listViewSource != null)
        {
            var newIndex =
                _listViewSource.GridViewRowList.FindIndex(i => i.OriginalIndex == selectedItem.OriginalIndex);
            if (newIndex >= 0 && _listView != null)
                _listView.SelectedItem = newIndex;
        }

        if (_listView?.SelectedItem == null && _listView is { Source.Count: > 0 })
            _listView.SelectedItem = 0;
    }

    /// <summary>
    ///     Handles mark changed events from the filtered list view and propagates changes to the input source.
    /// </summary>
    /// <param name="s">The event sender.</param>
    /// <param name="a">The event arguments containing the row that was marked or unmarked.</param>
    private void OnListViewSourceMarkChanged(object? s, GridViewDataSource.RowMarkedEventArgs a)
    {
        _inputSource?.GridViewRowList[a.Row.OriginalIndex].IsMarked = a.Row.IsMarked;
    }

    #endregion

    #region User Actions

    /// <summary>
    ///     Reloads the data with the specified AllProperties setting.
    /// </summary>
    private void ReloadDataWithAllProperties(bool allProperties)
    {
        _applicationData.AllProperties = allProperties;

        // Recreate the data table with the new property settings
        DataTable newDataTable;
        if (_applicationData.PSObjects is { Count: > 0 })
        {
            var psObjects = _applicationData.PSObjects.Cast<PSObject>().ToList();
            newDataTable = TypeGetter.CastObjectsToTableView(psObjects, allProperties);
        }
        else
        {
            newDataTable = new DataTable([], []);
        }

        // Update the data table reference
        typeof(OutGridViewWindow)
            .GetField("_dataTable", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(this, newDataTable);

        // Recalculate column widths
        var gridHeaders = newDataTable.DataColumns.Select(c => c.Label).ToList();
        _naturalColumnWidths = CalculateNaturalColumnWidths(gridHeaders);
        _gridViewDetails.ListViewColumnWidths = _naturalColumnWidths;
        _gridViewDetails.UsableWidth = _naturalColumnWidths.Sum();

        // Update header
        _header?.SetHeaders(gridHeaders, _gridViewDetails.ListViewColumnWidths);

        // Reload and reapply filter
        _inputSource = LoadData();
        ApplyFilter();

        // Update content size
        _listView?.SetContentSize(new Size(_naturalColumnWidths.Sum(), _listView.GetContentSize().Height));

        // Update status bar to show current state
        UpdateStatusBar();

        // Force redraw
        SetNeedsLayout();
        SetNeedsDraw();
    }

    /// <summary>
    ///     Updates the status bar to reflect the current AllProperties state.
    /// </summary>
    private void UpdateStatusBar()
    {
        if (_statusBar == null) return;

        // Remove and recreate status bar to update the checkbox text
        Remove(_statusBar);
        AddStatusBar();
    }

    /// <summary>
    ///     Accepts the current selection and closes the window.
    /// </summary>
    private void Accept()
    {
        Result = GetSelectedIndexes();
        App?.RequestStop();
    }

    /// <summary>
    ///     Cancels the operation and closes the window.
    /// </summary>
    private void Close()
    {
        Result = null;
        App?.RequestStop();
    }

    #endregion

    #region UI Construction

    /// <summary>
    ///     Adds the filter text field and error display to the window.
    /// </summary>
    private void AddFilter()
    {
        _filterLabel = new Label
        {
            Text = FILTER_LABEL,
            X = MARGIN_LEFT,
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
            var filterText = _filterField.Text;
            try
            {
                _filterErrorView.Text = string.Empty;
                _applicationData.Filter = filterText;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                _filterErrorView.Text = ex.Message;
            }
        };

        Add(_filterLabel, _filterField, _filterErrorView);

        _filterField.Text = _applicationData.Filter ?? string.Empty;
        _filterField.InsertionPoint = _filterField.Text.Length;
    }

    /// <summary>
    ///     Adds the main list view control to the window with configured selection behavior.
    /// </summary>
    private void AddListView()
    {
        _listView = new ListView
        {
            Source = _inputSource,
            X = MARGIN_LEFT,
            Y = _filterErrorView is not null ? Pos.Bottom(_filterErrorView) : 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            AllowsMarking = _applicationData.OutputMode != OutputModeOption.None,
            AllowsMultipleSelection = _applicationData.OutputMode == OutputModeOption.Multiple,
            SelectedItem = 0,
            VerticalScrollBar = { AutoShow = true },
            HorizontalScrollBar = { AutoShow = true }
        };

        _listView.KeyBindings.Remove(Key.A.WithCtrl);

        if (!_applicationData.MinUI) AddHeader();

        Add(_listView);
        return;

        void AddHeader()
        {
            _header = new Header
            {
                X = CHECK_WIDTH
            };


            _listView.Padding!.Thickness = _listView.Padding.Thickness with { Top = 1 };
            _listView!.Padding!.Add(_header);
            _listView.VerticalScrollBar.Y = 1;
        }
    }


    /// <summary>
    ///     Adds the status bar with keyboard shortcuts to the window.
    /// </summary>
    private void AddStatusBar()
    {
        var shortcuts = new List<Shortcut>();
        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Space, "Select", null));

        if (_applicationData.OutputMode == OutputModeOption.Multiple)
        {
            shortcuts.Add(new Shortcut(Key.A.WithCtrl, "Sel. All", () =>
            {
                _listView?.MarkAll(true);
                _listView?.SetNeedsDraw();
            }));

            shortcuts.Add(new Shortcut(Key.D.WithCtrl, "Sel. None", () =>
            {
                _listView?.MarkAll(false);
                _listView?.SetNeedsDraw();
            }));
        }

        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Enter, "Accept", () =>
            {
                if (MostFocused == _listView)
                {
                    if (_applicationData.OutputMode == OutputModeOption.Single &&
                        _inputSource!.GridViewRowList.Find(i => i.IsMarked) == null)
                        if (_listView!.SelectedItem is not null && _listView.SelectedItem < _listViewSource!.Count)
                        {
                            var item = _listViewSource.GridViewRowList[_listView.SelectedItem.Value];
                            item.IsMarked = !item.IsMarked;
                        }

                    Accept();
                }
                else if (MostFocused == _filterField)
                {
                    _listView!.SetFocus();
                }
            }));

        shortcuts.Add(new Shortcut(Key.Esc, "Close", Close));

        var allPropertiesShortcut = new Shortcut
        {
            CommandView = new CheckBox
            {
                Title = "A_ll Properties",
                CheckedState = _applicationData.AllProperties ? CheckState.Checked : CheckState.UnChecked,
                CanFocus = false,
                MouseHighlightStates = MouseState.None
            },
            CanFocus = false,
            BindKeyToApplication = true
        };

        allPropertiesShortcut.Accepting += (_, e) =>
        {
            ReloadDataWithAllProperties(!_applicationData.AllProperties);
            e.Handled = true;
        };

        shortcuts.Add(allPropertiesShortcut);


        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            FileVersionInfo tgFileVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location);
            string tgVersion = tgFileVersionInfo?.FileVersion ?? "no version found";
            //if (tgFileVersionInfo is { IsPreRelease: true })
            {
                tgVersion = tgFileVersionInfo?.ProductVersion?[..tgFileVersionInfo.ProductVersion.IndexOf('+')] ?? tgVersion;
            }
            shortcuts.Add(new Shortcut(Key.Empty, $"{App?.Driver?.GetName()} v{tgVersion}", null));
        }

        _statusBar = new StatusBar(shortcuts);
        Add(_statusBar);
    }

    #endregion

    #region Layout Calculation

    protected override void OnSubViewsLaidOut(LayoutEventArgs args)
    {
        base.OnSubViewsLaidOut(args);
        _listView?.SetContentSize(new Size(_naturalColumnWidths!.Sum(), _listView.GetContentSize().Height));
    }

    /// <summary>
    ///     Calculates the natural column widths needed to display all data without truncation.
    /// </summary>
    /// <param name="gridHeaders">The column headers for the grid.</param>
    /// <returns>An array of column widths where each width is the maximum needed for that column.</returns>
    private int[] CalculateNaturalColumnWidths(List<string>? gridHeaders)
    {
        if (gridHeaders is null || _dataTable is null)
            return [];

        var columnWidths = new int[gridHeaders.Count];

        // Start with header widths
        for (var i = 0; i < gridHeaders.Count; i++)
            columnWidths[i] = gridHeaders[i].Length;

        // Expand to fit data
        foreach (var row in _dataTable.Data)
            for (var i = 0; i < _dataTable.DataColumns.Count; i++)
            {
                var columnKey = _dataTable.DataColumns[i].ToString();
                if (row.Values.TryGetValue(columnKey, out var value))
                {
                    var len = value.DisplayValue.Length;
                    if (len > columnWidths[i])
                        columnWidths[i] = len;
                }
            }

        return columnWidths;
    }

    #endregion
}