// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using Microsoft.PowerShell.OutGridView.Models;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides methods to retrieve type information and convert PowerShell objects into data table structures for display
///     in the grid view using PowerShell's native formatting infrastructure.
/// </summary>
public class TypeGetter
{
    /// <summary>
    ///     Gets the properties to display for the PowerShell objects using PowerShell's formatting system.
    /// </summary>
    /// <param name="psObjects">The list of PowerShell objects to get format data for.</param>
    /// <returns>A list of property information representing the properties to display.</returns>
    public static List<PSPropertyInfo> GetFormatDataForObjects(List<PSObject> psObjects)
    {
        if (psObjects.Count == 0)
        {
            return [];
        }

        var firstObject = psObjects[0];

        // Try to get the DefaultDisplayPropertySet from PSStandardMembers
        var standardMembers = firstObject.Members["PSStandardMembers"]?.Value as PSMemberSet;
        var defaultDisplayPropertySet = standardMembers?.Members["DefaultDisplayPropertySet"]?.Value as PSPropertySet;

        if (defaultDisplayPropertySet?.ReferencedPropertyNames != null &&
            defaultDisplayPropertySet.ReferencedPropertyNames.Count > 0)
        {
            // Return only the properties in the DefaultDisplayPropertySet
            return defaultDisplayPropertySet.ReferencedPropertyNames
                .Select(name => firstObject.Properties[name])
                .Where(prop => prop != null)
                .Cast<PSPropertyInfo>()
                .ToList();
        }

        // Fall back to all visible properties (excluding PS* internal properties)
        return firstObject.Properties
            .Where(p => !p.Name.StartsWith("PS", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    ///     Converts a PowerShell object to a data table row with values extracted based on the specified properties and columns.
    /// </summary>
    /// <param name="psObject">The PowerShell object to convert.</param>
    /// <param name="properties">The list of properties to extract.</param>
    /// <param name="dataTableColumns">The list of columns with format specifications.</param>
    /// <param name="objectIndex">The original index of the object in the source collection.</param>
    /// <returns>A <see cref="DataTableRow" /> containing the extracted values.</returns>
    public static DataTableRow CastObjectToDataTableRow(PSObject psObject, List<PSPropertyInfo> properties, List<DataTableColumn> dataTableColumns, int objectIndex)
    {
        var valuePairs = new Dictionary<string, IValue>();

        for (int i = 0; i < properties.Count && i < dataTableColumns.Count; i++)
        {
            var property = properties[i];
            var column = dataTableColumns[i];
            var propValue = psObject.Properties[property.Name];
            object? rawValue = null;

            try
            {
                rawValue = propValue?.Value;
            }
            catch
            {
                // If property access fails, use null
            }

            // Use the column's FormatValue method to create the display string
            var displayValue = column.FormatValue(rawValue);

            // Determine if this is a numeric value for sorting
            var isDecimal = rawValue is decimal or int or long or short or byte or double or float;

            if (isDecimal)
            {
                var decimalValue = Convert.ToDecimal(rawValue, CultureInfo.InvariantCulture);
                valuePairs[column.ToString()] = new DecimalValue
                {
                    DisplayValue = displayValue,
                    SortValue = decimalValue
                };
            }
            else
            {
                valuePairs[column.ToString()] = new StringValue
                {
                    DisplayValue = displayValue,
                    RawValue = rawValue
                };
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
    /// <param name="psObjects">The list of PowerShell objects to convert.</param>
    /// <returns>A <see cref="DataTable" /> containing the columns and rows extracted from the PowerShell objects.</returns>
    public static DataTable CastObjectsToTableView(List<PSObject> psObjects)
    {
        if (psObjects.Count == 0)
        {
            return new DataTable([], []);
        }

        // Get the properties to display using PowerShell's format data
        var properties = GetFormatDataForObjects(psObjects);

        // Create columns from the properties with format strings based on property type
        var dataTableColumns = properties
            .Select(prop =>
            {
                var column = new DataTableColumn(prop.Name, $"$_.{prop.Name}");

                // Set format string based on property type
                var propType = prop.TypeNameOfValue;
                column.FormatString = propType switch
                {
                    "System.DateTime" => "G",  // General date/time
                    "System.Decimal" => "N2",  // Decimal with 2 decimal places
                    "System.Int32" or "System.Int64" or "System.Int16" or "System.Byte" => "N0",  // Integer numbers with thousand separators
                    "System.Double" or "System.Single" => "N2",  // Floating point with 2 decimals
                    _ => null
                };

                return column;
            })
            .ToList();

        // Convert each object to a row
        var dataTableRows = new List<DataTableRow>();
        for (var i = 0; i < psObjects.Count; i++)
        {
            var dataTableRow = CastObjectToDataTableRow(psObjects[i], properties, dataTableColumns, i);
            dataTableRows.Add(dataTableRow);
        }

        SetTypesOnDataColumns(dataTableRows, dataTableColumns);

        return new DataTable(dataTableColumns, dataTableRows);
    }
}