// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     An <see cref="ITableSource" /> implementation that wraps the OutGridView <see cref="DataTable" /> model.
///     Supports thread-safe row addition for streaming pipeline support (Issue #209).
/// </summary>
internal sealed class OutTableViewDataSource : ITableSource
{
    private readonly List<DataTableColumn> _columns;
    private readonly List<DataTableRow> _rows;
    private readonly object _lock = new();

    /// <summary>
    ///     Creates an empty data source with the specified columns.
    /// </summary>
    public OutTableViewDataSource(List<DataTableColumn> columns)
    {
        _columns = columns;
        _rows = new List<DataTableRow>();
    }

    /// <summary>
    ///     Creates a data source with pre-populated rows.
    /// </summary>
    public OutTableViewDataSource(List<DataTableColumn> columns, List<DataTableRow> rows)
    {
        _columns = columns;
        _rows = new List<DataTableRow>(rows);
    }

    /// <inheritdoc />
    public string[] ColumnNames => _columns.Select(c => c.Label).ToArray();

    /// <inheritdoc />
    public int Columns => _columns.Count;

    /// <inheritdoc />
    public int Rows
    {
        get { lock (_lock) return _rows.Count; }
    }

    /// <inheritdoc />
    public object this[int row, int col]
    {
        get
        {
            lock (_lock)
            {
                if (row < 0 || row >= _rows.Count || col < 0 || col >= _columns.Count)
                    return string.Empty;

                var dataRow = _rows[row];
                var columnKey = _columns[col].ToString();
                return dataRow.Values.TryGetValue(columnKey, out var value)
                    ? value.DisplayValue
                    : string.Empty;
            }
        }
    }

    /// <summary>
    ///     Thread-safely adds a row to the data source.
    /// </summary>
    public void AddRow(DataTableRow row)
    {
        lock (_lock) _rows.Add(row);
    }

    /// <summary>
    ///     Gets the original object index for the specified row.
    /// </summary>
    public int GetOriginalObjectIndex(int row)
    {
        lock (_lock) return row >= 0 && row < _rows.Count ? _rows[row].OriginalObjectIndex : -1;
    }

    /// <summary>
    ///     Gets a snapshot of all rows.
    /// </summary>
    public List<DataTableRow> GetAllRows()
    {
        lock (_lock) return new List<DataTableRow>(_rows);
    }

    /// <summary>
    ///     Gets the column definitions.
    /// </summary>
    public List<DataTableColumn> GetColumns() => _columns;

    /// <summary>
    ///     Creates a new filtered data source containing only rows matching the regex pattern.
    /// </summary>
    public OutTableViewDataSource Filter(string pattern)
    {
        List<DataTableRow> allRows;
        lock (_lock) allRows = new List<DataTableRow>(_rows);

        if (string.IsNullOrEmpty(pattern))
            return new OutTableViewDataSource(_columns, allRows);

        var result = allRows.Where(r => RowMatchesFilter(r, pattern)).ToList();

        return new OutTableViewDataSource(_columns, result);
    }

    /// <summary>
    ///     Creates a data source from an existing <see cref="DataTable" />.
    /// </summary>
    public static OutTableViewDataSource FromDataTable(DataTable dataTable)
    {
        return new OutTableViewDataSource(dataTable.DataColumns, dataTable.Data);
    }

    private bool RowMatchesFilter(DataTableRow row, string pattern)
    {
        foreach (var column in _columns)
        {
            var columnKey = column.ToString();
            if (row.Values.TryGetValue(columnKey, out var value) &&
                Regex.IsMatch(value.DisplayValue, pattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}
