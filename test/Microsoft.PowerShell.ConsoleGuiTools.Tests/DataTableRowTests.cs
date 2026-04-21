// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.PowerShell.OutGridView.Models;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

public class DataTableRowTests
{
    [Fact]
    public void DecimalValue_CompareTo_LessThan()
    {
        var a = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        var b = new DecimalValue { DisplayValue = "2", SortValue = 2m };
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void DecimalValue_CompareTo_GreaterThan()
    {
        var a = new DecimalValue { DisplayValue = "2", SortValue = 2m };
        var b = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        Assert.True(a.CompareTo(b) > 0);
    }

    [Fact]
    public void DecimalValue_CompareTo_Equal()
    {
        var a = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        var b = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void DecimalValue_CompareTo_NonDecimalValue_Returns1()
    {
        var a = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        Assert.Equal(1, a.CompareTo("not a decimal value"));
    }

    [Fact]
    public void DecimalValue_CompareTo_Null_Returns1()
    {
        var a = new DecimalValue { DisplayValue = "1", SortValue = 1m };
        Assert.Equal(1, a.CompareTo(null));
    }

    [Fact]
    public void DecimalValue_OriginalValue_ReturnsSortValue()
    {
        var v = new DecimalValue { DisplayValue = "42", SortValue = 42m };
        Assert.Equal(42m, v.OriginalValue);
    }

    [Fact]
    public void StringValue_CompareTo_LessThan()
    {
        var a = new StringValue { DisplayValue = "apple" };
        var b = new StringValue { DisplayValue = "banana" };
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void StringValue_CompareTo_GreaterThan()
    {
        var a = new StringValue { DisplayValue = "banana" };
        var b = new StringValue { DisplayValue = "apple" };
        Assert.True(a.CompareTo(b) > 0);
    }

    [Fact]
    public void StringValue_CompareTo_Equal()
    {
        var a = new StringValue { DisplayValue = "same" };
        var b = new StringValue { DisplayValue = "same" };
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void StringValue_CompareTo_NonStringValue_Returns1()
    {
        var a = new StringValue { DisplayValue = "test" };
        Assert.Equal(1, a.CompareTo(42));
    }

    [Fact]
    public void StringValue_OriginalValue_WithRawValue_ReturnsRawValue()
    {
        var raw = new object();
        var v = new StringValue { DisplayValue = "display", RawValue = raw };
        Assert.Same(raw, v.OriginalValue);
    }

    [Fact]
    public void StringValue_OriginalValue_WithoutRawValue_ReturnsDisplayValue()
    {
        var v = new StringValue { DisplayValue = "display" };
        Assert.Equal("display", v.OriginalValue);
    }

    [Fact]
    public void DataTableRow_Constructor_SetsProperties()
    {
        var values = new Dictionary<string, IValue>
        {
            ["col1"] = new StringValue { DisplayValue = "value1" }
        };
        var row = new DataTableRow(values, 5);

        Assert.Same(values, row.Values);
        Assert.Equal(5, row.OriginalObjectIndex);
    }

    [Fact]
    public void DataTable_Constructor_SetsProperties()
    {
        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Name", "$_.Name")
        };
        var rows = new List<DataTableRow>();
        var table = new DataTable(columns, rows);

        Assert.Same(columns, table.DataColumns);
        Assert.Same(rows, table.Data);
    }
}
