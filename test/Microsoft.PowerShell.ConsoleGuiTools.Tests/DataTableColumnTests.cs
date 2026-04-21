// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using Microsoft.PowerShell.OutGridView.Models;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

public class DataTableColumnTests
{
    [Fact]
    public void FormatValue_NullValue_ReturnsEmptyString()
    {
        var column = new DataTableColumn("Test", "$_.Test");
        Assert.Equal(string.Empty, column.FormatValue(null));
    }

    [Fact]
    public void FormatValue_WithFormatString_UsesFormatString()
    {
        var column = new DataTableColumn("Test", "$_.Test") { FormatString = "N2" };
        var result = column.FormatValue(123.456m);
        // Should format as decimal with 2 decimal places
        Assert.Contains("123", result);
    }

    [Fact]
    public void FormatValue_NullFormatString_UsesToString()
    {
        // When FormatString is explicitly null, use plain ToString (no default formatting)
        var column = new DataTableColumn("Id", "$_.Id") { FormatString = null };
        var result = column.FormatValue(1234);
        // Should NOT have thousands separator — plain ToString
        Assert.Equal("1234", result);
    }

    [Fact]
    public void FormatValue_EmptyFormatString_UsesDefaultTypeFormatting()
    {
        // When FormatString is empty string (not null), use type-based defaults
        var column = new DataTableColumn("Size", "$_.Size") { FormatString = "" };
        var result = column.FormatValue(1234);
        // int with empty FormatString should use N0 formatting (thousands separator)
        var expected = 1234.ToString("N0", CultureInfo.CurrentCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatValue_DateTime_WithEmptyFormatString_UsesGeneralFormat()
    {
        var column = new DataTableColumn("Date", "$_.Date") { FormatString = "" };
        var dt = new DateTime(2024, 6, 15, 14, 30, 0);
        var result = column.FormatValue(dt);
        var expected = dt.ToString("G", CultureInfo.CurrentCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatValue_Decimal_WithEmptyFormatString_UsesN0()
    {
        var column = new DataTableColumn("Amount", "$_.Amount") { FormatString = "" };
        var result = column.FormatValue(1234567.89m);
        var expected = 1234567.89m.ToString("N0", CultureInfo.CurrentCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatValue_Double_WithEmptyFormatString_UsesN2()
    {
        var column = new DataTableColumn("Value", "$_.Value") { FormatString = "" };
        var result = column.FormatValue(3.14159);
        var expected = 3.14159.ToString("N2", CultureInfo.CurrentCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatValue_String_ReturnsStringValue()
    {
        var column = new DataTableColumn("Name", "$_.Name");
        Assert.Equal("hello", column.FormatValue("hello"));
    }

    [Fact]
    public void FormatValue_InvalidFormatString_FallsThrough()
    {
        var column = new DataTableColumn("Test", "$_.Test") { FormatString = "ZZZZ_INVALID" };
        // An invalid format string should not throw — it should fall through
        var result = column.FormatValue(42);
        Assert.NotNull(result);
    }

    [Fact]
    public void Equals_SameLabelAndAccessor_ReturnsTrue()
    {
        var a = new DataTableColumn("Name", "$_.Name");
        var b = new DataTableColumn("Name", "$_.Name");
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentLabel_ReturnsFalse()
    {
        var a = new DataTableColumn("Name", "$_.Name");
        var b = new DataTableColumn("Id", "$_.Name");
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentAccessor_ReturnsFalse()
    {
        var a = new DataTableColumn("Name", "$_.Name");
        var b = new DataTableColumn("Name", "$_.Id");
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var a = new DataTableColumn("Name", "$_.Name");
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualColumns_SameHashCode()
    {
        var a = new DataTableColumn("Name", "$_.Name");
        var b = new DataTableColumn("Name", "$_.Name");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsBase64EncodedString()
    {
        var column = new DataTableColumn("Name", "$_.Name");
        var result = column.ToString();
        // Should decode to "Name" + "$_.Name"
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result));
        Assert.Equal("Name$_.Name", decoded);
    }

    [Fact]
    public void Type_ValidStringType_ReturnsCorrectType()
    {
        var column = new DataTableColumn("Test", "$_.Test")
        {
            StringType = typeof(decimal).FullName
        };
        Assert.Equal(typeof(decimal), column.Type);
    }

    [Fact]
    public void Type_NullStringType_ThrowsOnAccess()
    {
        var column = new DataTableColumn("Test", "$_.Test");
        // StringType is null by default; Type.GetType(null) throws ArgumentNullException
        Assert.Throws<ArgumentNullException>(() => column.Type);
    }
}
