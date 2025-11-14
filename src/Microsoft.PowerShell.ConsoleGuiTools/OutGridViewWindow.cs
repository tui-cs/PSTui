using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the Terminal.Gui Window implementation for displaying tabular data with filtering and selection
///     capabilities.
/// </summary>
internal sealed class OutGridViewWindow : Window
{
    private const string FILTER_LABEL = "_Filter";
    private const int MARGIN_LEFT = 1;
    private const int CHECK_WIDTH = 2;

    private Label? _filterLabel;
    private TextField? _filterField;
    private View? _filterErrorView;
    private Label? _header;
    private ListView? _listView;
    private GridViewDataSource? _inputSource;
    private GridViewDataSource? _listViewSource;
    private readonly ApplicationData _applicationData;
    private readonly GridViewDetails _gridViewDetails;
    private readonly DataTable _dataTable;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OutGridViewWindow" /> class with the specified application data.
    /// </summary>
    /// <param name="applicationData">The configuration and data to display in the grid view.</param>
    public OutGridViewWindow(ApplicationData applicationData)
    {
        _applicationData = applicationData;
        Title = _applicationData.Title ?? "Out-ConsoleGridView";

        if (_applicationData.MinUI)
            BorderStyle = LineStyle.None;

        _gridViewDetails = new GridViewDetails
        {
            ListViewOffset = _applicationData.OutputMode != OutputModeOption.None
                ? MARGIN_LEFT + CHECK_WIDTH
                : MARGIN_LEFT
        };

        // Convert PSObjects to DataTable
        if (_applicationData.PSObjects is { Count: > 0 })
        {
            var typeGetter = new TypeGetter();
            var psObjects = _applicationData.PSObjects.Cast<PSObject>().ToList();
            _dataTable = TypeGetter.CastObjectsToTableView(psObjects);
        }
        else
        {
            _dataTable = new DataTable([], []);
        }

        // Copy the input DataTable into our master ListView source list
        _inputSource = LoadData();

        if (!_applicationData.MinUI)
        {
            AddFilter();
            AddHeaders();
        }

        AddListView();
        AddStatusBar();

        _listView?.SetFocus();
    }

    /// <summary>
    ///     Gets a value indicating whether the user cancelled the operation.
    /// </summary>
    public bool Cancelled { get; private set; }

    /// <summary>
    ///     Gets the original indexes of all marked rows.
    /// </summary>
    /// <returns>A set of zero-based indexes from the original data table.</returns>
    public HashSet<int> GetSelectedIndexes()
    {
        var selectedIndexes = new HashSet<int>();
        if (Cancelled || _inputSource == null) return selectedIndexes;

        foreach (var gvr in _inputSource.GridViewRowList)
            if (gvr.IsMarked)
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
        if (_dataTable == null || _dataTable.Data.Count == 0)
            return new GridViewDataSource(items);

        for (var i = 0; i < _dataTable.Data.Count; i++)
        {
            var dataTableRow = _dataTable.Data[i];
            var valueList = new List<string>();
            foreach (var dataTableColumn in _dataTable.DataColumns)
            {
                var dataValue = dataTableRow.Values[dataTableColumn.ToString()].DisplayValue;
                valueList.Add(dataValue);
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

    /// <summary>
    ///     Updates the display strings for all rows in the specified data source based on current column widths.
    /// </summary>
    /// <param name="source">The data source containing rows to update.</param>
    private void UpdateDisplayStrings(GridViewDataSource? source)
    {
        if (source == null || _dataTable == null) return;

        foreach (var gvr in source.GridViewRowList)
        {
            var valueList = new List<string>();
            var dataTableRow = _dataTable.Data[gvr.OriginalIndex];
            foreach (var dataTableColumn in _dataTable.DataColumns)
            {
                var dataValue = dataTableRow.Values[dataTableColumn.ToString()].DisplayValue;
                valueList.Add(dataValue);
            }

            gvr.DisplayString = GridViewHelpers.GetPaddedString(valueList, 0, _gridViewDetails.ListViewColumnWidths);
        }
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
            _listViewSource.MarkChanged -= ListViewSource_MarkChanged;
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
            if (_filterErrorView != null)
                _filterErrorView.Text = ex.Message;
        }

        if (_listViewSource != null)
            _listViewSource.MarkChanged += ListViewSource_MarkChanged;

        if (_listView != null)
            _listView.Source = _listViewSource;

        if (selectedItem is { } && _listViewSource != null)
        {
            var newIndex =
                _listViewSource.GridViewRowList.FindIndex(i => i.OriginalIndex == selectedItem.OriginalIndex);
            if (newIndex >= 0 && _listView != null)
                _listView.SelectedItem = newIndex;
        }

        if (_listView?.SelectedItem == -1 && _listView != null)
            _listView.SelectedItem = 0;
    }

    /// <summary>
    ///     Handles mark changed events from the filtered list view and propagates changes to the input source.
    /// </summary>
    /// <param name="s">The event sender.</param>
    /// <param name="a">The event arguments containing the row that was marked or unmarked.</param>
    private void ListViewSource_MarkChanged(object? s, GridViewDataSource.RowMarkedEventArgs a)
    {
        if (_inputSource != null)
            _inputSource.GridViewRowList[a.Row.OriginalIndex].IsMarked = a.Row.IsMarked;
    }

    #endregion

    #region User Actions

    /// <summary>
    ///     Accepts the current selection and closes the window.
    /// </summary>
    private static void Accept()
    {
        Application.RequestStop();
    }

    /// <summary>
    ///     Cancels the operation and closes the window.
    /// </summary>
    private void Close()
    {
        Cancelled = true;
        Application.RequestStop();
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
            SchemeName = "Error"
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
        _filterField.CursorPosition = _filterField.Text.Length;
    }

    /// <summary>
    ///     Adds the column header label and separator line to the window.
    /// </summary>
    private void AddHeaders()
    {
        _header = new Label
        {
            Y = _applicationData.MinUI ? 0 : Pos.Bottom(_filterErrorView!)
        };
        Add(_header);

        if (!_applicationData.MinUI)
        {
            var headerLine = new Line
            {
                X = MARGIN_LEFT,
                Y = Pos.Bottom(_header),
                Width = Dim.Fill(MARGIN_LEFT)
            };
            Add(headerLine);
        }
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
            Y = !_applicationData.MinUI ? Pos.Bottom(_filterLabel!) + 2 : 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            AllowsMarking = _applicationData.OutputMode != OutputModeOption.None,
            AllowsMultipleSelection = _applicationData.OutputMode == OutputModeOption.Multiple,
            SelectedItem = 0
        };

        _listView.KeyBindings.Remove(Key.A.WithCtrl);

        Add(_listView);
    }

    /// <summary>
    ///     Adds the status bar with keyboard shortcuts to the window.
    /// </summary>
    private void AddStatusBar()
    {
        var shortcuts = new List<Shortcut>();
        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Space, "Select Item", null));

