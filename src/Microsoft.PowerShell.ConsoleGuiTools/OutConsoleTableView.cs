// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the main orchestration for the non-streaming Out-ConsoleTableView path,
///     managing the Terminal.Gui application lifecycle. Used when all objects are available
///     upfront (e.g., called from the static orchestrator or tests).
/// </summary>
internal sealed class OutConsoleTableView : System.IDisposable
{
    /// <summary>
    ///     Runs the table view Terminal.Gui Application with the specified configuration
    ///     and returns the indexes of selected items.
    /// </summary>
    public HashSet<int> Run(ApplicationData applicationData)
    {
        // Convert PSObjects to DataTable
        DataTable dataTable;
        if (applicationData.PSObjects is { Count: > 0 })
        {
            var psObjects = applicationData.PSObjects.Cast<PSObject>().ToList();
            dataTable = TypeGetter.CastObjectsToTableView(psObjects, applicationData.AllProperties);
        }
        else
        {
            dataTable = new DataTable([], []);
        }

        var dataSource = OutTableViewDataSource.FromDataTable(dataTable);

        Terminal.Gui.Configuration.ConfigurationManager.Enable(Terminal.Gui.Configuration.ConfigLocations.All);

        using OutTableViewWindow window = new(applicationData, dataSource);
        window.OnPipelineComplete(); // All data is available upfront
        using IApplication app = Application.Create().Init(driverName: applicationData.ForceDriver);
        HashSet<int>? selectedIndexes = app.Run(window) as HashSet<int>;
        return selectedIndexes ?? [];
    }

    public void Dispose()
    {
        // No resources to dispose currently
    }
}
