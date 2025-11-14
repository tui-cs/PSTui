// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using OutGridView.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace OutGridView.Cmdlet;

internal sealed class ConsoleGui : IDisposable
{
    private const string FILTER_LABEL = "Filter";
    // This adjusts the left margin of all controls
    private const int MARGIN_LEFT = 1;
    // Width of Terminal.Gui ListView selection/check UI elements (old == 4, new == 2)
    private const int CHECK_WIDTH = 2;
    private bool _cancelled;
    private Label? _filterLabel;
    private TextField? _filterField;
    private View? _filterErrorView;
    private Label? _header;
    private ListView? _listView;
    // _inputSource contains the full set of Input data and tracks any items the user
    // marks. When the cmdlet exits, any marked items are returned. When a filter is 
    // active, the list view shows a copy of _inputSource that includes both the items
    // matching the filter AND any items previously marked. 
    private GridViewDataSource? _inputSource;

    // _listViewSource is a filtered copy of _inputSource that ListView.Source is set to. 
    // Changes to IsMarked are propagated back to _inputSource.
    private GridViewDataSource? _listViewSource;
    private ApplicationData? _applicationData;
    private GridViewDetails? _gridViewDetails;

    public HashSet<int> Start(ApplicationData applicationData)
    {
        _applicationData = applicationData;
        // In Terminal.Gui v2, Application.Init() no longer accepts a driver parameter.
        // Instead, use Application.ForceDriver to specify the driver.
        if (_applicationData.UseNetDriver)
        {
            Application.ForceDriver = "NetDriver";
        }
        Application.Init();
        _gridViewDetails = new GridViewDetails
        {
            // If OutputMode is Single or Multiple, then we make items selectable. If we make them selectable,
            // 2 columns are required for the check/selection indicator and space.
            ListViewOffset = _applicationData.OutputMode != OutputModeOption.None ? MARGIN_LEFT + CHECK_WIDTH : MARGIN_LEFT
        };

        Window win = CreateTopLevelWindow();

        // Create the headers and calculate column widths based on the DataTable
        List<string> gridHeaders = _applicationData.DataTable.DataColumns.Select((c) => c.Label).ToList();

        // Copy the input DataTable into our master ListView source list; upon exit any items
        // that are IsMarked are returned (if Outputmode is set)
        _inputSource = LoadData();

        if (!_applicationData.MinUI)
        {
            // Add Filter UI
            AddFilter(win);
            // Add Header UI
            AddHeaders(win, gridHeaders);
        }

        // Add ListView
        AddListView(win);

        // Status bar is where our key-bindings are handled
        AddStatusBar(win, !_applicationData.MinUI);

        // We *always* apply a filter, even if the -Filter parameter is not set or Filtering is not
        // available. The ListView always shows a fitlered version of _inputSource even if there is no
        // actual fitler. 
        //ApplyFilter();

        _listView?.SetFocus();

        win.SubViewLayout += OnWinSubViewLayout;

        // Run the GUI.
        Application.Run(win);
        win.Dispose();
        Application.Shutdown();

        // Return results of selection if required.
        HashSet<int> selectedIndexes = new HashSet<int>();
        if (_cancelled)
        {
            return selectedIndexes;
        }

        // Return any items that were selected.
        foreach (GridViewRow gvr in _inputSource.GridViewRowList)
        {
            if (gvr.IsMarked)
            {
                selectedIndexes.Add(gvr.OriginalIndex);
            }
        }

        return selectedIndexes;

        void OnWinSubViewLayout(object? sender, EventArgs e)
        {
            CalculateColumnWidths(gridHeaders);

            _header!.Text = GridViewHelpers.GetPaddedString(gridHeaders, _gridViewDetails!.ListViewOffset,
                _gridViewDetails.ListViewColumnWidths);
            UpdateDisplayStrings(_listViewSource);
            ApplyFilter();
        }
    }

