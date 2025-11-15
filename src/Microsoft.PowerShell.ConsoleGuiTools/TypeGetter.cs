// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.Commands;
using Microsoft.PowerShell.OutGridView.Models;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides methods to retrieve type information and convert PowerShell objects into data table structures for display
///     in the grid view using PowerShell's native formatting infrastructure.
/// </summary>
public class TypeGetter
{
    private readonly Dictionary<string, FormatViewDefinition?> _formatCache = new();

    /// <summary>
    ///     Regex pattern to match ANSI escape sequences.
    /// </summary>
    private static readonly Regex AnsiEscapeRegex = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

    /// <summary>
    ///     Strips ANSI escape sequences from a string.
    /// </summary>
    /// <param name="value">The string potentially containing ANSI codes.</param>
    /// <returns>The string with ANSI codes removed.</returns>
    private static string StripAnsiCodes(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return AnsiEscapeRegex.Replace(value, string.Empty);
    }

    /// <summary>
    ///     Gets the format view definition for the specified type name, using a cache to avoid redundant lookups.
    /// </summary>
    /// <param name="typeName">The full type name to get the format view definition for.</param>
    /// <returns>The format view definition if found; otherwise, <see langword="null" />.</returns>
    private FormatViewDefinition? GetFormatViewDefinitionForType(string typeName)
    {
        if (_formatCache.TryGetValue(typeName, out var cached)) return cached;

        // Create a runspace with the default initial session state to load format data
        var iss = InitialSessionState.CreateDefault();
        using var runspace = RunspaceFactory.CreateRunspace(iss);
        runspace.Open();

        try
        {
            using var ps = System.Management.Automation.PowerShell.Create();
            ps.Runspace = runspace;
            ps.AddCommand("Get-FormatData").AddParameter("TypeName", typeName);

            var results = ps.Invoke();

            FormatViewDefinition? result = null;
            if (results.Count > 0)
            {
                var extendedTypeDefinition = results[0].BaseObject as ExtendedTypeDefinition;
                result = extendedTypeDefinition?.FormatViewDefinition.FirstOrDefault(v => v.Control is TableControl);
            }

            _formatCache[typeName] = result;
            return result;
        }
        finally
        {
            runspace.Close();
        }
    }

    /// <summary>
    ///     Gets the format view definition for the specified PowerShell object.
    /// </summary>
    /// <param name="obj">The PowerShell object to get the format view definition for.</param>
    /// <returns>The format view definition if found; otherwise, <see langword="null" />.</returns>
    private FormatViewDefinition? GetFormatViewDefinitionForObject(PSObject obj)
    {
        string? typeName = obj.BaseObject.GetType().FullName;
        if (typeName is null) return null;

        if (_formatCache.TryGetValue(typeName, out var cached))
            return cached;

        return GetFormatViewDefinitionForType(typeName);
    }

    /// <summary>
    ///     Retrieves the column definitions for the specified PowerShell objects based on their format view definitions or properties.
    /// </summary>
    /// <param name="psObjects">The list of PowerShell objects to analyze.</param>
    /// <returns>A distinct list of data table columns.</returns>
    private List<DataTableColumn> GetDataColumnsForObject(List<PSObject> psObjects)
    {
        var dataColumns = new List<DataTableColumn>();

        if (psObjects.Count == 0) return dataColumns;

        var firstObject = psObjects[0];
        var fvd = GetFormatViewDefinitionForObject(firstObject);

        List<string> labels;
        List<string> propertyAccessors;

        if (fvd?.Control is TableControl tableControl)
        {
            // Use the table format definition
            var definedColumnLabels = tableControl.Headers.Select(h => h.Label).ToList();
            var displayEntries = tableControl.Rows[0].Columns.Select(c => c.DisplayEntry).ToArray();
            var propertyLabels = displayEntries.Select(de => de.Value).ToList();

            // Use the TypeDefinition Label if available otherwise just use the property name as a label
            labels = definedColumnLabels.Zip(propertyLabels, (definedLabel, propLabel) =>
            {
                if (string.IsNullOrEmpty(definedLabel)) return propLabel;
                return definedLabel;
            }).ToList();

            propertyAccessors = displayEntries.Select(de =>
                de.ValueType == DisplayEntryValueType.Property
                    ? $"$_.\"{de.Value}\""
                    : de.Value  // ScriptBlock
            ).ToList();
        }
        else if (PSObjectIsPrimitive(firstObject))
        {
            // Handle primitive types
            labels = [firstObject.BaseObject.GetType().Name];
            propertyAccessors = ["$_"];
        }
        else
        {
            // Fallback to properties
            labels = firstObject.Properties.Select(p => p.Name).ToList();
            propertyAccessors = firstObject.Properties.Select(p => $"$_.\"{p.Name}\"").ToList();
        }

        for (int i = 0; i < labels.Count; i++)
        {
            var column = new DataTableColumn(labels[i], propertyAccessors[i]);
            dataColumns.Add(column);
        }

        return dataColumns.Distinct().ToList();
    }

