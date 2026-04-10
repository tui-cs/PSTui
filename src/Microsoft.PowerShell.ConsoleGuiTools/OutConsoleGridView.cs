// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the main orchestration for the Out-ConsoleGridView cmdlet, managing the Terminal.Gui application lifecycle
///     and coordinating between the application data and the grid view window.
/// </summary>
/// <remarks>
///     This class serves as a facade that initializes the Terminal.Gui framework, creates and runs the grid view window,
///     and handles cleanup operations. It delegates the actual UI rendering and user interaction to the
///     <see cref="OutGridViewWindow" /> class.
/// </remarks>
internal sealed class OutConsoleGridView : IDisposable
{
    private ApplicationData? _applicationData;

    /// <summary>
    ///     Runs the grid view Terminal.Gui Application with the specified configuration and returns the indexes of selected items.
    /// </summary>
    /// <param name="applicationData">
    ///     The application configuration containing the data table, output mode, filter settings, and other display options.
    /// </param>
    /// <returns>
    ///     A <see cref="HashSet{T}" /> containing the zero-based indexes of items selected by the user.
    ///     Returns an empty set if the user cancels the operation or if no items were selected.
    /// </returns>
    public HashSet<int> Run(ApplicationData applicationData)
    {
        _applicationData = applicationData;

        Terminal.Gui.Configuration.ConfigurationManager.Enable(Terminal.Gui.Configuration.ConfigLocations.All);

        using OutGridViewWindow window = new(_applicationData);
        using IApplication app = Application.Create().Init(driverName: _applicationData.ForceDriver);
        HashSet<int>? selectedIndexes = app.Run(window) as HashSet<int>;
        return selectedIndexes ?? [];
    }

    /// <summary>
    ///     Releases resources used by the <see cref="OutConsoleGridView" />.
    /// </summary>
    /// <remarks>
    ///     Currently, there are no resources to dispose. This method is provided for future extensibility
    ///     and to follow the standard IDisposable pattern.
    /// </remarks>
    public void Dispose()
    {
        // No resources to dispose currently
    }
}