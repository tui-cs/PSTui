// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Text.RegularExpressions;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides regex-based filtering for a TreeView, allowing users to filter tree nodes by matching their display text against a regular expression pattern.
/// </summary>
/// <param name="parent">The parent ShowObjectTreeView that owns this filter.</param>
/// <param name="forTree">The TreeView to apply filtering to.</param>
internal sealed class RegexTreeViewTextFilter(ShowObjectTreeWindow parent, TreeView<object> forTree) : ITreeViewFilter<object>
{
    #region Fields

    private readonly TreeView<object> _forTree = forTree ?? throw new ArgumentNullException(nameof(forTree));
    private string _text = string.Empty;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets the regex pattern text used for filtering.
    /// </summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            RefreshTreeView();
        }
    }

    #endregion

    #region ITreeViewFilter Implementation

    /// <summary>
    ///     Determines whether the specified model object matches the current filter criteria.
    /// </summary>
    /// <param name="model">The model object to test against the filter.</param>
    /// <returns><see langword="true" /> if the object matches the filter or no filter is set; otherwise, <see langword="false" />.</returns>
    public bool IsMatch(object model)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return true;

        var modelText = _forTree.AspectGetter(model);
        try
        {
            var isMatch = Regex.IsMatch(modelText ?? string.Empty, Text, RegexOptions.IgnoreCase);
            parent.SetRegexError(string.Empty);
            return isMatch;
        }
        catch (RegexParseException e)
        {
            parent.SetRegexError(e.Message);
            return false;
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Refreshes the tree view to apply the updated filter.
    /// </summary>
    private void RefreshTreeView()
    {
        _forTree.InvalidateLineMap();
        _forTree.SetNeedsDraw();
    }

    #endregion
}