    /// <summary>
    ///     Types that are considered primitives to PowerShell but not to C#.
    /// </summary>
    private static readonly List<string> ADDITIONAL_PRIMITIVE_TYPES =
    [
        "System.String",
        "System.Decimal",
        "System.IntPtr",
        "System.Security.SecureString",
        "System.Numerics.BigInteger"
    ];

    /// <summary>
    ///     Determines whether the specified PowerShell object represents a primitive type.
    /// </summary>
    private static bool PSObjectIsPrimitive(PSObject ps)
    {
        var psBaseType = ps.BaseObject.GetType();
        return psBaseType.IsPrimitive || psBaseType.IsEnum ||
               ADDITIONAL_PRIMITIVE_TYPES.Contains(psBaseType.FullName!);
    }

    /// <summary>
    ///     Converts a PowerShell object to a data table row using PSPropertyExpression to evaluate property accessors.
    /// </summary>
    public static DataTableRow CastObjectToDataTableRow(PSObject psObject, List<DataTableColumn> dataTableColumns, int objectIndex)
    {
        var valuePairs = new Dictionary<string, IValue>();

        foreach (var column in dataTableColumns)
        {
            object? result = null;

            try
            {
                // PSPropertyExpression constructor takes a ScriptBlock for script expressions
                // For simple properties, we need to extract just the property name
                var accessor = column.PropertyScriptAccessor;

                if (accessor.StartsWith("$_.\"") && accessor.EndsWith("\""))
                {
                    // Extract property name from "$_."PropertyName"" format
                    var propertyName = accessor.Substring(4, accessor.Length - 5);
                    var property = psObject.Properties[propertyName];
                    result = property?.Value;

                    // Unwrap PSObject if needed to get the base value
                    if (result is PSObject psObjResult)
                    {
                        result = psObjResult.BaseObject;
                    }
                }
                else if (accessor == "$_")
                {
                    // The whole object
                    result = psObject.BaseObject;
                }
                else
                {
                    // It's a script block - create and invoke it
                    var scriptBlock = ScriptBlock.Create(accessor);
                    var results = scriptBlock.InvokeWithContext(null, new List<PSVariable>
                    {
                        new PSVariable("_", psObject)
                    });
                    result = results.FirstOrDefault();

                    // Unwrap PSObject if needed
                    if (result is PSObject psScriptResult)
                    {
                        result = psScriptResult.BaseObject;
                    }
                }
            }
            catch (Exception ex)
            {
                // If evaluation fails, use null
                result = null;
            }

            // Convert to string and strip ANSI codes
            var displayValue = result?.ToString() ?? string.Empty;
            displayValue = StripAnsiCodes(displayValue);

            // Determine if this is a numeric value for sorting
            var isNumeric = result is decimal or int or long or short or byte or double or float or uint or ulong or ushort or sbyte;

            var columnKey = column.ToString();

            if (isNumeric)
            {
                var decimalValue = Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                valuePairs[columnKey] = new DecimalValue
                {
                    DisplayValue = displayValue,
                    SortValue = decimalValue
                };
            }
            else
            {
                valuePairs[columnKey] = new StringValue
                {
                    DisplayValue = displayValue,
                    RawValue = result
                };
            }
        }

        return new DataTableRow(valuePairs, objectIndex);
    }

    /// <summary>
    ///     Sets the data type on each column based on the values in the data rows.
    /// </summary>
    private static void SetTypesOnDataColumns(List<DataTableRow> dataTableRows, List<DataTableColumn> dataTableColumns)
    {
        var dataRows = dataTableRows.Select(x => x.Values);

        foreach (var dataColumn in dataTableColumns)
            dataColumn.StringType = typeof(decimal).FullName;

        // If every value in a column could be a decimal, assume that it is supposed to be a decimal
        foreach (var dataRow in dataRows)
        {
            foreach (var dataColumn in dataTableColumns)
            {
                if (dataRow[dataColumn.ToString()] is not DecimalValue)
                    dataColumn.StringType = typeof(string).FullName;
            }
        }
    }

    /// <summary>
    ///     Converts a list of PowerShell objects into a data table structure suitable for display in the grid view.
    /// </summary>
    public static DataTable CastObjectsToTableView(List<PSObject> psObjects)
    {
        if (psObjects.Count == 0)
        {
            return new DataTable([], []);
        }

        // Get the columns using format view definitions
        var typeGetter = new TypeGetter();
        var dataTableColumns = typeGetter.GetDataColumnsForObject(psObjects);

        // Convert each object to a row
        var dataTableRows = new List<DataTableRow>();
        for (var i = 0; i < psObjects.Count; i++)
        {
            var dataTableRow = CastObjectToDataTableRow(psObjects[i], dataTableColumns, i);
            dataTableRows.Add(dataTableRow);
        }

        SetTypesOnDataColumns(dataTableRows, dataTableColumns);

        return new DataTable(dataTableColumns, dataTableRows);
    }
}