// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Internal;
using Microsoft.PowerShell.OutGridView.Models;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Sends output to an interactive table using Terminal.Gui's TableView.
///     Renders with native column headers, sizing, horizontal scrolling, and streaming support.
/// </summary>
[Cmdlet(VerbsData.Out, "ConsoleGridView")]
[Alias("ocgv")]
public class OutConsoleGridViewCmdletCommand : PSCmdlet, IDisposable
{
    #region Properties

    private const string DATA_NOT_QUALIFIED_FOR_GRID_VIEW = nameof(DATA_NOT_QUALIFIED_FOR_GRID_VIEW);
    private const string ENVIRONMENT_NOT_SUPPORTED_FOR_GRID_VIEW = nameof(ENVIRONMENT_NOT_SUPPORTED_FOR_GRID_VIEW);

    private readonly OutConsoleGridView _outConsoleGridView = new();
    private bool _initialized;

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
    ///     Gets or sets a value indicating whether the TUI should run in full screen mode.
    /// </summary>
    [Parameter(HelpMessage = "If specified the TUI will run in full screen mode instead of inline.")]
    public SwitchParameter FullScreen { set; get; }

    /// <summary>
    ///     Gets or sets which UI element should receive initial focus (Table or Filter).
    /// </summary>
    [Parameter(HelpMessage = "Specifies which UI element receives initial focus. Valid values are 'Table' (default) and 'Filter'.")]
    public FocusTarget Focus { set; get; } = FocusTarget.Table;

    /// <summary>
    ///     Gets or sets a search string that positions the cursor on the first matching row.
    /// </summary>
    [Parameter(HelpMessage = "Positions the cursor on the first row matching this regex pattern. Unlike -Filter, non-matching rows remain visible.")]
    public string? Search { set; get; }

    /// <summary>
    ///     Gets or sets the Terminal.Gui driver to use.
    /// </summary>
    [Parameter(HelpMessage =
        "Sets the Terminal.Gui driver to use. Valid values are 'ansi', 'windows', or 'unix'.")]
    [Alias("ForceDriver")]
    public string? Driver { set; get; }

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
    ///     Validates that the environment supports the grid view.
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

        if (!_initialized)
        {
            _initialized = true;
            var applicationData = new ApplicationData
            {
                Title = Title,
                OutputMode = OutputMode,
                Filter = Filter,
                MinUI = MinUI,
                FullScreen = FullScreen,
                Focus = Focus,
                Search = Search,
                Driver = Driver,
                Verbose = Verbose,
                Debug = Debug,
                ModuleVersion = MyInvocation.MyCommand.Version.ToString()
            };
            _outConsoleGridView.Initialize(applicationData);
        }

        _outConsoleGridView.AddObject(input);
    }

    /// <summary>
    ///     Performs final processing after all pipeline objects have been received.
    ///     Signals the UI that loading is complete and writes selected objects to the pipeline.
    /// </summary>
    protected override void EndProcessing()
    {
        base.EndProcessing();

        if (!_initialized) return;

        HashSet<int> selectedIndexes = _outConsoleGridView.Complete();
        foreach (var idx in selectedIndexes)
        {
            WriteObject(_outConsoleGridView.GetObject(idx), false);
        }
    }

    /// <summary>
    ///     Releases all resources.
    /// </summary>
    public void Dispose()
    {
        _outConsoleGridView.Dispose();
        GC.SuppressFinalize(this);
    }
}