    private GridViewDataSource LoadData()
    {
        var items = new List<GridViewRow>();
        if (_applicationData == null)
            return new GridViewDataSource(items);

        for (int i = 0; i < _applicationData.DataTable.Data.Count; i++)
        {
            var dataTableRow = _applicationData.DataTable.Data[i];
            var valueList = new List<string>();
            foreach (var dataTableColumn in _applicationData.DataTable.DataColumns)
            {
                string dataValue = dataTableRow.Values[dataTableColumn.ToString()].DisplayValue;
                valueList.Add(dataValue);
            }

            string displayString = GridViewHelpers.GetPaddedString(valueList, 0, _gridViewDetails?.ListViewColumnWidths);

            items.Add(new GridViewRow
            {
                DisplayString = displayString,
                // We use this to keep _inputSource up to date when a filter is applied
                OriginalIndex = i
            });
        }

        return new GridViewDataSource(items);
    }

    private void UpdateDisplayStrings(GridViewDataSource? source)
    {
        if (source == null) return;
        foreach (var gvr in source.GridViewRowList)
        {
            var valueList = new List<string>();
            var dataTableRow = _applicationData!.DataTable.Data[gvr.OriginalIndex];
            foreach (var dataTableColumn in _applicationData.DataTable.DataColumns)
            {
                string dataValue = dataTableRow.Values[dataTableColumn.ToString()].DisplayValue;
                valueList.Add(dataValue);
            }
            gvr.DisplayString = GridViewHelpers.GetPaddedString(valueList, 0, _gridViewDetails?.ListViewColumnWidths);
        }
    }
    private void ApplyFilter()
    {
        // The ListView is always filled with a (filtered) copy of _inputSource.
        // We listen for `MarkChanged` events on this filtered list and apply those changes up to _inputSource.

        GridViewRow? selectedItem = null;

        if (_listViewSource != null)
        {
            // Get the item that is currently selected so we can restore selection after re-applying filter
            selectedItem = _listViewSource?.GridViewRowList.ElementAtOrDefault(_listView?.SelectedItem ?? 0);
            _listViewSource!.MarkChanged -= ListViewSource_MarkChanged;
            _listViewSource = null;
        }

        if (_inputSource is null)
        {
            _inputSource = LoadData();
        }


        if (_applicationData != null)
        {
            try
            {
                _listViewSource = new GridViewDataSource(GridViewHelpers.FilterData(_inputSource.GridViewRowList,
                    _applicationData.Filter ?? string.Empty));
            }
            catch (RegexParseException ex)
            {
                _filterErrorView!.Text = ex.Message;
            }
        }

        _listViewSource?.MarkChanged += ListViewSource_MarkChanged;
        _listView?.Source = _listViewSource;

        // Restore selection - find the previously selected item in the new filtered list
        if (selectedItem is not null && _listViewSource != null)
        {
            int newIndex =
                _listViewSource.GridViewRowList.FindIndex(i => i.OriginalIndex == selectedItem.OriginalIndex);
            if (newIndex >= 0)
            {
                _listView!.SelectedItem = newIndex;
            }
        }
        if (_listView?.SelectedItem == -1)
        {
            _listView!.SelectedItem = 0;
        }
    }

    private void ListViewSource_MarkChanged(object? s, GridViewDataSource.RowMarkedEventArgs a)
    {
        _inputSource?.GridViewRowList[a.Row.OriginalIndex].IsMarked = a.Row.IsMarked;
    }

    private static void Accept()
    {
        Application.RequestStop();
    }

    private void Close()
    {
        _cancelled = true;
        Application.RequestStop();
    }

    private Window CreateTopLevelWindow()
    {
        // Creates the top-level window to show
        var win = new Window
        {
            Title = _applicationData!.Title ?? "Out-ConsoleGridView",
        };

        if (_applicationData.MinUI)
        {
            win.BorderStyle = LineStyle.None;
        }

        return win;
    }

