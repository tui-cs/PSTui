// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSTui.Models;
using Terminal.Gui.App;

namespace PSTui;

/// <summary>
///     Provides the main orchestration for the Show-ObjectTree cmdlet, managing the Terminal.Gui application lifecycle
///     and coordinating between the application data and the tree view window.
/// </summary>
/// <remarks>
///     This class serves as a facade that initializes the Terminal.Gui framework, creates and runs the tree view window,
///     and handles cleanup operations. It delegates the actual UI rendering and user interaction to the <see cref="ShowObjectTreeWindow" /> class.
/// </remarks>
internal sealed class ShowObjectView : IDisposable
{
    /// <summary>
    ///     Runs the Show-ObjectView Terminal.Gui Application with the specified configuration.
    /// </summary>
    /// <param name="applicationData">The application configuration containing the PSObjects and display options.</param>
    /// <remarks>
    ///     <para>
    ///         This method initializes the Terminal.Gui framework, creates a <see cref="ShowObjectTreeWindow" /> instance,
    ///         and runs it until the user closes the window. The method handles the complete application lifecycle
    ///         including initialization, execution, and shutdown.
    ///     </para>
    /// </remarks>
    internal static void Run(ApplicationData applicationData)
    {
        // Terminal.Gui 2.5 dropped ConfigurationManager (tui-cs/Terminal.Gui#5416).
        // Library defaults, ~/.tui, ./.tui, and TUI_CONFIG are applied at assembly
        // load via TuiConfigurationBuilder.Shared. Re-apply with the cmdlet name so
        // ./.tui/Show-ObjectTree.config.json is discovered even though the entry
        // assembly is pwsh, not this module, and with the session's current filesystem
        // location, because pwsh does not sync the process working directory with cd.
        new Terminal.Gui.Configuration.TuiConfigurationBuilder("Show-ObjectTree", applicationData.CurrentDirectory)
            .ApplyToStaticFacades();

        ShowObjectTreeWindow window = new(applicationData);
        IApplication app = Application.Create();
        app.AppModel = applicationData.FullScreen ? AppModel.FullScreen : AppModel.Inline;
        app.ForceInlinePosition = new(0, 10);
        app.Init(driverName: applicationData.Driver);
        app.Run(window);
        window.Dispose();
        app.Dispose();
    }

    /// <summary>
    ///     Releases resources used by the <see cref="ShowObjectView" />.
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