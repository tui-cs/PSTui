// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.Commands;
using Microsoft.PowerShell.OutGridView.Models;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides methods to retrieve type information and convert PowerShell objects into data table structures for display
///     in the grid view.
/// </summary>
public class TypeGetter
{
    private readonly Dictionary<string, FormatViewDefinition?> _formatCache = new();

    /// <summary>
    ///     Gets the format view definition for the specified type name, using a cache to avoid redundant lookups.
    /// </summary>
    /// <param name="typeName">The full type name to get the format view definition for.</param>
    /// <returns>The format view definition if found; otherwise, <see langword="null" />.</returns>
    private FormatViewDefinition? GetFormatViewDefinitionForType(string typeName)
    {
        if (_formatCache.TryGetValue(typeName, out var cached)) return cached;

        // Always create a new runspace to avoid pipeline concurrency issues
        // when called from within an active PowerShell cmdlet/pipeline
        using var runspace = RunspaceFactory.CreateRunspace();
        runspace.Open();

        try
        {
            using var ps = System.Management.Automation.PowerShell.Create();
            ps.Runspace = runspace;
            ps.AddCommand("Get-FormatData").AddParameter("TypeName", typeName);

            var results = ps.Invoke();

            if (results.Count == 0)
            {
                ps.Commands.Clear();
                ps.AddCommand("Get-FormatData")
                    .AddParameter("TypeName", typeName);
                results = ps.Invoke();
            }

            FormatViewDefinition? result = null;
            if (results.Count > 0)
            {
                var extendedTypeDefinition = results[0].BaseObject as ExtendedTypeDefinition;
                result = extendedTypeDefinition?.FormatViewDefinition[0];
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
    ///     Gets the format view definition for the specified PowerShell object using the current runspace.
    /// </summary>
    /// <param name="obj">The PowerShell object to get the format view definition for.</param>
    /// <returns>The format view definition if found; otherwise, <see langword="null" />.</returns>
    private FormatViewDefinition? GetFormatViewDefinitionForObject(PSObject obj)
    {
        string? typeName = obj.BaseObject.GetType().FullName;
        if (typeName is null)
        {
            return null;
        }
        if (_formatCache.TryGetValue(typeName, out var cached))
            return cached;
        return GetFormatViewDefinitionForType(typeName);
    }

    /// <summary>
    ///     Converts a PowerShell object to a data table row with values extracted based on the specified columns.
    /// </summary>
    /// <param name="ps">The PowerShell object to convert.</param>
    /// <param name="dataColumns">The list of columns defining which properties to extract.</param>
    /// <param name="objectIndex">The original index of the object in the source collection.</param>
    /// <returns>A <see cref="DataTableRow" /> containing the extracted values.</returns>
    public static DataTableRow CastObjectToDataTableRow(PSObject ps, List<DataTableColumn> dataColumns, int objectIndex)
    {
        var valuePairs = new Dictionary<string, IValue>();

        foreach (var dataColumn in dataColumns)
        {
            var expression = new PSPropertyExpression(ScriptBlock.Create(dataColumn.PropertyScriptAccessor));

            var result = expression.GetValues(ps).FirstOrDefault()?.Result;

            var stringValue = result?.ToString() ?? string.Empty;

            var isDecimal = decimal.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat,
                out var decimalValue);

            if (isDecimal)
            {
                valuePairs[dataColumn.ToString()] = new DecimalValue
                { DisplayValue = stringValue, SortValue = decimalValue };
            }
            else
            {
                var stringDecorated = new StringDecorated(stringValue);
                valuePairs[dataColumn.ToString()] = new StringValue
                { DisplayValue = stringDecorated.ToString(OutputRendering.PlainText) };
            }
        }

        return new DataTableRow(valuePairs, objectIndex);
    }

    /// <summary>
    ///     Sets the data type on each column based on the values in the data rows.
    ///     If all values in a column can be parsed as decimal, the column type is set to decimal; otherwise, it's set to
    ///     string.
    /// </summary>
    /// <param name="dataTableRows">The list of data table rows to analyze.</param>
    /// <param name="dataTableColumns">The list of data table columns to update with type information.</param>
    private static void SetTypesOnDataColumns(List<DataTableRow> dataTableRows, List<DataTableColumn> dataTableColumns)
    {
        var dataRows = dataTableRows.Select(x => x.Values);

        foreach (var dataColumn in dataTableColumns) dataColumn.StringType = typeof(decimal).FullName;

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
    ///     Retrieves the column definitions for the specified PowerShell objects based on their format view definitions or
    ///     properties.
    /// </summary>
    /// <param name="psObjects">The list of PowerShell objects to analyze.</param>
    /// <returns>A distinct list of data table columns.</returns>
    private List<DataTableColumn> GetDataColumnsForObject(List<PSObject> psObjects)
    {
        var dataColumns = new List<DataTableColumn>();

        foreach (var obj in psObjects)
        {
            List<string> labels;

            var fvd = GetFormatViewDefinitionForObject(obj);

            List<string> propertyAccessors;

            if (fvd == null)
            {
                if (PSObjectIsPrimitive(obj))
                {
                    labels = [obj.BaseObject.GetType().Name];
                    propertyAccessors = ["$_"];
                }
                else
                {
                    labels = obj.Properties.Select(x => x.Name).ToList();
                    propertyAccessors = obj.Properties.Select(x => $"$_.\"{x.Name}\"").ToList();
                }
            }
            else
            {
                var tableControl = fvd.Control as TableControl;

                var definedColumnLabels = tableControl?.Headers.Select(x => x.Label);

                var displayEntries = tableControl?.Rows[0].Columns.Select(x => x.DisplayEntry);

                var enumerable = displayEntries as DisplayEntry[] ?? displayEntries!.ToArray();
                var propertyLabels = enumerable.Select(x => x.Value);

                // Use the TypeDefinition Label if available otherwise just use the property name as a label
                labels = (definedColumnLabels ?? []).Zip(propertyLabels, (definedColumnLabel, propertyLabel) =>
                {
                    if (string.IsNullOrEmpty(definedColumnLabel)) return propertyLabel;

                    return definedColumnLabel;
                }).ToList();

                propertyAccessors = enumerable.Select(x => x.ValueType == DisplayEntryValueType.Property
                    ? $"$_.\"{x.Value}\""
                    :
                    // Otherwise return access script
                    x.Value).ToList();
            }

            dataColumns.AddRange(labels.Select((t, i) => new DataTableColumn(t, propertyAccessors[i])));
        }

        return dataColumns.Distinct().ToList();
    }

    /// <summary>
    ///     Converts a list of PowerShell objects into a data table structure suitable for display in the grid view.
    /// </summary>
    /// <param name="psObjects">The list of PowerShell objects to convert.</param>
    /// <returns>A <see cref="DataTable" /> containing the columns and rows extracted from the PowerShell objects.</returns>
    public static DataTable CastObjectsToTableView(List<PSObject> psObjects)
    {
        var typeGetter = new TypeGetter();
        var dataTableColumns = typeGetter.GetDataColumnsForObject(psObjects);

        var dataTableRows = new List<DataTableRow>();
        for (var i = 0; i < psObjects.Count; i++)
        {
            var dataTableRow = CastObjectToDataTableRow(psObjects[i], dataTableColumns, i);
            dataTableRows.Add(dataTableRow);
        }

        SetTypesOnDataColumns(dataTableRows, dataTableColumns);

        return new DataTable(dataTableColumns, dataTableRows);
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
    ///     Determines whether the specified PowerShell object represents a primitive type in the PowerShell context.
    /// </summary>
    /// <param name="ps">The PowerShell object to check.</param>
    /// <returns><see langword="true" /> if the object is a primitive type; otherwise, <see langword="false" />.</returns>
    private static bool PSObjectIsPrimitive(PSObject ps)
    {
        var psBaseType = ps.BaseObject.GetType();

        return psBaseType.IsPrimitive || psBaseType.IsEnum || ADDITIONAL_PRIMITIVE_TYPES.Contains(psBaseType.FullName!);
    }
}