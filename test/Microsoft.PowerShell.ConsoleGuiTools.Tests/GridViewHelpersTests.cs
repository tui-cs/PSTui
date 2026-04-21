// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

public class GridViewHelpersTests
{
    #region FilterData

    [Fact]
    public void FilterData_EmptyFilter_ReturnsOriginalList()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "apple", OriginalIndex = 0 },
            new() { DisplayString = "banana", OriginalIndex = 1 }
        };

        var result = GridViewHelpers.FilterData(rows, "");
        Assert.Same(rows, result);
    }

    [Fact]
    public void FilterData_NullFilter_ReturnsOriginalList()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "apple", OriginalIndex = 0 }
        };

        var result = GridViewHelpers.FilterData(rows, null!);
        Assert.Same(rows, result);
    }

    [Fact]
    public void FilterData_MatchingFilter_ReturnsMatchingRows()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "apple pie", OriginalIndex = 0 },
            new() { DisplayString = "banana split", OriginalIndex = 1 },
            new() { DisplayString = "apple sauce", OriginalIndex = 2 }
        };

        var result = GridViewHelpers.FilterData(rows, "apple");
        Assert.Equal(2, result.Count);
        Assert.Equal("apple pie", result[0].DisplayString);
        Assert.Equal("apple sauce", result[1].DisplayString);
    }

    [Fact]
    public void FilterData_CaseInsensitive()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "APPLE", OriginalIndex = 0 },
            new() { DisplayString = "banana", OriginalIndex = 1 }
        };

        var result = GridViewHelpers.FilterData(rows, "apple");
        Assert.Single(result);
        Assert.Equal("APPLE", result[0].DisplayString);
    }

    [Fact]
    public void FilterData_MarkedItemsAlwaysIncluded()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "apple", OriginalIndex = 0, IsMarked = true },
            new() { DisplayString = "banana", OriginalIndex = 1, IsMarked = false },
            new() { DisplayString = "cherry", OriginalIndex = 2, IsMarked = true }
        };

        // Filter for "banana" but marked items should still appear first
        var result = GridViewHelpers.FilterData(rows, "banana");
        Assert.Equal(3, result.Count);
        Assert.Equal("apple", result[0].DisplayString);   // marked, first
        Assert.Equal("cherry", result[1].DisplayString);   // marked, first
        Assert.Equal("banana", result[2].DisplayString);   // matched filter
    }

    [Fact]
    public void FilterData_NoMatch_ReturnsOnlyMarkedItems()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "apple", OriginalIndex = 0, IsMarked = true },
            new() { DisplayString = "banana", OriginalIndex = 1, IsMarked = false }
        };

        var result = GridViewHelpers.FilterData(rows, "zzz_no_match");
        Assert.Single(result);
        Assert.Equal("apple", result[0].DisplayString);
    }

    [Fact]
    public void FilterData_RegexPattern()
    {
        var rows = new List<GridViewRow>
        {
            new() { DisplayString = "item123", OriginalIndex = 0 },
            new() { DisplayString = "item456", OriginalIndex = 1 },
            new() { DisplayString = "other", OriginalIndex = 2 }
        };

        var result = GridViewHelpers.FilterData(rows, @"item\d+");
        Assert.Equal(2, result.Count);
    }

    #endregion

    #region GetPaddedString

    [Fact]
    public void GetPaddedString_NullColumnWidths_ReturnsEmptyString()
    {
        var result = GridViewHelpers.GetPaddedString(new List<string> { "test" }, 0, null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetPaddedString_NullStrings_ReturnsOffset()
    {
        var result = GridViewHelpers.GetPaddedString(null, 3, new[] { 10 });
        Assert.Equal("   ", result);
    }

    [Fact]
    public void GetPaddedString_StringFitsInColumn_PadsRight()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "hi" }, 0, new[] { 10 });
        Assert.Equal("hi        ", result);
    }

    [Fact]
    public void GetPaddedString_StringExceedsColumn_Truncated_WithEllipsis()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "Hello World!" }, 0, new[] { 8 });
        Assert.Equal("Hello...", result);
    }

    [Fact]
    public void GetPaddedString_VerySmallColumn_TruncatesWithoutEllipsis()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "Hello" }, 0, new[] { 3 });
        Assert.Equal("Hel", result);
    }

    [Fact]
    public void GetPaddedString_ZeroWidthColumn_SkipsContent()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "Hello" }, 0, new[] { 0 });
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetPaddedString_MultipleColumns_SeparatedBySpace()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "A", "B" }, 0, new[] { 5, 5 });
        Assert.Equal("A     B    ", result);
    }

    [Fact]
    public void GetPaddedString_WithOffset_PadsLeft()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "Hi" }, 4, new[] { 5 });
        Assert.Equal("    Hi   ", result);
    }

    [Fact]
    public void GetPaddedString_NewlinesEncoded()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "line1\nline2" }, 0, new[] { 20 });
        Assert.Contains("`n", result);
        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void GetPaddedString_CarriageReturnsEncoded()
    {
        var result = GridViewHelpers.GetPaddedString(
            new List<string> { "line1\rline2" }, 0, new[] { 20 });
        Assert.Contains("`r", result);
        Assert.DoesNotContain("\r", result);
    }

    #endregion
}
