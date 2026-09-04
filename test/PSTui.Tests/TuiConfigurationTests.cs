// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using Terminal.Gui.Configuration;
using Xunit;

namespace PSTui.Tests;

/// <summary>
///     Tests for the Terminal.Gui 2.5 configuration migration (TuiConfigurationBuilder replacing
///     ConfigurationManager.Enable(ConfigLocations.All)). These pin the behavior the cmdlets rely on:
///     app-scoped ./.tui/&lt;cmdlet&gt;.config.json files must be discovered relative to the current
///     directory (as Terminal.Gui 2.4.x did via relative-path File IO), not the host's install
///     directory — the entry assembly (and AppContext.BaseDirectory) is pwsh, not this module.
/// </summary>
// Claude - Fable 5
public class TuiConfigurationTests
{
    /// <summary>
    ///     Creates a temp directory containing .tui/&lt;fileName&gt; with the given JSON content.
    /// </summary>
    private static string CreateConfigDir(string fileName, string json)
    {
        string dir = Directory.CreateTempSubdirectory("pstui-cfg-").FullName;
        Directory.CreateDirectory(Path.Combine(dir, ".tui"));
        File.WriteAllText(Path.Combine(dir, ".tui", fileName), json);
        return dir;
    }

    [Fact]
    public void AppCurrentConfig_IsDiscovered_RelativeToCurrentDirectory()
    {
        string dir = CreateConfigDir("Out-ConsoleGridView.config.json", "{ \"Theme\": \"PSTuiCwdProbe\" }");
        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            // Same call OutConsoleGridView.StartUi makes. Only reads Configuration —
            // deliberately does not ApplyToStaticFacades, to avoid mutating process-wide state.
            var builder = new TuiConfigurationBuilder("Out-ConsoleGridView");

            Assert.Equal("PSTuiCwdProbe", builder.Configuration["Theme"]);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AppCurrentConfig_IsDiscovered_FromExplicitCurrentDirectory()
    {
        // The exact call the cmdlets make: pwsh does not sync the process working
        // directory with Set-Location, so the cmdlets pass the session's current
        // filesystem location as the builder's currentDirectory.
        string dir = CreateConfigDir("Out-ConsoleGridView.config.json", "{ \"Theme\": \"PSTuiExplicitDirProbe\" }");
        try
        {
            var builder = new TuiConfigurationBuilder("Out-ConsoleGridView", dir);

            Assert.Equal("PSTuiExplicitDirProbe", builder.Configuration["Theme"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void MalformedAppCurrentConfig_DoesNotThrow_FallsBackToDefaults()
    {
        // A user's broken config file must never crash the cmdlet (or pwsh).
        string dir = CreateConfigDir("Out-ConsoleGridView.config.json", "{ this is not json !!!");
        try
        {
            var builder = new TuiConfigurationBuilder("Out-ConsoleGridView", dir);

            // Building must not throw; the malformed source is skipped.
            Assert.Null(builder.Configuration["Theme"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GlobalCurrentConfig_IsDiscovered_RelativeToCurrentDirectory()
    {
        string dir = CreateConfigDir("config.json", "{ \"Theme\": \"PSTuiGlobalCwdProbe\" }");
        string originalCwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = dir;

            var builder = new TuiConfigurationBuilder("Out-ConsoleGridView");

            Assert.Equal("PSTuiGlobalCwdProbe", builder.Configuration["Theme"]);
        }
        finally
        {
            Environment.CurrentDirectory = originalCwd;
            Directory.Delete(dir, recursive: true);
        }
    }
}
