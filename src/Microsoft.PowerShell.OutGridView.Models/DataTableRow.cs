// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerShell.OutGridView.Models;

/// <summary>
///     Represents a value that can be displayed and compared in a data table.
/// </summary>
public interface IValue : IComparable
{
    /// <summary>
    ///     Gets or sets the string representation of the value for display purposes.
    /// </summary>
    string DisplayValue { get; set; }

    /// <summary>
    ///     Gets the original object value before formatting.
    /// </summary>
    object? OriginalValue { get; }
}

/// <summary>
///     Represents a decimal value in a data table with support for numeric sorting.
/// </summary>
public class DecimalValue : IValue
{
    /// <summary>
    ///     Gets or sets the string representation of the decimal value for display purposes.
    /// </summary>
    public required string DisplayValue { get; set; }

    /// <summary>
    ///     Gets or sets the decimal value used for sorting.
    /// </summary>
    public decimal SortValue { get; set; }

    /// <summary>
    ///     Gets the original decimal value.
    /// </summary>
    public object? OriginalValue => SortValue;

    /// <summary>
    ///     Compares the current instance with another object of the same type.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    ///     A value that indicates the relative order of the objects being compared.
    ///     Less than zero if this instance precedes <paramref name="obj" />,
    ///     zero if they are equal, or greater than zero if this instance follows <paramref name="obj" />.
    ///     Returns 1 if <paramref name="obj" /> is not a <see cref="DecimalValue" />.
    /// </returns>
    public int CompareTo(object? obj) => obj is not DecimalValue otherDecimalValue
        ? 1
        : decimal.Compare(SortValue, otherDecimalValue.SortValue);
}

/// <summary>
///     Represents a string value in a data table with support for string sorting.
/// </summary>
public class StringValue : IValue
{
    /// <summary>
    ///     Gets or sets the string value for display and sorting purposes.
    /// </summary>
    public required string DisplayValue { get; set; }

    /// <summary>
    ///     Gets or sets the original object value before conversion to string.
    /// </summary>
    public object? RawValue { get; set; }

    /// <summary>
    ///     Gets the original object value.
    /// </summary>
    public object? OriginalValue => RawValue ?? DisplayValue;

    /// <summary>
    ///     Compares the current instance with another object of the same type.
    /// </summary>
    /// <param name="obj">An object to compare with this instance.</param>
    /// <returns>
    ///     A value that indicates the relative order of the objects being compared.
    ///     Less than zero if this instance precedes <paramref name="obj" />,
    ///     zero if they are equal, or greater than zero if this instance follows <paramref name="obj" />.
    ///     Returns 1 if <paramref name="obj" /> is not a <see cref="StringValue" />.
    /// </returns>
    public int CompareTo(object? obj) => obj is not StringValue otherStringValue
        ? 1
        : string.Compare(DisplayValue, otherStringValue.DisplayValue, StringComparison.Ordinal);
}

/// <summary>
///     Represents a single row in a data table with values mapped to column identifiers.
/// </summary>
public class DataTableRow
{
    /// <summary>
    ///     Gets or sets the dictionary of values for this row, keyed by the column identifier.
    ///     The key is the data column hash code serialized as a string for JSON compatibility.
    /// </summary>
    public Dictionary<string, IValue> Values { get; set; }

    /// <summary>
    ///     Gets or sets the original index of the object in the source collection before any transformations.
    /// </summary>
    public int OriginalObjectIndex { get; set; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DataTableRow" /> class with the specified values and original index.
    /// </summary>
    /// <param name="data">The dictionary of values for this row, keyed by column identifier.</param>
    /// <param name="originalObjectIndex">The original index of the object in the source collection.</param>
    public DataTableRow(Dictionary<string, IValue> data, int originalObjectIndex)
    {
        Values = data;
        OriginalObjectIndex = originalObjectIndex;
    }
}