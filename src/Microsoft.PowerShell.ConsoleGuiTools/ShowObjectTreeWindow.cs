// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the Terminal.Gui Window implementation for displaying object hierarchies in a tree view with filtering capabilities.
/// </summary>
internal sealed class ShowObjectTreeWindow : Window, ITreeBuilder<object>
{
    private const string FILTER_LABEL = "_Filter:";

    #region Fields

    private readonly TreeView<object> _tree;
    private readonly View _filterErrorView;
    private Shortcut? _selectedShortcut;
    private readonly StatusBar _statusBar;
    private readonly ApplicationData _applicationData;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets a value indicating whether this tree builder supports the CanExpand operation.
    /// </summary>
    public bool SupportsCanExpand => true;

    #endregion

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of the <see cref="ShowObjectTreeWindow" /> class with the specified application data.
    /// </summary>
    /// <param name="applicationData">The configuration and PSObjects to display.</param>
    public ShowObjectTreeWindow(ApplicationData applicationData)
    {
        _applicationData = applicationData;
        Title = _applicationData.Title ?? "Show-ObjectView";
        Width = Dim.Fill();
        Height = Dim.Fill(1);

        if (_applicationData.MinUI)
        {
            BorderStyle = LineStyle.None;
            Title = string.Empty;
            X = -1;
            Height = Dim.Fill();
        }

        // Extract root objects from PSObjects
        var rootObjects = _applicationData.PSObjects?.Select(p =>
        {
            if (p is PSObject pso)
                return pso.BaseObject;
            return p;
        }).ToList() ?? [];

        var filterLabel = new Label
        {
            Text = FILTER_LABEL,
            X = 1
        };

        var filterTextField = new TextField
        {
            Text = _applicationData.Filter ?? string.Empty,
            X = Pos.Right(filterLabel) + 1,
            Width = Dim.Fill(1),
            InsertionPoint= (_applicationData.Filter ?? string.Empty).Length
        };

        _filterErrorView = new Label
        {
            SchemeName = SchemeManager.SchemesToSchemeName(Schemes.Error),
            X = Pos.Right(filterLabel) + 1,
            Y = Pos.Top(filterLabel) + 1,
            Width = Dim.Width(filterTextField),
            Height = Dim.Auto(DimAutoStyle.Text)
        };

        _tree = new TreeView<object>
        {
            Y = Pos.Bottom(_filterErrorView),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            TreeBuilder = this,
            AspectGetter = AspectGetter
        };
        _tree.SelectionChanged += SelectionChanged;

        var regexFilter = new RegexTreeViewTextFilter(this, _tree)
        {
            Text = _applicationData.Filter ?? string.Empty
        };
        _tree.Filter = regexFilter;

        if (rootObjects.Count > 0)
            _tree.AddObjects(rootObjects);
        else
            _tree.AddObject("No Objects");

        filterTextField.TextChanged += (sender, e) => OnFilterTextChanged(sender, e, regexFilter);

        var shortcuts = CreateShortcuts(rootObjects);

        _statusBar = new StatusBar(shortcuts)
        {
            Visible = !_applicationData.MinUI
        };

        if (!_applicationData.MinUI)
        {
            Add(filterLabel);
            Add(filterTextField);
            Add(_filterErrorView);
        }

        Add(_tree);
    }

    protected override void OnIsRunningChanged(bool newIsRunning)
    {
        base.OnIsRunningChanged(newIsRunning);
        if (!newIsRunning) return;

        // We do this here, because _statusBar requires the Application to be running to
        // access the driver information.
        Add(_statusBar);
        _tree.SetFocus();
    }

    #endregion

        #region Event Handlers

        /// <summary>
        ///     Handles filter text changes and applies the regex filter.
        /// </summary>
        /// <param name="sender">The text field that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <param name="regexFilter">The regex filter to update.</param>
    private void OnFilterTextChanged(object? sender, EventArgs e, RegexTreeViewTextFilter regexFilter)
    {
        var textField = sender as TextField;
        if (textField is null) return;

        // Test that the regex is valid before applying it
        try
        {
            _ = new Regex(textField.Text ?? string.Empty, RegexOptions.IgnoreCase);
        }
        catch (RegexParseException ex)
        {
            _filterErrorView.Text = ex.Message;
            return;
        }

        _filterErrorView.Text = string.Empty;
        regexFilter.Text = textField.Text ?? string.Empty;
    }

    /// <summary>
    ///     Handles selection changes in the tree view and updates the status bar.
    /// </summary>
    /// <param name="sender">The tree view that triggered the event.</param>
    /// <param name="e">The selection changed event arguments.</param>
    private void SelectionChanged(object? sender, SelectionChangedEventArgs<object> e)
    {
        var selectedValue = e.NewValue;

        if (selectedValue is CachedMemberResult cmr)
            selectedValue = cmr.Value;

        _selectedShortcut?.Title = selectedValue != null ? selectedValue.GetType().Name : string.Empty;

        _statusBar.SetNeedsDraw();
    }

    #endregion

    #region Public Methods

