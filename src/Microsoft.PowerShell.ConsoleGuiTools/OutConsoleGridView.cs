// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Threading;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

namespace Microsoft.PowerShell.ConsoleGuiTools;

/// <summary>
///     Provides the main orchestration for Out-ConsoleGridView, managing the Terminal.Gui
///     application lifecycle. Supports both batch (all objects upfront) and streaming
///     (objects arrive incrementally from the pipeline) modes.
/// </summary>
internal sealed class OutConsoleGridView : IDisposable
{
    private readonly List<PSObject> _psObjects = [];
    private readonly TypeGetter _typeGetter = new();
    private readonly ManualResetEventSlim _uiRunning = new(false);
    private IApplication? _app;
    private ApplicationData? _applicationData;
    private List<DataTableColumn>? _columns;
    private OutGridViewDataSource? _dataSource;
    private int _objectIndex;
    private int _pendingNotify; // 0 = no pending UI update, 1 = scheduled
    private HashSet<int>? _result;
    private Thread? _uiThread;
    private OutGridViewWindow? _window;

    public void Dispose()
    {
        _uiRunning.Dispose();
    }

    /// <summary>
    ///     Initializes the streaming session. Call this once before feeding objects.
    /// </summary>
    public void Initialize(ApplicationData applicationData)
    {
        _applicationData = applicationData;
    }

    /// <summary>
    ///     Adds an object from the pipeline. On the first object, starts the UI on a background thread.
    /// </summary>
    public void AddObject(PSObject psObject)
    {
        _psObjects.Add(psObject);

        if (_columns == null)
        {
            // First object: determine columns, add first row, then start UI
            _columns = _typeGetter.GetDataColumnsForObject(psObject);
            _dataSource = new OutGridViewDataSource(_columns);
            var row = TypeGetter.CastObjectToDataTableRow(psObject, _columns, _objectIndex++);
            _dataSource.AddRow(row);
            StartUi();
            _uiRunning.Wait();
        }
        else
        {
            var row = TypeGetter.CastObjectToDataTableRow(psObject, _columns, _objectIndex++);
            _dataSource!.AddRow(row);
        }

        // Coalesce UI notifications: only schedule one Invoke at a time.
        // When it fires it picks up ALL rows added since the last notification.
        if (Interlocked.CompareExchange(ref _pendingNotify, 1, 0) == 0)
            _app?.Invoke(() =>
            {
                Interlocked.Exchange(ref _pendingNotify, 0);
                _window?.OnDataChanged();
            });
    }

    /// <summary>
    ///     Signals that the pipeline is complete and waits for the UI to finish.
    ///     Returns the selected indexes.
    /// </summary>
    public HashSet<int> Complete()
    {
        if (_window == null)
            return [];

        _app?.Invoke(() => _window.OnPipelineComplete());
        _uiThread?.Join();
        return _result ?? [];
    }

    /// <summary>
    ///     Gets the PSObject at the given original index (for output).
    /// </summary>
    public PSObject GetObject(int index)
    {
        return _psObjects[index];
    }

    private void StartUi()
    {
        _uiThread = new Thread(() =>
        {
            ConfigurationManager.AppName = "Out-ConsoleGridView";
            ConfigurationManager.Enable(ConfigLocations.All);

            _window = new OutGridViewWindow(_applicationData!, _dataSource!)
            {
                OnRunning = () => _uiRunning.Set()
            };
            _app = Application.Create();
            _app.AppModel = _applicationData!.FullScreen ? AppModel.FullScreen : AppModel.Inline;
            _app.Init(_applicationData.Driver);
            _result = _app.Run(_window) as HashSet<int>;
            _window.Dispose();
            _app.Dispose();
        })
        {
            IsBackground = true,
            Name = "OutConsoleGridView-UI"
        };
        _uiThread.Start();
    }
}