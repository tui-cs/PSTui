// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Represents a cached reflection result for a property or field member, including its value and collection details.
/// </summary>
internal sealed class CachedMemberResult
{
    #region Fields

    private readonly string? _representation;
    private List<CachedMemberResultElement>? _valueAsList;

    #endregion

    #region Properties

    /// <summary>
    ///     Gets or sets the member information (property or field) that was accessed.
    /// </summary>
    public MemberInfo Member { get; set; }

    /// <summary>
    ///     Gets or sets the value retrieved from the member.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    ///     Gets or sets the parent object that contains this member.
    /// </summary>
    public object Parent { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this member's value is a collection.
    /// </summary>
    public bool IsCollection => _valueAsList != null;

    /// <summary>
    ///     Gets the collection elements if this member's value is a collection; otherwise, <see langword="null" />.
    /// </summary>
    public IReadOnlyCollection<CachedMemberResultElement>? Elements => _valueAsList?.AsReadOnly();

    #endregion

    #region Constructor

    /// <summary>
    ///     Initializes a new instance of the <see cref="CachedMemberResult" /> class by reflecting on the specified member.
    /// </summary>
    /// <param name="parent">The parent object containing the member.</param>
    /// <param name="mem">The member information to retrieve the value from.</param>
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

    #endregion

    #region Overrides

    /// <summary>
    ///     Returns a string representation of this member in the format "MemberName: value".
    /// </summary>
    /// <returns>A formatted string showing the member name and value.</returns>
    public override string ToString() => Member.Name + ": " + _representation;

    #endregion

    #region Private Methods

    /// <summary>
    ///     Converts the member's value to a string representation, detecting collections and formatting them appropriately.
    /// </summary>
    /// <returns>A string representation of the value.</returns>
    private string? ValueToString()
    {
        if (Value == null)
            return "Null";

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

    /// <summary>
    ///     Determines whether the value is a collection of a known type and caches the collection elements.
    /// </summary>
    /// <param name="elementType">When this method returns, contains the element type if the value is a homogeneous collection; otherwise, <see langword="null" />.</param>
    /// <param name="size">When this method returns, contains the size of the collection if applicable; otherwise, 0.</param>
    /// <returns><see langword="true" /> if the value is a collection of a single known type; otherwise, <see langword="false" />.</returns>
    private bool IsCollectionOfKnownTypeAndSize(out Type? elementType, out int size)
    {
        elementType = null;
        size = 0;

        if (Value is null or string)
            return false;

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

    #endregion
}
