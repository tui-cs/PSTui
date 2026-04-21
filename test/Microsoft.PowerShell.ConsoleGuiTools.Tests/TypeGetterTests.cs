// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation;
using Microsoft.PowerShell.OutGridView.Models;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

public class TypeGetterTests
{
    [Fact]
    public void CastObjectsToTableView_EmptyList_ReturnsEmptyTable()
    {
        var result = TypeGetter.CastObjectsToTableView([]);

        Assert.Empty(result.DataColumns);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void CastObjectsToTableView_PrimitiveStrings_SingleColumn()
    {
        var objects = new List<PSObject>
        {
            new PSObject("hello"),
            new PSObject("world")
        };

        var result = TypeGetter.CastObjectsToTableView(objects);

        Assert.Single(result.DataColumns);
        Assert.Equal("String", result.DataColumns[0].Label);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public void CastObjectsToTableView_IntegersAsProperties_CreatesDecimalColumn()
    {
        var obj1 = new PSObject();
        obj1.Properties.Add(new PSNoteProperty("Id", 42));
        var obj2 = new PSObject();
        obj2.Properties.Add(new PSNoteProperty("Id", 99));

        var result = TypeGetter.CastObjectsToTableView(new List<PSObject> { obj1, obj2 });

        Assert.Single(result.DataColumns);
        Assert.Equal("Id", result.DataColumns[0].Label);
        Assert.Equal(2, result.Data.Count);
        // All values numeric, so column type should be decimal
        Assert.Equal(typeof(decimal).FullName, result.DataColumns[0].StringType);
    }

    [Fact]
    public void CastObjectsToTableView_ObjectWithProperties_CreatesColumns()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Name", "Test"));
        obj.Properties.Add(new PSNoteProperty("Value", 42));

        var result = TypeGetter.CastObjectsToTableView(new List<PSObject> { obj });

        Assert.Equal(2, result.DataColumns.Count);
        Assert.Contains(result.DataColumns, c => c.Label == "Name");
        Assert.Contains(result.DataColumns, c => c.Label == "Value");
        Assert.Single(result.Data);
    }

    [Fact]
    public void CastObjectToDataTableRow_SimpleProperty_ExtractsValue()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Name", "TestValue"));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Name", "$_.\"Name\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        Assert.Equal(0, row.OriginalObjectIndex);
        var columnKey = columns[0].ToString();
        Assert.True(row.Values.ContainsKey(columnKey));
        Assert.Equal("TestValue", row.Values[columnKey].DisplayValue);
    }

    [Fact]
    public void CastObjectToDataTableRow_NumericProperty_CreatesDecimalValue()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Count", 42));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Count", "$_.\"Count\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.IsType<DecimalValue>(row.Values[columnKey]);
        Assert.Equal(42m, ((DecimalValue)row.Values[columnKey]).SortValue);
    }

    [Fact]
    public void CastObjectToDataTableRow_StringProperty_CreatesStringValue()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Name", "hello"));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Name", "$_.\"Name\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.IsType<StringValue>(row.Values[columnKey]);
    }

    [Fact]
    public void CastObjectToDataTableRow_NullProperty_ReturnsEmptyDisplayValue()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Name", null));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Name", "$_.\"Name\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.Equal(string.Empty, row.Values[columnKey].DisplayValue);
    }

    [Fact]
    public void CastObjectToDataTableRow_MissingProperty_ReturnsEmptyDisplayValue()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Other", "value"));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Missing", "$_.\"Missing\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.Equal(string.Empty, row.Values[columnKey].DisplayValue);
    }

    [Fact]
    public void CastObjectToDataTableRow_PrimitiveAccessor_ExtractsWholeObject()
    {
        var obj = new PSObject("hello world");

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("String", "$_")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.Equal("hello world", row.Values[columnKey].DisplayValue);
    }

    [Fact]
    public void CastObjectToDataTableRow_AnsiCodes_AreStripped()
    {
        var obj = new PSObject();
        obj.Properties.Add(new PSNoteProperty("Name", "\x1b[31mRed\x1b[0m"));

        var columns = new List<DataTableColumn>
        {
            new DataTableColumn("Name", "$_.\"Name\"")
        };

        var row = TypeGetter.CastObjectToDataTableRow(obj, columns, 0);

        var columnKey = columns[0].ToString();
        Assert.Equal("Red", row.Values[columnKey].DisplayValue);
    }

    [Fact]
    public void CastObjectsToTableView_SetsColumnTypes()
    {
        var obj1 = new PSObject();
        obj1.Properties.Add(new PSNoteProperty("Count", 10));
        obj1.Properties.Add(new PSNoteProperty("Name", "test"));

        var obj2 = new PSObject();
        obj2.Properties.Add(new PSNoteProperty("Count", 20));
        obj2.Properties.Add(new PSNoteProperty("Name", "test2"));

        var result = TypeGetter.CastObjectsToTableView(new List<PSObject> { obj1, obj2 });

        var countColumn = result.DataColumns.Find(c => c.Label == "Count");
        var nameColumn = result.DataColumns.Find(c => c.Label == "Name");

        Assert.NotNull(countColumn);
        Assert.NotNull(nameColumn);
        // Count column should be decimal type (all values are numeric)
        Assert.Equal(typeof(decimal).FullName, countColumn.StringType);
        // Name column should be string type
        Assert.Equal(typeof(string).FullName, nameColumn.StringType);
    }

    [Fact]
    public void CastObjectsToTableView_MultipleObjects_PreservesOrder()
    {
        var objects = new List<PSObject>();
        for (int i = 0; i < 5; i++)
        {
            var obj = new PSObject();
            obj.Properties.Add(new PSNoteProperty("Index", i));
            objects.Add(obj);
        }

        var result = TypeGetter.CastObjectsToTableView(objects);

        Assert.Equal(5, result.Data.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i, result.Data[i].OriginalObjectIndex);
        }
    }
}
