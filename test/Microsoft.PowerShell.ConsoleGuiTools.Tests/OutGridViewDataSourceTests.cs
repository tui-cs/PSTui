// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.OutGridView.Models;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

/// <summary>
///     Unit tests for <see cref="OutGridViewDataSource" /> covering filtering, sorting,
///     thread-safe row addition, and index tracking.
/// </summary>
public class OutGridViewDataSourceTests
{
    #region Helpers

    private static List<DataTableColumn> CreateColumns(params string[] names)
    {
        var columns = new List<DataTableColumn>();
        foreach (var name in names)
            columns.Add(new DataTableColumn(name, name));
        return columns;
    }

    private static DataTableRow CreateRow(List<DataTableColumn> columns, int originalIndex, params string[] values)
    {
        var dict = new Dictionary<string, IValue>();
        for (int i = 0; i < columns.Count && i < values.Length; i++)
            dict[columns[i].ToString()] = new StringValue { DisplayValue = values[i] };
        return new DataTableRow(dict, originalIndex);
    }

    private static OutGridViewDataSource CreateTestDataSource()
    {
        var columns = CreateColumns("Name", "Id");
        var ds = new OutGridViewDataSource(columns);
        ds.AddRow(CreateRow(columns, 0, "Charlie", "3"));
        ds.AddRow(CreateRow(columns, 1, "Alice", "1"));
        ds.AddRow(CreateRow(columns, 2, "Bob", "2"));
        return ds;
    }

    #endregion

    #region Construction and Basic Properties

    [Fact]
    public void Constructor_EmptyDataSource_HasZeroRows()
    {
        var columns = CreateColumns("Name", "Id");
        var ds = new OutGridViewDataSource(columns);

        Assert.Equal(0, ds.Rows);
        Assert.Equal(2, ds.Columns);
    }

    [Fact]
    public void Constructor_WithRows_HasCorrectRowCount()
    {
        var columns = CreateColumns("Name");
        var rows = new List<DataTableRow>
        {
            CreateRow(columns, 0, "A"),
            CreateRow(columns, 1, "B")
        };
        var ds = new OutGridViewDataSource(columns, rows);

        Assert.Equal(2, ds.Rows);
    }

    [Fact]
    public void ColumnNames_ReturnsLabels()
    {
        var columns = CreateColumns("Name", "Id", "Status");
        var ds = new OutGridViewDataSource(columns);

        Assert.Equal(new[] { "Name", "Id", "Status" }, ds.ColumnNames);
    }

    [Fact]
    public void Indexer_ReturnsCorrectCellValue()
    {
        var ds = CreateTestDataSource();

        Assert.Equal("Charlie", ds[0, 0]);
        Assert.Equal("3", ds[0, 1]);
        Assert.Equal("Alice", ds[1, 0]);
        Assert.Equal("1", ds[1, 1]);
    }

    [Fact]
    public void Indexer_OutOfBounds_ReturnsEmptyString()
    {
        var ds = CreateTestDataSource();

        Assert.Equal(string.Empty, ds[-1, 0]);
        Assert.Equal(string.Empty, ds[99, 0]);
        Assert.Equal(string.Empty, ds[0, -1]);
        Assert.Equal(string.Empty, ds[0, 99]);
    }

    #endregion

    #region AddRow

    [Fact]
    public void AddRow_IncreasesRowCount()
    {
        var columns = CreateColumns("Name");
        var ds = new OutGridViewDataSource(columns);

        ds.AddRow(CreateRow(columns, 0, "First"));
        Assert.Equal(1, ds.Rows);

        ds.AddRow(CreateRow(columns, 1, "Second"));
        Assert.Equal(2, ds.Rows);
    }