        if (_applicationData.OutputMode == OutputModeOption.Multiple)
        {
            shortcuts.Add(new Shortcut(Key.A.WithCtrl, "Select All", () =>
            {
                _listView?.MarkAll(true);
                _listView?.SetNeedsDraw();
            }));

            shortcuts.Add(new Shortcut(Key.D.WithCtrl, "Select None", () =>
            {
                _listView?.MarkAll(false);
                _listView?.SetNeedsDraw();
            }));
        }

        if (_applicationData.OutputMode != OutputModeOption.None)
            shortcuts.Add(new Shortcut(Key.Enter, "Accept", () =>
            {
                if (Application.Top?.MostFocused == _listView)
                {
                    if (_applicationData.OutputMode == OutputModeOption.Single &&
                        _inputSource!.GridViewRowList.Find(i => i.IsMarked) == null)
                        if (_listView!.SelectedItem >= 0 && _listView.SelectedItem < _listViewSource!.Count)
                        {
                            var item = _listViewSource.GridViewRowList[_listView.SelectedItem];
                            item.IsMarked = !item.IsMarked;
                        }

                    Accept();
                }
                else if (Application.Top?.MostFocused == _filterField)
                {
                    _listView!.SetFocus();
                }
            }));

        shortcuts.Add(new Shortcut(Key.Esc, "Close", Close));
        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            shortcuts.Add(new Shortcut(Key.Empty,
                $"{Application.Driver} v{FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location).ProductVersion}",
                null));
        }

        Add(new StatusBar(shortcuts));
    }

    #endregion

    #region Layout Calculation


    /// <summary>
    ///     Handles layout of subviews by calculating column widths and applying the current filter.
    /// </summary>
    /// <param name="args">The layout event arguments.</param>
    protected override void OnSubViewLayout(LayoutEventArgs args)
    {
        // Create the headers and calculate column widths based on the DataTable
        var gridHeaders = _dataTable?.DataColumns.Select(c => c.Label).ToList();

        CalculateColumnWidths(gridHeaders);

        if (_header is { })
            _header.Text = GridViewHelpers.GetPaddedString(gridHeaders, _gridViewDetails.ListViewOffset,
                _gridViewDetails.ListViewColumnWidths);
        UpdateDisplayStrings(_listViewSource);
        ApplyFilter();
        base.OnSubViewLayout(args);
    }

    /// <summary>
    ///     Calculates optimal column widths based on header and data content, fitting within the available screen width.
    /// </summary>
    /// <param name="gridHeaders">The column headers for the grid.</param>
    private void CalculateColumnWidths(List<string>? gridHeaders)
    {
        if (gridHeaders == null || _dataTable == null) return;

        _gridViewDetails.ListViewColumnWidths = new int[gridHeaders.Count];
        var listViewColumnWidths = _gridViewDetails.ListViewColumnWidths;

        for (var i = 0; i < gridHeaders.Count; i++)
            listViewColumnWidths[i] = gridHeaders[i].Length;

        foreach (var row in _dataTable.Data)
        {
            var index = 0;
            foreach (var col in row.Values.Take(Application.Top!.Frame.Height / 2))
            {
                var len = col.Value.DisplayValue.Length;
                if (len > listViewColumnWidths[index])
                    listViewColumnWidths[index] = len;
                index++;
            }
        }

        _gridViewDetails.UsableWidth = Application.Top!.Frame.Width - MARGIN_LEFT - listViewColumnWidths.Length -
                                       _gridViewDetails.ListViewOffset;
        var columnWidthsSum = listViewColumnWidths.Sum();
        while (columnWidthsSum >= _gridViewDetails.UsableWidth)
        {
            var maxWidth = 0;
            var maxIndex = 0;
            for (var i = 0; i < listViewColumnWidths.Length; i++)
                if (listViewColumnWidths[i] > maxWidth)
                {
                    maxWidth = listViewColumnWidths[i];
                    maxIndex = i;
                }

            listViewColumnWidths[maxIndex]--;
            columnWidthsSum--;
        }
    }

    #endregion
}