// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Internal;
using Microsoft.PowerShell.OutGridView.Models;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Sends output to an interactive table in a separate console window. This class is invoked by PowerShell when the
///     Out-ConsoleGridView cmdlet is called.
/// </summary>
[Cmdlet(VerbsData.Out, "ConsoleGridView")]
[Alias("ocgv")]
public class OutConsoleGridViewCmdletCommand : PSCmdlet, IDisposable
{
    #region Properties

    private const string DATA_NOT_QUALIFIED_FOR_GRID_VIEW = nameof(DATA_NOT_QUALIFIED_FOR_GRID_VIEW);
    private const string ENVIRONMENT_NOT_SUPPORTED_FOR_GRID_VIEW = nameof(ENVIRONMENT_NOT_SUPPORTED_FOR_GRID_VIEW);

    private readonly List<PSObject> _psObjects = [];
    private readonly OutConsoleGridView _outConsoleGridView = new();

    #endregion Properties

    #region Input Parameters

    /// <summary>
    ///     Gets or sets the current pipeline object.
    /// </summary>
    [Parameter(ValueFromPipeline = true, HelpMessage = "Specifies the input pipeline object")]
    public PSObject InputObject { get; set; } = AutomationNull.Value;

    /// <summary>
    ///     Gets or sets the title of the Out-ConsoleGridView window.
    /// </summary>
    [Parameter(HelpMessage =
        "Specifies the text that appears in the title bar of the Out-ConsoleGridView window. By default, the title bar displays the command that invokes Out-ConsoleGridView.")]
    [ValidateNotNullOrEmpty]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the selected items should be written to the pipeline
    ///     and if it should be possible to select multiple or single list items.
    /// </summary>
    [Parameter(HelpMessage =
        "Determines whether a single item (Single), multiple items (Multiple; default), or no items (None) will be written to the pipeline. Also determines selection behavior in the TUI.")]
    public OutputModeOption OutputMode { set; get; } = OutputModeOption.Multiple;

    /// <summary>
    ///     Gets or sets the initial value for the filter in the TUI.
    /// </summary>
    [Parameter(HelpMessage =
        "Pre-populates the Filter edit box, allowing filtering to be specified on the command line. The filter uses regular expressions.")]
    public string? Filter { set; get; }

    /// <summary>
    ///     Gets or sets a value indicating whether "minimum UI" mode will be enabled.
    /// </summary>
    [Parameter(HelpMessage = "If specified no window frame, filter box, or status bar will be displayed in the TUI.")]
    public SwitchParameter MinUI { set; get; }

    /// <summary>
    ///     Gets or sets the Terminal.Gui driver to use.
    /// </summary>
    [Parameter(HelpMessage =
        "Forces the Terminal.Gui driver to use. Valid values are 'ansi', 'windows', or 'unix'.")]
    public string? ForceDriver { set; get; }

    /// <summary>
    ///     Gets or sets a value indicating whether all properties should be displayed instead of just the default display properties.
    /// </summary>
    [Parameter(HelpMessage =
        "If specified, all properties of the objects will be displayed instead of just the default display properties.")]
    public SwitchParameter AllProperties { set; get; }

    /// <summary>
    ///     Gets a value indicating whether the Verbose switch is present.
    /// </summary>
    public bool Verbose => MyInvocation.BoundParameters.ContainsKey("Verbose");

    /// <summary>
    ///     Gets a value indicating whether the Debug switch is present.
    /// </summary>
    public bool Debug => MyInvocation.BoundParameters.ContainsKey("Debug");

    #endregion Input Parameters

    /// <summary>
    ///     Performs initialization of command execution. Validates that the environment supports grid view.
    /// </summary>
    protected override void BeginProcessing()
    {
        if (Console.IsInputRedirected)
        {
            var error = new ErrorRecord(
                new PSNotSupportedException("Not supported in this environment (when input is redirected)."),
                ENVIRONMENT_NOT_SUPPORTED_FOR_GRID_VIEW,
                ErrorCategory.NotImplemented,
                null);

            ThrowTerminatingError(error);
        }
    }

    /// <summary>
    ///     Processes each input object received from the pipeline.
    /// </summary>
    protected override void ProcessRecord()
    {
        if (Equals(InputObject, AutomationNull.Value)) return;

        if (InputObject.BaseObject is IDictionary dictionary)
            // Dictionaries should be enumerated through because the pipeline does not enumerate through them.
            foreach (DictionaryEntry entry in dictionary)
                ProcessObject(PSObject.AsPSObject(entry));
        else
            ProcessObject(InputObject);
    }

    /// <summary>
    ///     Processes a single object for display in the grid view.
    /// </summary>
    /// <param name="input">The PSObject to process.</param>
    /// <exception cref="FormatException">Thrown when the data type is not supported for Out-ConsoleGridView.</exception>
    private void ProcessObject(PSObject input)
    {
        var baseObject = input.BaseObject;

        // Throw a terminating error for types that are not supported.
        if (baseObject is ScriptBlock ||
            baseObject is SwitchParameter ||
            baseObject is PSReference ||
            baseObject is PSObject)
        {
            var error = new ErrorRecord(
                new FormatException("Invalid data type for Out-ConsoleGridView"),
                DATA_NOT_QUALIFIED_FOR_GRID_VIEW,
                ErrorCategory.InvalidType,
                null);

            ThrowTerminatingError(error);
        }

        _psObjects.Add(input);
    }

    /// <summary>
    ///     Performs final processing after all pipeline objects have been received.
    ///     Displays the console grid view and writes selected objects to the pipeline.
    /// </summary>
    protected override void EndProcessing()
    {
        base.EndProcessing();

        // Return if no objects
        if (_psObjects.Count == 0) return;

        var applicationData = new ApplicationData
        {
            PSObjects = _psObjects.Cast<object>().ToList(),
            Title = Title ?? "Out-ConsoleGridView",
            OutputMode = OutputMode,
            Filter = Filter,
            MinUI = MinUI,
            ForceDriver = ForceDriver,
            AllProperties = AllProperties,
            Verbose = Verbose,
            Debug = Debug,
            ModuleVersion = MyInvocation.MyCommand.Version.ToString()
        };

        HashSet<int> selectedIndexes = _outConsoleGridView.Run(applicationData);
        foreach (var idx in selectedIndexes)
        {
            var selectedObject = _psObjects[idx];

            WriteObject(selectedObject, false);
        }
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="OutConsoleGridViewCmdletCommand" />.
    /// </summary>
    public void Dispose()
    {
        _outConsoleGridView.Dispose();
        GC.SuppressFinalize(this);
    }
}