    private void AddStatusBar(Window win, bool visible)
    {
        var shortcuts = new List<Shortcut>();
        if (_applicationData!.OutputMode != OutputModeOption.None)
        {
            // Use Key.Empty for SPACE with no delegate because ListView already
            // handles SPACE
            shortcuts.Add(new Shortcut(Key.Space, "Select Item", null));
        }

        if (_applicationData.OutputMode == OutputModeOption.Multiple)
        {
            shortcuts.Add(new Shortcut(Key.A.WithCtrl, "Select All", () =>
            {
                // This selects only the items that match the Filter
                var gvds = _listView!.Source as GridViewDataSource;
                gvds!.GridViewRowList.ForEach(i => i.IsMarked = true);
                _listView.SetNeedsDraw();
            }));

            // Ctrl-D is commonly used in GUIs for select-none 
            shortcuts.Add(new Shortcut(Key.D.WithCtrl, "Select None", () =>
            {
                // This un-selects only the items that match the Filter
                var gvds = _listView!.Source as GridViewDataSource;
                gvds!.GridViewRowList.ForEach(i => i.IsMarked = false);
                _listView.SetNeedsDraw();
            }));
        }

        if (_applicationData.OutputMode != OutputModeOption.None)
        {
            shortcuts.Add(new Shortcut(Key.Enter, "Accept", () =>
            {
                if (Application.Top?.MostFocused == _listView)
                {
                    // If nothing was explicitly marked, we return the item that was selected
                    // when ENTER is pressed in Single mode. If something was previously selected
                    // (using SPACE) then honor that as the single item to return
                    if (_applicationData.OutputMode == OutputModeOption.Single &&
                        _inputSource!.GridViewRowList.Find(i => i.IsMarked) == null)
                    {
                        // Toggle the mark on the currently selected item
                        if (_listView!.SelectedItem >= 0 && _listView.SelectedItem < _listViewSource!.Count)
                        {
                            var item = _listViewSource.GridViewRowList[_listView.SelectedItem];
                            item.IsMarked = !item.IsMarked;
                        }
                    }
                    Accept();
                }
                else if (Application.Top?.MostFocused == _filterField)
                {
                    _listView!.SetFocus();
                }
            }));
        }

        shortcuts.Add(new Shortcut(Key.Esc, "Close", Close));
        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            shortcuts.Add(new Shortcut(Key.Empty,
            $"{Application.Driver} v{FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location).ProductVersion}", null));
        }