    [Fact]
    public async Task AddRow_ThreadSafe_ConcurrentAdds()
    {
        var columns = CreateColumns("Name");
        var ds = new OutGridViewDataSource(columns);
        const int threadCount = 10;
        const int rowsPerThread = 100;

        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < rowsPerThread; i++)
                    ds.AddRow(CreateRow(columns, threadId * rowsPerThread + i, $"Row_{threadId}_{i}"));
            });
        }

        await Task.WhenAll(tasks);
        Assert.Equal(threadCount * rowsPerThread, ds.Rows);
    }

    #endregion

    #region GetOriginalObjectIndex

    [Fact]
    public void GetOriginalObjectIndex_ReturnsCorrectIndex()
    {
        var ds = CreateTestDataSource();

        Assert.Equal(0, ds.GetOriginalObjectIndex(0));
        Assert.Equal(1, ds.GetOriginalObjectIndex(1));
        Assert.Equal(2, ds.GetOriginalObjectIndex(2));
    }

    [Fact]
    public void GetOriginalObjectIndex_OutOfBounds_ReturnsNegativeOne()
    {
        var ds = CreateTestDataSource();

        Assert.Equal(-1, ds.GetOriginalObjectIndex(-1));
        Assert.Equal(-1, ds.GetOriginalObjectIndex(99));
    }

    #endregion

    #region Filter

    [Fact]
    public void Filter_EmptyPattern_ReturnsSelf()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("");
        Assert.Same(ds, filtered);
    }

    [Fact]
    public void Filter_NullPattern_ReturnsSelf()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter(null!);
        Assert.Same(ds, filtered);
    }

    [Fact]
    public void Filter_MatchesSubstring_ReturnsMatchingRows()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("Ali");
        Assert.Equal(1, filtered.Rows);
        Assert.Equal("Alice", filtered[0, 0]);
    }

    [Fact]
    public void Filter_CaseInsensitive()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("alice");
        Assert.Equal(1, filtered.Rows);
        Assert.Equal("Alice", filtered[0, 0]);
    }

    [Fact]
    public void Filter_Regex_MatchesPattern()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("^[AB]");
        Assert.Equal(2, filtered.Rows);
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("ZZZ");
        Assert.Equal(0, filtered.Rows);
    }

    [Fact]
    public void Filter_MatchesAnyColumn()
    {
        var ds = CreateTestDataSource();

        // "3" is in the Id column of Charlie's row
        var filtered = ds.Filter("^3$");
        Assert.Equal(1, filtered.Rows);
        Assert.Equal("Charlie", filtered[0, 0]);
    }

    [Fact]
    public void Filter_PreservesOriginalObjectIndex()
    {
        var ds = CreateTestDataSource();

        var filtered = ds.Filter("Bob");
        Assert.Equal(1, filtered.Rows);
        Assert.Equal(2, filtered.GetOriginalObjectIndex(0)); // Bob was at index 2
    }

    [Fact]
    public void Filter_InvalidRegex_ThrowsRegexParseException()
    {
        var ds = CreateTestDataSource();

        Assert.ThrowsAny<System.Text.RegularExpressions.RegexParseException>(() => ds.Filter("[invalid"));
    }

    #endregion

    #region Sort

    [Fact]
    public void Sort_Ascending_ByName()
    {
        var ds = CreateTestDataSource(); // Charlie, Alice, Bob

        var sorted = ds.Sort(0, descending: false);

        Assert.Equal("Alice", sorted[0, 0]);
        Assert.Equal("Bob", sorted[1, 0]);
        Assert.Equal("Charlie", sorted[2, 0]);
    }

    [Fact]
    public void Sort_Descending_ByName()
    {
        var ds = CreateTestDataSource();

        var sorted = ds.Sort(0, descending: true);

        Assert.Equal("Charlie", sorted[0, 0]);
        Assert.Equal("Bob", sorted[1, 0]);
        Assert.Equal("Alice", sorted[2, 0]);
    }

    [Fact]
    public void Sort_NumericAware_ById()
    {
        var columns = CreateColumns("Name", "Id");
        var ds = new OutGridViewDataSource(columns);
        ds.AddRow(CreateRow(columns, 0, "A", "10"));
        ds.AddRow(CreateRow(columns, 1, "B", "2"));
        ds.AddRow(CreateRow(columns, 2, "C", "1"));

        var sorted = ds.Sort(1, descending: false);

        // Numeric sort: 1, 2, 10 (not lexicographic: 1, 10, 2)
        Assert.Equal("1", sorted[0, 1]);
        Assert.Equal("2", sorted[1, 1]);
        Assert.Equal("10", sorted[2, 1]);
    }

    [Fact]
    public void Sort_PreservesOriginalObjectIndex()
    {
        var ds = CreateTestDataSource(); // Charlie(0), Alice(1), Bob(2)

        var sorted = ds.Sort(0, descending: false); // Alice, Bob, Charlie

        Assert.Equal(1, sorted.GetOriginalObjectIndex(0)); // Alice was index 1
        Assert.Equal(2, sorted.GetOriginalObjectIndex(1)); // Bob was index 2
        Assert.Equal(0, sorted.GetOriginalObjectIndex(2)); // Charlie was index 0
    }

    [Fact]
    public void Sort_AddsAscendingGlyph()
    {
        var ds = CreateTestDataSource();

        var sorted = ds.Sort(0, descending: false);

        Assert.Equal("Name ▲", sorted.ColumnNames[0]);
        Assert.Equal("Id", sorted.ColumnNames[1]); // Other columns unchanged
    }

    [Fact]
    public void Sort_AddsDescendingGlyph()
    {
        var ds = CreateTestDataSource();

        var sorted = ds.Sort(0, descending: true);

        Assert.Equal("Name ▼", sorted.ColumnNames[0]);
    }

    [Fact]
    public void Sort_InvalidColumnIndex_ReturnsSelf()
    {
        var ds = CreateTestDataSource();

        Assert.Same(ds, ds.Sort(-1, false));
        Assert.Same(ds, ds.Sort(99, false));
    }

    [Fact]
    public void Sort_DoesNotMutateOriginal()
    {
        var ds = CreateTestDataSource();

        var sorted = ds.Sort(0, descending: false);

        // Original still has Charlie first
        Assert.Equal("Charlie", ds[0, 0]);
        // Sorted has Alice first
        Assert.Equal("Alice", sorted[0, 0]);
    }

    #endregion

    #region GetColumns

    [Fact]
    public void GetColumns_ReturnsSameColumnList()
    {
        var columns = CreateColumns("A", "B");
        var ds = new OutGridViewDataSource(columns);

        Assert.Same(columns, ds.GetColumns());
    }

    #endregion
}
