// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     A specialized view for displaying grid column headers with individual subviews for each column. This is added to
///     the top of the Padding of the ListView.
/// </summary>
internal sealed class Header : View
{
    public Header()
    {
        Height = 1;
        CanFocus = false;
        Width = Dim.Fill();
    }

    public override void EndInit()
    {
        base.EndInit();


        // We are a subview of the ListView.Padding. 
        if (SuperView is Padding padding) padding.Parent?.ViewportChanged += ListViewOnViewportChanged;

        return;

        void ListViewOnViewportChanged(object? sender, DrawEventArgs e)
        {
            if (sender is ListView listView)
                Viewport = Viewport with { X = listView.Viewport.X };
        }
    }

    protected override void OnSubViewLayout(LayoutEventArgs args)
    {
        if (SuperView is Padding { Parent: ListView listView })
            SetContentSize(GetContentSize() with { Width = listView.GetContentSize().Width });

        base.OnSubViewLayout(args);
    }

    /// <summary>
    ///     Updates the header with new column strings and widths.
    /// </summary>
    /// <param name="headers">The list of header strings to display.</param>
    /// <param name="columnWidths">The width of each column.</param>
    public void SetHeaders(List<string>? headers, int[]? columnWidths)
    {
        if (headers == null || columnWidths == null)
            return;

        // Clear existing labels
        RemoveAll();

        // Create a label for each header
        var currentX = 0;
        for (var i = 0; i < headers.Count; i++)
        {
            // Skip columns with zero width
            if (columnWidths[i] <= 0)
                continue;

            var column = new View
            {
                Text = headers[i],
                X = currentX,
                Y = 0,
                Width = Dim.Auto(DimAutoStyle.Text),
                Height = 1,
                TextAlignment = Alignment.Start,
                VerticalTextAlignment = Alignment.Start
            };
            column.GettingAttributeForRole += ColumnOnGettingAttributeForRole;

            void ColumnOnGettingAttributeForRole(object? sender, VisualRoleEventArgs e)
            {
                if (e.Role == VisualRole.Normal)
                {
                    e.Result = e.Result!.Value with { Style = TextStyle.Bold | TextStyle.Underline };
                    e.Handled = true;
                }
            }

            Add(column);

            // Move to next column position (width + 1 space separator)
            currentX += columnWidths[i] + 1;
        }
    }
}