        win.Add(new StatusBar(shortcuts));
    }

    private void CalculateColumnWidths(List<string> gridHeaders)
    {
        _gridViewDetails!.ListViewColumnWidths = new int[gridHeaders.Count];
        var listViewColumnWidths = _gridViewDetails.ListViewColumnWidths;

        for (int i = 0; i < gridHeaders.Count; i++)
        {
            listViewColumnWidths[i] = gridHeaders[i].Length;
        }

        // calculate the width of each column based on longest string in each column for each row
        foreach (var row in _applicationData.DataTable.Data)
        {
            int index = 0;

            // use half of the visible buffer height for the number of objects to inspect to calculate widths
            foreach (var col in row.Values.Take(Application.Top.Frame.Height / 2))
            {
                var len = col.Value.DisplayValue.Length;
                if (len > listViewColumnWidths[index])
                {
                    listViewColumnWidths[index] = len;
                }
                index++;
            }
        }

        // if the total width is wider than the usable width, remove 1 from widest column until it fits
        _gridViewDetails.UsableWidth = Application.Top.Frame.Width - MARGIN_LEFT - listViewColumnWidths.Length - _gridViewDetails.ListViewOffset;
        int columnWidthsSum = listViewColumnWidths.Sum();
        while (columnWidthsSum >= _gridViewDetails.UsableWidth)
        {
            int maxWidth = 0;
            int maxIndex = 0;
            for (int i = 0; i < listViewColumnWidths.Length; i++)
            {
                if (listViewColumnWidths[i] > maxWidth)
                {
                    maxWidth = listViewColumnWidths[i];
                    maxIndex = i;
                }
            }

            listViewColumnWidths[maxIndex]--;
            columnWidthsSum--;
        }
    }

    private void AddFilter(Window win)
    {
        _filterLabel = new Label
        {
            Text = FILTER_LABEL,
            X = MARGIN_LEFT,
            Y = 0
        };

        _filterField = new TextField
        {
            Text = _applicationData!.Filter ?? string.Empty,
            X = Pos.Right(_filterLabel) + 1,
            Y = Pos.Top(_filterLabel),
            CanFocus = true,
            Width = Dim.Fill() - 1
        };

        // TextField captures Ctrl-A (select all text) and Ctrl-D (delete backwards)
        // In OCGV these are used for select-all/none of items. Selecting items is more
        // common than editing the filter field so we turn them off in the filter textview. 
        // BACKSPACE still works for delete backwards
        _filterField.KeyBindings.Remove(Key.A.WithCtrl);
        _filterField.KeyBindings.Remove(Key.D.WithCtrl);

        _filterErrorView = new View
        {
            Text = string.Empty,
            X = Pos.Right(_filterLabel) + 1,
            Y = Pos.Top(_filterLabel) + 1,
            Width = Dim.Fill() - _filterLabel.Text!.Length,
            // This enables the height to go 0, and the view to disappear when there is no error
            Height = Dim.Auto(DimAutoStyle.Text),
            SchemeName = "Error"
        };

        _filterField.TextChanged += (sender, e) =>
        {
            string? filterText = _filterField.Text?.ToString();
            try
            {
                _filterErrorView.Text = string.Empty;
                _filterErrorView.SetNeedsDraw();
                _applicationData!.Filter = filterText!;
                ApplyFilter();

            }
            catch (Exception ex)
            {
                _filterErrorView.Text = ex.Message;
            }
        };

        win.Add(_filterLabel, _filterField, _filterErrorView);

        _filterField.Text = _applicationData.Filter ?? string.Empty;
        _filterField.CursorPosition = _filterField.Text.Length;
    }

    private void AddHeaders(Window win, List<string> gridHeaders)
    {
        _header = new Label
        {
            //Text = GridViewHelpers.GetPaddedString(gridHeaders, _gridViewDetails!.ListViewOffset, _gridViewDetails.ListViewColumnWidths),
        };
        if (_applicationData!.MinUI)
        {
            _header.Y = 0;
        }
        else
        {
            _header.Y = Pos.Bottom(_filterErrorView!);
        }
        win.Add(_header);

        if (!_applicationData.MinUI)
        {
            var headerLine = new Line()
            {
                X = MARGIN_LEFT,
                Y = Pos.Bottom(_header),
                Width = Dim.Fill(MARGIN_LEFT),
            };
            win.Add(headerLine);
        }
    }

    private void AddListView(Window win)
    {
        _listView = new ListView
        {
            Source = _inputSource,
            X = MARGIN_LEFT
        };
        if (!_applicationData!.MinUI)
        {
            _listView.Y = Pos.Bottom(_filterLabel!) + 2; // 1 for space, 1 for header, 1 for header underline
        }
        else
        {
            _listView.Y = 1; // 1 for space, 1 for header, 1 for header underline
        }
        _listView.Width = Dim.Fill(1);
        _listView.Height = Dim.Fill();
        _listView.AllowsMarking = _applicationData.OutputMode != OutputModeOption.None;
        _listView.AllowsMultipleSelection = _applicationData.OutputMode == OutputModeOption.Multiple;

        _listView.SelectedItem = 0;

        win.Add(_listView);
    }

    public void Dispose()
    {
        if (!Console.IsInputRedirected)
        {
            // By emitting this, we fix two issues:
            // 1. An issue where arrow keys don't work in the console because .NET
            //    requires application mode to support Arrow key escape sequences.
            //    Esc[?1h sets the cursor key to application mode
            //    See http://ascii-table.com/ansi-escape-sequences-vt-100.php
            // 2. An issue where moving the mouse causes characters to show up because
            //    mouse tracking is still on. Esc[?1003l turns it off.
            //    See https://www.xfree86.org/current/ctlseqs.html#Mouse%20Tracking
            Console.Write("\u001b[?1h\u001b[?1003l");
        }
    }
}
