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
///     Displays objects in a hierarchical tree view in a separate console window. This class is invoked by PowerShell when
///     the
///     Show-ObjectTree cmdlet is called.
/// </summary>
[Cmdlet("Show", "ObjectTree")]
[Alias("shot")]
public class ShowObjectTreeCmdletCommand : PSCmdlet, IDisposable
{
    #region Properties

    private const string DATA_NOT_QUALIFIED_FOR_SHOW_OBJECT_TREE = nameof(DATA_NOT_QUALIFIED_FOR_SHOW_OBJECT_TREE);

    private const string ENVIRONMENT_NOT_SUPPORTED_FOR_SHOW_OBJECT_TREE =
        nameof(ENVIRONMENT_NOT_SUPPORTED_FOR_SHOW_OBJECT_TREE);

    private readonly List<PSObject> _psObjects = [];

    #endregion Properties

    #region Input Parameters

    /// <summary>
    ///     Gets or sets the current pipeline object.
    /// </summary>
    [Parameter(ValueFromPipeline = true, HelpMessage = "Specifies the input pipeline object")]
    public PSObject InputObject { get; set; } = AutomationNull.Value;

    /// <summary>
    ///     Gets or sets the title of the Show-ObjectTree window.
    /// </summary>
    [Parameter(HelpMessage =
        "Specifies the text that appears in the title. By default, the title bar displays Show-ObjectTree.")]
    [ValidateNotNullOrEmpty]
    public string? Title { get; set; }

    /// <summary>
    ///     Gets or sets the initial value for the filter in the GUI.
    /// </summary>
    [Parameter(HelpMessage =
        "Pre-populates the Filter edit box, allowing filtering to be specified on the command line. The filter uses regular expressions.")]
    public string? Filter { set; get; }

    /// <summary>
    ///     Gets or sets a value indicating whether "minimum UI" mode will be enabled.
    /// </summary>
    [Parameter(HelpMessage = "If specified no title or status bar will be displayed in the TUI. The filter will only be displayed if -Filter is specified.")]
    public SwitchParameter MinUI { set; get; }

    /// <summary>
    ///     Gets or sets the Terminal.Gui driver to use.
    /// </summary>
    [Parameter(HelpMessage =
        "Specifies the Terminal.Gui driver to use. Valid values are 'ansi', 'windows', or 'unix'. The default is `ansi`")]
    public string? Driver { set; get; }

    /// <summary>
    ///     Gets or sets a value indicating whether the application should run in full-screen mode
    ///     using the alternate screen buffer. By default, the application renders inline.
    /// </summary>
    [Parameter(HelpMessage =
        "If specified, the application runs in full-screen mode using the alternate screen buffer. By default, the application renders inline.")]
    public SwitchParameter FullScreen { set; get; }

    /// <summary>
    ///     Gets a value indicating whether the Debug switch is present.
    /// </summary>
    public bool Debug => MyInvocation.BoundParameters.ContainsKey("Debug");

    #endregion Input Parameters

    /// <summary>
    ///     Performs initialization of command execution. Validates that the environment supports object tree view.
    /// </summary>
    protected override void BeginProcessing()
    {
        if (Console.IsInputRedirected)
        {
            var error = new ErrorRecord(
                new PSNotSupportedException("Not supported in this environment (when input is redirected)."),
                ENVIRONMENT_NOT_SUPPORTED_FOR_SHOW_OBJECT_TREE,
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
                new FormatException("Invalid data type for Show-ObjectTree"),
                DATA_NOT_QUALIFIED_FOR_SHOW_OBJECT_TREE,
                ErrorCategory.InvalidType,
                null);

            ThrowTerminatingError(error);
        }

        _psObjects.Add(input);
    }

    /// <summary>
    ///     Performs final processing after all pipeline objects have been received.
    ///     Displays the object tree view with all collected objects.
    /// </summary>
    protected override void EndProcessing()
    {
        base.EndProcessing();

        // Return if no objects
        if (_psObjects.Count == 0) return;

        var applicationData = new ApplicationData
        {
            PSObjects = _psObjects.Cast<object>().ToList(),
            Title = Title ?? "Show-ObjectTree",
            Filter = Filter,
            MinUI = MinUI,
            Driver = Driver,
            FullScreen = FullScreen,
            Debug = Debug,
            ModuleVersion = MyInvocation.MyCommand.Version.ToString()
        };

        ShowObjectView.Run(applicationData);
    }

    /// <summary>
    ///     Releases all resources used by the <see cref="ShowObjectTreeCmdletCommand" />.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}