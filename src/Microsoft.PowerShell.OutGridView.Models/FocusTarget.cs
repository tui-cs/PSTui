// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.OutGridView.Models;

/// <summary>
///     Specifies which UI element should receive initial focus.
/// </summary>
public enum FocusTarget
{
    /// <summary>
    ///     The table/grid view receives initial focus (default).
    /// </summary>
    Table,

    /// <summary>
    ///     The filter text field receives initial focus.
    /// </summary>
    Filter
}
