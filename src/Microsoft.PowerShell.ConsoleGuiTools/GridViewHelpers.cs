// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.PowerShell.ConsoleGuiTools
{
    /// <summary>
    /// Provides helper methods for filtering and formatting data in the grid view.
    /// </summary>
    internal sealed class GridViewHelpers
    {
        /// <summary>
        /// Filters a list of grid view rows based on a regular expression pattern.
        /// Marked items are always included and appear first in the result, followed by unmarked items that match the filter.
        /// </summary>
        /// <param name="listToFilter">The list of rows to filter.</param>
        /// <param name="filter">The regular expression pattern to match against the display string. If null or empty, the original list is returned.</param>
        /// <returns>A filtered list with marked items first, followed by matching unmarked items.</returns>
        public static List<GridViewRow> FilterData(List<GridViewRow> listToFilter, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return listToFilter;
            }

            var filteredList = new List<GridViewRow>();
            filteredList.AddRange(listToFilter.Where(gvr => gvr.IsMarked));
            filteredList.AddRange(listToFilter.Where(gvr => !gvr.IsMarked && Regex.IsMatch(gvr.DisplayString!, filter, RegexOptions.IgnoreCase)));

            return filteredList;
        }

        public static string GetPaddedString(List<string>? strings, int offset, int[]? listViewColumnWidths)
        {
            if (listViewColumnWidths is null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            if (offset > 0)
            {
                builder.Append(string.Empty.PadRight(offset));
            }

            if (strings == null) return builder.ToString();
            for (int i = 0; i < strings.Count; i++)
            {
                if (i > 0)
                {
                    // Add a space between columns
                    builder.Append(' ');
                }

                // Replace any newlines with encoded newline/linefeed (`n or `r)
                // Note we can't use Environment.Newline because we don't know that the
                // command honors that.
                strings[i] = strings[i].Replace("\r", "`r");
                strings[i] = strings[i].Replace("\n", "`n");

                // If the string doesn't fit in the column, append an ellipsis.
                // Guard against negative or very small column widths
                if (listViewColumnWidths[i] <= 0)
                {
                    // Skip columns with zero or negative width (but separator already added above)
                }
                else if (listViewColumnWidths[i] < 4)
                {
                    // For very small columns (< 4), just truncate without ellipsis
                    var truncateLength = Math.Min(strings[i].Length, listViewColumnWidths[i]);
                    builder.Append(strings[i], 0, truncateLength);
                }
                else if (strings[i].Length > listViewColumnWidths[i])
                {
                    builder.Append(strings[i], 0, listViewColumnWidths[i] - 3);
                    builder.Append("...");
                }
                else
                {
                    builder.Append(strings[i].PadRight(listViewColumnWidths[i]));
                }
            }

            return builder.ToString();
        }
    }
}