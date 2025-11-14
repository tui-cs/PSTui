// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace OutGridView.Cmdlet;

internal sealed class ShowObjectView : Window, ITreeBuilder<object>
{
    private readonly TreeView<object>? _tree;
    private readonly View? _filterErrorView;

    public bool SupportsCanExpand => true;
    private readonly Shortcut? _selectedShortcut;
    private readonly StatusBar? _statusBar;

    public ShowObjectView(List<object> rootObjects, ApplicationData applicationData)
    {
        Title = applicationData.Title;
        Width = Dim.Fill();
        Height = Dim.Fill(1);
        Modal = false;

        if (applicationData.MinUI)
        {
            BorderStyle = LineStyle.None;
            Title = string.Empty;
            X = -1;
            Height = Dim.Fill();
        }

        var filterLabel = new Label
        {
            Text = "_Filter:",
            X = 1
        };

        var filterTextField = new TextField
        {
            Text = applicationData.Filter ?? string.Empty,
            X = Pos.Right(filterLabel) + 1,
            Width = Dim.Fill(1)
        };
        filterTextField.CursorPosition = filterTextField.Text.Length;

        _filterErrorView = new Label
        {
            SchemeName = "Error",
            X = Pos.Right(filterLabel) + 1,
            Y = Pos.Top(filterLabel) + 1,
            Width = Dim.Width(filterTextField),
            Height = Dim.Auto(DimAutoStyle.Text)
        };

        _tree = new TreeView<object>
        {
            Y = Pos.Bottom(_filterErrorView),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _tree.TreeBuilder = this;
        _tree.AspectGetter = AspectGetter;
        _tree.SelectionChanged += SelectionChanged;

        var regexFilter = new RegexTreeViewTextFilter(this, _tree)
        {
            Text = applicationData.Filter ?? string.Empty
        };
        _tree.Filter = regexFilter;

        if (rootObjects.Count > 0)
            _tree.AddObjects(rootObjects);
        else
            _tree.AddObject("No Objects");

        filterTextField.TextChanged += OnFilterTextFieldOnTextChanged;

        var shortcuts = new List<Shortcut>();

        var elementDescription = "objects";

        var types = rootObjects.Select(o => o.GetType()).Distinct().ToArray();
        if (types.Length == 1) elementDescription = types[0].Name;
        shortcuts.Add(new Shortcut(Key.Esc, "Close", () => Application.RequestStop()));

        Shortcut countShortcut = new Shortcut(Key.Empty, $"{rootObjects.Count} {elementDescription}", null);
        _selectedShortcut = new Shortcut(Key.Empty, string.Empty, null);
        shortcuts.Add(countShortcut);
        shortcuts.Add(_selectedShortcut);

        if (applicationData.Debug)
        {
            shortcuts.Add(new Shortcut(Key.Empty, $" v{applicationData.ModuleVersion}", null));
            shortcuts.Add(new Shortcut(Key.Empty,
                $"{Application.Driver} v{FileVersionInfo.GetVersionInfo(Assembly.GetAssembly(typeof(Application))!.Location).ProductVersion}",
                null));
        }

        _statusBar = new StatusBar(shortcuts);
        _statusBar.Visible = !applicationData.MinUI;

        if (!applicationData.MinUI)
        {
            Add(filterLabel);
            Add(filterTextField);
            Add(_filterErrorView);
        }

        Add(_tree);
        Add(_statusBar);
        return;

        void OnFilterTextFieldOnTextChanged(object? sender, EventArgs e)
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
                _filterErrorView?.Text = ex.Message;
                return;
            }

            _filterErrorView?.Text = string.Empty;
            regexFilter.Text = textField.Text ?? string.Empty;
        }
    }

    internal void SetRegexError(string error)
    {
        if (string.Equals(error, _filterErrorView?.Text, StringComparison.Ordinal)) return;
        _filterErrorView?.Text = error;
    }

    private void SelectionChanged(object? sender, SelectionChangedEventArgs<object> e)
    {
        var selectedValue = e.NewValue;

        if (selectedValue is CachedMemberResult cmr) selectedValue = cmr.Value;

        if (selectedValue != null && _selectedShortcut != null)
            _selectedShortcut.Title = selectedValue.GetType().Name;
        else
            _selectedShortcut?.Title = string.Empty;

        _statusBar?.SetNeedsDraw();
    }

    private string? AspectGetter(object? toRender)
    {
        if (toRender is Process p) return p.ProcessName;
        if (toRender is null) return "Null";
        if (toRender is FileSystemInfo fsi && !IsRootObject(fsi)) return fsi.Name;

        return toRender.ToString();
    }

    private bool IsRootObject(object o) => _tree!.Objects.Contains(o);

    public bool CanExpand(object toExpand)
    {
        if (toExpand is CachedMemberResult p) return IsBasicType(p.Value);

        // Any complex object type can be expanded to reveal properties
        return IsBasicType(toExpand);
    }

    private static bool IsBasicType(object? value) =>
        value != null && value is not string && !value.GetType().IsValueType;

    public IEnumerable<object> GetChildren(object? forObject)
    {
        while (true)
        {
            if (forObject == null || !CanExpand(forObject)) return [];

            if (forObject is CachedMemberResult p)
            {
                if (p.IsCollection) return p.Elements ?? Enumerable.Empty<object>();

                forObject = p.Value;
                continue;
            }

            if (forObject is CachedMemberResultElement e)
            {
                forObject = e.Value;
                continue;
            }

            var children = new List<object>();

            foreach (var member in forObject.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public)
                         .OrderBy(m => m.Name))
            {
                if (member is PropertyInfo prop) children.Add(new CachedMemberResult(forObject, prop));

                if (member is FieldInfo field) children.Add(new CachedMemberResult(forObject, field));
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

    private static IEnumerable<object> GetExtraChildren(object forObject)
    {
        if (forObject is DirectoryInfo dir)
            foreach (var c in dir.EnumerateFileSystemInfos())
                yield return c;
    }

    internal static void Run(List<PSObject> objects, ApplicationData applicationData)
    {
        // In Terminal.Gui v2, Application.Init() no longer accepts a driver parameter.
        // Instead, use Application.ForceDriver to specify the driver.
        if (applicationData.UseNetDriver) Application.ForceDriver = "NetDriver";
        Application.Init();
        Window? window = null;

        try
        {
            window = new ShowObjectView(objects.Select(p => p.BaseObject).ToList(), applicationData);
            Application.Run(window);
        }
        finally
        {
            window?.Dispose();
            Application.Shutdown();
        }
    }
}

internal sealed class CachedMemberResultElement
{
    public int Index;
    public object? Value;

    private readonly string _representation;

    public CachedMemberResultElement(object? value, int index)
    {
        Index = index;
        Value = value;

        try
        {
            _representation = Value?.ToString() ?? "Null";
        }
        catch (Exception)
        {
            Value = _representation = "Unavailable";
        }
    }

    public override string ToString() => $"[{Index}]: {_representation}]";
}

internal sealed class CachedMemberResult
{
    public MemberInfo Member { get; set; }
    public object? Value { get; set; }
    public object Parent { get; set; }
    private readonly string? _representation;
    private List<CachedMemberResultElement>? _valueAsList;


    public bool IsCollection => _valueAsList != null;
    public IReadOnlyCollection<CachedMemberResultElement>? Elements => _valueAsList?.AsReadOnly();

    public CachedMemberResult(object parent, MemberInfo mem)
    {
        Parent = parent;
        Member = mem;

        try
        {
            if (mem is PropertyInfo p)
                Value = p.GetValue(parent);
            else if (mem is FieldInfo f)
                Value = f.GetValue(parent);
            else
                throw new NotSupportedException($"Unknown {nameof(MemberInfo)} Type");

            _representation = ValueToString();
        }
        catch (Exception)
        {
            Value = _representation = "Unavailable";
        }
    }

    private string? ValueToString()
    {
        if (Value == null) return "Null";
        try
        {
            if (IsCollectionOfKnownTypeAndSize(out var elementType, out var size))
                return $"{elementType!.Name}[{size}]";
        }
        catch (Exception)
        {
            return Value?.ToString();
        }


        return Value?.ToString();
    }

    private bool IsCollectionOfKnownTypeAndSize(out Type? elementType, out int size)
    {
        elementType = null;
        size = 0;

        if (Value is null or string) return false;

        if (Value is IEnumerable enumerable)
        {
            var list = enumerable.Cast<object>().ToList();

            var types = list.Where(v => v != null).Select(v => v!.GetType()).Distinct().ToArray();

            if (types.Length == 1)
            {
                elementType = types[0];
                size = list.Count;

                _valueAsList = list.Select((e, i) => new CachedMemberResultElement(e, i)).ToList();
                return true;
            }
        }

        return false;
    }

    public override string ToString() => Member.Name + ": " + _representation;
}

internal sealed class RegexTreeViewTextFilter(ShowObjectView parent, TreeView<object> forTree) : ITreeViewFilter<object>
{
    private readonly TreeView<object> _forTree = forTree ?? throw new ArgumentNullException(nameof(forTree));

    private string _text = string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            RefreshTreeView();
        }
    }

    private void RefreshTreeView()
    {
        _forTree.InvalidateLineMap();
        _forTree.SetNeedsDraw();
    }

    public bool IsMatch(object model)
    {
        if (string.IsNullOrWhiteSpace(Text)) return true;

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
}