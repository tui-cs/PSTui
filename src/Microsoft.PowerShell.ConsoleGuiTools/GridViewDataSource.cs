// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

    /// <summary>
    ///     Gets the number of rows in the data source.
    /// </summary>
    public int Count => GridViewRowList.Count;

    /// <summary>
    ///     Gets the number of rows in the data source.
    /// </summary>
    public int Length => GridViewRowList.Count;

    /// <summary>
    ///     Gets or sets a value indicating whether to suspend raising the <see cref="CollectionChanged" /> event.
    /// </summary>
    public bool SuspendCollectionChangedEvent { get; set; }

#pragma warning disable CS0067  
    /// <summary>
    ///     Occurs when the collection changes.
    /// </summary>
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

    /// <summary>
    ///     Renders a specific item in the list view at the specified position.
    /// </summary>
    /// <param name="listView">The list view to render into.</param>
    /// <param name="selected">A value indicating whether the item is selected.</param>
    /// <param name="item">The index of the item to render.</param>
    /// <param name="col">The column position to start rendering.</param>
    /// <param name="line">The line position to render on.</param>
    /// <param name="width">The width available for rendering.</param>
    /// <param name="start">The starting position within the item's display string.</param>
    public void Render(ListView listView, bool selected, int item, int col, int line, int width, int start = 0)
    {
        listView.Move(col - start, line);

        var driver = listView.App?.Driver;
        var row = GridViewRowList[item];
        string displayString = row.DisplayString ?? string.Empty;
        // Pad right of display string with spaces to fill width
        if (displayString.Length < width)
        {
            displayString = displayString.PadRight(width);
        }
        else if (displayString.Length > width)
        {
            displayString = displayString[..width];
        }
        driver!.AddStr(displayString);
    }

    /// <summary>
    ///     Determines whether the specified item is marked.
    /// </summary>
    /// <param name="item">The index of the item to check.</param>
    /// <returns><see langword="true" /> if the item is marked; otherwise, <see langword="false" />.</returns>
    public bool IsMarked(int item) => GridViewRowList[item].IsMarked;

    /// <summary>
    ///     Sets the marked state of the specified item and raises the <see cref="MarkChanged" /> event.
    /// </summary>
    /// <param name="item">The index of the item to mark or unmark.</param>
    /// <param name="value"><see langword="true" /> to mark the item; <see langword="false" /> to unmark it.</param>
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