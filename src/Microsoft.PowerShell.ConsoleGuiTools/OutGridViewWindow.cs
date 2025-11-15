using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
    private const int MARGIN_LEFT = 0;
    private const int CHECK_WIDTH = 2;

    private Label? _filterLabel;
    private TextField? _filterField;
    private View? _filterErrorView;
    private View? _header;
    private ListView? _listView;
    private GridViewDataSource? _inputSource;
    private GridViewDataSource? _listViewSource;
    private readonly ApplicationData _applicationData;
    private readonly GridViewDetails _gridViewDetails;
    private readonly DataTable _dataTable;
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

        if (_applicationData.MinUI)
            BorderStyle = LineStyle.None;

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

        if (!_applicationData.MinUI)
        {
            AddFilter();
        }

        AddListView();
        AddStatusBar();

        _listView?.SetFocus();
        
        // Copy the input DataTable into our master ListView source list
        _inputSource = LoadData();
        ApplyFilter();
        _gridViewDetails.UsableWidth = _naturalColumnWidths!.Sum();
        var gridHeaders = _dataTable?.DataColumns.Select(c => c.Label).ToList();

        if (_header is { })
            _header.Text = GridViewHelpers.GetPaddedString(gridHeaders, _gridViewDetails.ListViewOffset,
                _gridViewDetails.ListViewColumnWidths);

    }

    /// <summary>
    ///     Determines if a property name represents an identifier rather than a quantity.
    ///     Identifiers should not have thousand separators.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>True if the property is likely an identifier; otherwise false.</returns>
    private static bool IsIdentifierProperty(string propertyName)
    {
        // Common identifier property names
        return propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
               propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("PID", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("ProcessId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("SessionId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("SI", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("ParentProcessId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("ThreadId", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Sets the data type on each column based on the values in the data rows.
    ///     If all values in a column can be parsed as decimal, the column type is set to decimal; otherwise, it's set to
    ///     string.
    /// </summary>
    /// <param name="dataTableRows">The list of data table rows to analyze.</param>
    /// <param name="dataTableColumns">The list of data table columns to update with type information.</param>
    private static void SetTypesOnDataColumns(List<DataTableRow> dataTableRows, List<DataTableColumn> dataTableColumns)
    {
        var dataRows = dataTableRows.Select(x => x.Values);

        foreach (var dataColumn in dataTableColumns)
            dataColumn.StringType = typeof(decimal).FullName;

        // If every value in a column could be a decimal, assume that it is supposed to be a decimal
        foreach (var dataRow in dataRows)
        {
            foreach (var dataColumn in dataTableColumns)
            {
                if (dataRow[dataColumn.ToString()] is not DecimalValue)
                    dataColumn.StringType = typeof(string).FullName;
            }
        }
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
        if (_dataTable.Data.Count == 0)
            return new GridViewDataSource(items);

        // Calculate and cache natural column widths
        var gridHeaders = _dataTable.DataColumns.Select(c => c.Label).ToList();
        _naturalColumnWidths = CalculateNaturalColumnWidths(gridHeaders);
        _gridViewDetails.ListViewColumnWidths = _naturalColumnWidths;

        for (var i = 0; i < _dataTable.Data.Count; i++)
        {
            var dataTableRow = _dataTable.Data[i];
            var valueList = new List<string>();
            foreach (var dataTableColumn in _dataTable.DataColumns)
            {
                var columnKey = dataTableColumn.ToString();

                // Check if the key exists in the dictionary
                if (dataTableRow.Values.TryGetValue(columnKey, out var value))
                {
                    valueList.Add(value.DisplayValue);
                }
                else
                {
                    // Key not found - this means the dictionary was populated with different keys
                    // This is a bug - let's add empty string for now to avoid crash
                    valueList.Add(string.Empty);
                }
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
        if (source == null) return;

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
        if (_header is { })
            _header.Text = GridViewHelpers.GetPaddedString(gridHeaders, _gridViewDetails.ListViewOffset,
                _gridViewDetails.ListViewColumnWidths);

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
    ///     Adds the main list view control to the window with configured selection behavior.
    /// </summary>
    private void AddListView()
    {
        _listView = new ListView
        {
            Source = _inputSource,
            X = MARGIN_LEFT,
            Y = !_applicationData.MinUI ? Pos.Bottom(_filterErrorView!) : 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            AllowsMarking = _applicationData.OutputMode != OutputModeOption.None,
            AllowsMultipleSelection = _applicationData.OutputMode == OutputModeOption.Multiple,
            SelectedItem = 0,
            VerticalScrollBar = { AutoShow = true },
            HorizontalScrollBar = { AutoShow = true }
        };

        _listView.KeyBindings.Remove(Key.A.WithCtrl);

        if (!_applicationData.MinUI)
        {
            AddHeaders();
        }

        Add(_listView);
        return;
            
        void AddHeaders()
        {
            _header = new View
            {
                //Y = _applicationData.MinUI ? 0 : Pos.Bottom(_filterErrorView!),
                Height = 1,
                Width = Dim.Auto(DimAutoStyle.Text)
            };
            _header.GettingAttributeForRole += HeaderOnGettingAttributeForRole;

            _listView.ViewportChanged += ListViewOnViewportChanged;

            _listView.Padding!.Thickness = _listView.Padding.Thickness with { Top = 1 };
            _listView!.Padding!.Add(_header);
            _listView.VerticalScrollBar.Y = 1;
            return;

            void ListViewOnViewportChanged(object? sender, DrawEventArgs e)
            {
                _header.Viewport = _header.Viewport with { X = _listView.Viewport.X };
            }

            void HeaderOnGettingAttributeForRole(object? sender, VisualRoleEventArgs e)
            {
                if (e.Role == VisualRole.Normal)
                {
                    e.Result = e.Result!.Value with { Style = TextStyle.Underline };
                    e.Handled = true;
                }
            }
        }
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

        CheckBox allPropertiesCheckBox = new CheckBox()
        {
            Title = "A_ll Properties",
            CheckedState = _applicationData.AllProperties ? CheckState.Checked : CheckState.UnChecked,
            CanFocus = false,
        };
        allPropertiesCheckBox.CheckedStateChanging += AllPropertiesCheckBoxOnCheckedStateChanging;

        void AllPropertiesCheckBoxOnCheckedStateChanging(object? sender, ResultEventArgs<CheckState> e)
        {
            
        }

        allPropertiesCheckBox.CheckedStateChanged += AllPropertiesCheckBoxOnCheckedStateChanged;

        void AllPropertiesCheckBoxOnCheckedStateChanged(object? sender, EventArgs<CheckState> e)
        {
            ReloadDataWithAllProperties(!_applicationData.AllProperties);
        }

        Shortcut allPropertiesShortcut = new Shortcut()
        {
            CommandView = allPropertiesCheckBox,
            CanFocus = false,
            BindKeyToApplication = true,
        };
        shortcuts.Add(allPropertiesShortcut);
        
        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            shortcuts.Add(new Shortcut(Key.Empty,
                $"{Application.Driver} v{FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location).ProductVersion}",
                null));
        }

        _statusBar = new StatusBar(shortcuts);
        Add(_statusBar);
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


        //if (_naturalColumnWidths!.Sum() > Viewport.Width - 1)
        //{
        //    _listView!.HorizontalScrollBar.Visible = true;
        //}
        //else
        //{
        //    _listView!.HorizontalScrollBar.Visible = false;
        //}
        //UpdateDisplayStrings(_listViewSource);

        //ApplyFilter();
        base.OnSubViewLayout(args);

    }

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
        if (gridHeaders == null)
            return [];

        var columnWidths = new int[gridHeaders.Count];

        // Start with header widths
        for (var i = 0; i < gridHeaders.Count; i++)
            columnWidths[i] = gridHeaders[i].Length;

        // Expand to fit data
        foreach (var row in _dataTable.Data)
        {
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
        }

        return columnWidths;
    }

    /// <summary>
    ///     Calculates optimal column widths based on header and data content, fitting within the available screen width.
    /// </summary>
    /// <param name="gridHeaders">The column headers for the grid.</param>
    /// <param name="width"></param>
    /// <returns><see langword="null"/>If the column widths could not be calculated.</returns>
    private int[]? CalculateColumnWidths(List<string>? gridHeaders, int width)
    {
        if (gridHeaders == null) return null;

        var listViewColumnWidths = _naturalColumnWidths;

        while (GetCurrentTotal(listViewColumnWidths) > width)
        {
            // Find the rightmost column with width > 0 and shrink it
            var shrinkIndex = -1;
            for (var i = listViewColumnWidths.Length - 1; i >= 0; i--)
            {
                if (listViewColumnWidths[i] > 0)
                {
                    shrinkIndex = i;
                    break;
                }
            }

            if (shrinkIndex == -1)
                break;

            listViewColumnWidths[shrinkIndex]--;
        }

        return listViewColumnWidths;

        // Calculate current total: sum of column widths + spaces between visible columns only
        static int GetCurrentTotal(int[] widths) => widths.Sum() + Math.Max(0, widths.Count(w => w > 0) - 1);
    }

    #endregion
}