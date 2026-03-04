// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides a data source implementation for the grid view that manages rows and supports marking and rendering.
/// </summary>
internal sealed class GridViewDataSource : IListDataSource
{
    /// <summary>
    ///     Gets or sets the list of rows displayed in the grid view.
    /// </summary>
    public List<GridViewRow> GridViewRowList { get; set; }

    /// <inheritdoc/>
    public int Count => GridViewRowList.Count;

    /// <inheritdoc/>
    public int MaxItemLength { get; set; }

    /// <inheritdoc/>
    public bool SuspendCollectionChangedEvent { get; set; }

#pragma warning disable CS0067  
    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
#pragma warning restore CS0067

    /// <summary>
    ///     Initializes a new instance of the <see cref="GridViewDataSource" /> class with the specified item list.
    /// </summary>
    /// <param name="itemList">The list of grid view rows to display.</param>
    public GridViewDataSource(List<GridViewRow> itemList)
    {
        GridViewRowList = itemList;
    }

    /// <inheritdoc/>
    public void Render(ListView listView, bool selected, int item, int col, int line, int width, int viewportX)
    {
        GridViewRow? row = GridViewRowList[item];
        string displayString = row?.DisplayString ?? string.Empty;

        displayString = viewportX switch
        {
            // Truncate the start of the string to skip characters scrolled out of view
            > 0 when displayString.Length > viewportX => displayString[viewportX..],
            > 0 when displayString.Length <= viewportX => string.Empty,
            _ => displayString
        };

        // Pad right of display string with spaces to fill width, or truncate if too long
        if (displayString.Length < width)
        {
            displayString = displayString.PadRight(width);
        }
        else if (displayString.Length > width)
        {
            displayString = displayString[..width];
        }

        listView.AddStr(displayString);
    }

    /// <inheritdoc/>
    public bool IsMarked(int item)
    {
        return item < GridViewRowList.Count && GridViewRowList[item].IsMarked;
    }

    /// <inheritdoc/>
    public void SetMark(int item, bool value)
    {
        var oldValue = GridViewRowList[item].IsMarked;
        GridViewRowList[item].IsMarked = value;
        var args = new RowMarkedEventArgs
        {
            Row = GridViewRowList[item],
            OldValue = oldValue
        };
        MarkChanged?.Invoke(this, args);
    }

    /// <summary>
    ///     Provides data for the <see cref="MarkChanged" /> event.
    /// </summary>
    public sealed class RowMarkedEventArgs : EventArgs
    {
        /// <summary>
        ///     Gets or sets the row that was marked or unmarked.
        /// </summary>
        public required GridViewRow Row { get; set; }

        /// <summary>
        ///     Gets or sets the previous marked state of the row.
        /// </summary>
        public bool OldValue { get; set; }
    }

    /// <summary>
    ///     Occurs when a row's marked state changes.
    /// </summary>
    public event EventHandler<RowMarkedEventArgs>? MarkChanged;

    /// <summary>
    ///     Converts the data source to a list.
    /// </summary>
    /// <returns>The grid view row list as an <see cref="IList" />.</returns>
    public IList ToList() => GridViewRowList;

    /// <summary>
    ///     Releases all resources used by the <see cref="GridViewDataSource" />.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose currently
    }
}
