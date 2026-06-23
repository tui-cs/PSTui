// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace PSTui.Models;

/// <summary>
///     Represents a data table containing rows and columns for display in the grid view.
/// </summary>
public class DataTable
{
    /// <summary>
    ///     Gets or sets the list of data rows in the table.
    /// </summary>
    public List<DataTableRow> Data { get; set; }

    /// <summary>
    ///     Gets or sets the list of column definitions for the table.
    /// </summary>
    public List<DataTableColumn> DataColumns { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataTable" /> class with the specified columns and data.
    /// </summary>
    /// <param name="columns">The list of column definitions for the table.</param>
    /// <param name="data">The list of data rows for the table.</param>
    public DataTable(List<DataTableColumn> columns, List<DataTableRow> data)
    {
        DataColumns = columns;
        Data = data;
    }
}