    /// <summary>
    ///     Sets the regex error message displayed in the filter error view.
    /// </summary>
    /// <param name="error">The error message to display.</param>
    internal void SetRegexError(string error)
    {
        if (string.Equals(error, _filterErrorView.Text, StringComparison.Ordinal)) return;
        _filterErrorView.Text = error;
    }

    #endregion

    #region ITreeBuilder Implementation

    /// <summary>
    ///     Determines whether the specified object can be expanded to show children.
    /// </summary>
    /// <param name="toExpand">The object to check for expansion capability.</param>
    /// <returns><see langword="true" /> if the object can be expanded; otherwise, <see langword="false" />.</returns>
    public bool CanExpand(object toExpand)
    {
        if (toExpand is CachedMemberResult p) return IsBasicType(p.Value);

        return IsBasicType(toExpand);
    }

    /// <summary>
    ///     Gets the child objects for the specified parent object.
    /// </summary>
    /// <param name="forObject">The parent object to get children for.</param>
    /// <returns>An enumerable collection of child objects.</returns>
    public IEnumerable<object> GetChildren(object? forObject)
    {
        while (true)
        {
            if (forObject == null || !CanExpand(forObject)) return [];

            switch (forObject)
            {
                case CachedMemberResult { IsCollection: true } p:
                    return p.Elements ?? Enumerable.Empty<object>();
                case CachedMemberResult p:
                    forObject = p.Value;
                    continue;
                case CachedMemberResultElement e:
                    forObject = e.Value;
                    continue;
            }

            var children = new List<object>();

            foreach (var member in forObject.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public)
                         .OrderBy(m => m.Name))
            {
                if (member is PropertyInfo prop)
                    children.Add(new CachedMemberResult(forObject, prop));

                if (member is FieldInfo field)
                    children.Add(new CachedMemberResult(forObject, field));
            }

            try
            {
                children.AddRange(GetExtraChildren(forObject));
            }
            catch (Exception)
            {
                // Extra children unavailable, possibly security or IO exceptions enumerating children etc
            }

            return children;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    ///     Gets the display text for an object in the tree view.
    /// </summary>
    /// <param name="toRender">The object to get display text for.</param>
    /// <returns>The display text for the object.</returns>
    private string? AspectGetter(object? toRender)
    {
        return toRender switch
        {
            Process p => p.ProcessName,
            null => "Null",
            FileSystemInfo fsi when !IsRootObject(fsi) => fsi.Name,
            _ => toRender.ToString()
        };
    }

    /// <summary>
    ///     Determines whether the specified object is a root object in the tree.
    /// </summary>
    /// <param name="o">The object to check.</param>
    /// <returns><see langword="true" /> if the object is a root object; otherwise, <see langword="false" />.</returns>
    private bool IsRootObject(object o) => _tree.Objects.Contains(o);

    /// <summary>
    ///     Determines whether the specified value is a basic (non-primitive, non-string) type that can be expanded.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true" /> if the value is a basic type; otherwise, <see langword="false" />.</returns>
    private static bool IsBasicType(object? value) =>
        value != null && value is not string && !value.GetType().IsValueType;

    /// <summary>
    ///     Gets additional child objects for special types like DirectoryInfo.
    /// </summary>
    /// <param name="forObject">The object to get extra children for.</param>
    /// <returns>An enumerable collection of additional child objects.</returns>
    private static IEnumerable<object> GetExtraChildren(object forObject)
    {
        if (forObject is DirectoryInfo dir)
            foreach (var c in dir.EnumerateFileSystemInfos())
                yield return c;
    }

    /// <summary>
    ///     Creates the keyboard shortcuts for the status bar.
    /// </summary>
    /// <param name="rootObjects">The root objects being displayed.</param>
    /// <returns>A list of shortcuts for the status bar.</returns>
    private List<Shortcut> CreateShortcuts(List<object> rootObjects)
    {
        var shortcuts = new List<Shortcut>();

        var elementDescription = "objects";
        var types = rootObjects.Select(o => o.GetType()).Distinct().ToArray();
        if (types.Length == 1)
            elementDescription = types[0].Name;

        shortcuts.Add(new Shortcut(Key.Esc, "Close", () => App?.RequestStop()));

        var countShortcut = new Shortcut(Key.Empty, $"{rootObjects.Count} {elementDescription}", null);
        _selectedShortcut = new Shortcut(Key.Empty, string.Empty, null);
        shortcuts.Add(countShortcut);
        shortcuts.Add(_selectedShortcut);

        if (_applicationData.Verbose || _applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{_applicationData.ModuleVersion}", null));
            FileVersionInfo tgFileVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location);
            string tgVersion = tgFileVersionInfo?.FileVersion ?? "no version found";
            //if (tgFileVersionInfo is { IsPreRelease: true })
            {
                tgVersion = tgFileVersionInfo?.ProductVersion?[..tgFileVersionInfo.ProductVersion.IndexOf('+')] ?? tgVersion;
            }
            shortcuts.Add(new Shortcut(Key.Empty,
                $"{App?.Driver?.GetName()} v{tgVersion}",
                null));
        }

        return shortcuts;
    }

    #endregion
}
