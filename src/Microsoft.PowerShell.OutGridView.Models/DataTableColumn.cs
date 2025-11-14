// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;
using Newtonsoft.Json;

// TODO: switch to System.Text.Json

namespace Microsoft.PowerShell.OutGridView.Models;

/// <summary>
///     Represents a column in a data table with metadata about its label, type, and property accessor.
/// </summary>
/// <param name="label">The display label for the column.</param>
/// <param name="propertyScriptAccessor">The script accessor used to retrieve the property value.</param>
public class DataTableColumn(string label, string propertyScriptAccessor)
{
    /// <summary>
    ///     Gets the runtime type of the column based on the <see cref="StringType" /> property.
    /// </summary>
    [JsonIgnore]
    public Type? Type => System.Type.GetType(StringType!);

    /// <summary>
    ///     Gets the display label for the column.
    /// </summary>
    public string Label { get; } = label;

    /// <summary>
    ///     Gets or sets the serializable string representation of the column's type.
    /// </summary>
    public string? StringType { get; set; }

    /// <summary>
    ///     Gets or sets the format string used to format values in this column.
    ///     This follows PowerShell/. NET composite formatting conventions (e.g., "N0" for numbers, "G" for DateTime).
    /// </summary>
    public string? FormatString { get; set; }

    /// <summary>
    ///     Gets the script accessor used to retrieve the property value for this column.
    /// </summary>
    public string PropertyScriptAccessor { get; } = propertyScriptAccessor;

    /// <summary>
    ///     Formats a value according to this column's format specification.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>A formatted string representation of the value.</returns>
    public string FormatValue(object? value)
    {
        if (value == null) return string.Empty;

        // If we have a format string, try to use it
        if (!string.IsNullOrEmpty(FormatString) && value is IFormattable formattable)
        {
            try
            {
                return formattable.ToString(FormatString, System.Globalization.CultureInfo.CurrentCulture);
            }
            catch
            {
                // Fall through to default formatting if format string is invalid
            }
        }

        // If FormatString is explicitly null, use simple ToString for most types
        // (this prevents unwanted formatting of identifier integers like ProcessId)
        if (FormatString == null)
        {
            return value.ToString() ?? string.Empty;
        }

        // Default formatting based on type (only when FormatString is empty but not null)
        return value switch
        {
            DateTime dt => dt.ToString("G", System.Globalization.CultureInfo.CurrentCulture),
            decimal d => d.ToString("N0", System.Globalization.CultureInfo.CurrentCulture),
            double db => db.ToString("N2", System.Globalization.CultureInfo.CurrentCulture),
            float f => f.ToString("N2", System.Globalization.CultureInfo.CurrentCulture),
            int or long or short or byte => string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:N0}", value),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    ///     Determines whether the specified object is equal to the current column.
    ///     Two columns are considered equal if they have the same label and property script accessor.
    /// </summary>
    /// <param name="obj">The object to compare with the current column.</param>
    /// <returns>
    ///     <see langword="true" /> if the specified object is equal to the current column; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    public override bool Equals(object? obj)
    {
        var b = obj as DataTableColumn;
        return b?.Label == Label && b.PropertyScriptAccessor == PropertyScriptAccessor;
    }

    /// <summary>
    ///     Returns the hash code for this column based on its label and property script accessor.
    /// </summary>
    /// <returns>A hash code for the current column.</returns>
    public override int GetHashCode() => Label.GetHashCode() + PropertyScriptAccessor.GetHashCode();

    /// <summary>
    ///     Returns a Base64-encoded string representation of the column for safe embedding in XAML.
    /// </summary>
    /// <returns>A Base64-encoded string containing the label and property script accessor.</returns>
    public override string ToString() =>
        // Needs to be encoded to embed safely in XAML
        Convert.ToBase64String(Encoding.UTF8.GetBytes(Label + PropertyScriptAccessor));
}