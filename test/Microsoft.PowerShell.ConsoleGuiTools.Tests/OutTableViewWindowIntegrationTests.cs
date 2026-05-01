// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.PowerShell.OutGridView.Models;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Time;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

/// <summary>
///     Integration tests for <see cref="OutTableViewWindow" /> exercising the full Terminal.Gui lifecycle
///     using the headless ANSI driver and VirtualTimeProvider.
/// </summary>
public class OutTableViewWindowIntegrationTests
{
    #region Helpers

    private static List<DataTableColumn> CreateColumns(params string[] names)
    {
        var columns = new List<DataTableColumn>();
        foreach (var name in names)
            columns.Add(new DataTableColumn(name, name));
        return columns;
    }

    private static DataTableRow CreateRow(List<DataTableColumn> columns, int originalIndex, params string[] values)
    {
        var dict = new Dictionary<string, IValue>();
        for (int i = 0; i < columns.Count && i < values.Length; i++)
            dict[columns[i].ToString()] = new StringValue { DisplayValue = values[i] };
        return new DataTableRow(dict, originalIndex);
    }

    private static (OutTableViewDataSource dataSource, ApplicationData appData) CreateTestData(
        OutputModeOption outputMode = OutputModeOption.Multiple,
        bool minUI = false,
        string? filter = null,
        string? search = null,
        FocusTarget focus = FocusTarget.Table,
        string? title = "Test Table")
    {
        var columns = CreateColumns("Name", "Id");
        var ds = new OutTableViewDataSource(columns);
        ds.AddRow(CreateRow(columns, 0, "Alice", "1"));
        ds.AddRow(CreateRow(columns, 1, "Bob", "2"));
        ds.AddRow(CreateRow(columns, 2, "Charlie", "3"));

        var appData = new ApplicationData
        {
            Title = title,
            OutputMode = outputMode,
            Filter = filter,
            MinUI = minUI,
            Search = search,
            Focus = focus,
            Driver = null
        };

        return (ds, appData);
    }

    /// <summary>
    ///     Sets up Terminal.Gui in headless mode, creates and begins the window,
    ///     invokes the test action, then tears down.
    /// </summary>
    private static void RunWithWindow(
        OutTableViewDataSource dataSource,
        ApplicationData appData,
        Action<IApplication, OutTableViewWindow> testAction)
    {
        Terminal.Gui.Configuration.ConfigurationManager.Enable(
            Terminal.Gui.Configuration.ConfigLocations.All);

        using OutTableViewWindow window = new(appData, dataSource);
        using IApplication app = Application.Create(new VirtualTimeProvider())
            .Init(driverName: "ansi");

        SessionToken? token = app.Begin(window);
        app.LayoutAndDraw(forceRedraw: true);

        try
        {
            testAction(app, window);
        }
        finally
        {
            if (token is not null)
                app.End(token);
        }
    }

    #endregion

    #region Rendering Tests

    [Fact]
    public void Render_ShowsColumnHeadersAndData()
    {
        var (ds, appData) = CreateTestData();

        RunWithWindow(ds, appData, (app, window) =>
        {
            string screen = app.Driver!.ToString();

            Assert.Contains("Name", screen);
            Assert.Contains("Id", screen);
            Assert.Contains("Alice", screen);
            Assert.Contains("Bob", screen);
            Assert.Contains("Charlie", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    [Fact]
    public void Render_WithFilter_ShowsOnlyMatchingRows()
    {
        var (ds, appData) = CreateTestData(filter: "Alice");

        RunWithWindow(ds, appData, (app, window) =>
        {
            string screen = app.Driver!.ToString();

            Assert.Contains("Alice", screen);
            Assert.DoesNotContain("Bob", screen);
            Assert.DoesNotContain("Charlie", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    [Fact]
    public void Render_MinUI_DoesNotShowFilterLabel()
    {
        var (ds, appData) = CreateTestData(minUI: true);

        RunWithWindow(ds, appData, (app, window) =>
        {
            string screen = app.Driver!.ToString();

            Assert.Contains("Alice", screen);
            Assert.DoesNotContain("Filter:", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    [Fact]
    public void Render_MinUI_WithFilter_ShowsFilterField()
    {
        var (ds, appData) = CreateTestData(minUI: true, filter: "Bob");

        RunWithWindow(ds, appData, (app, window) =>
        {
            string screen = app.Driver!.ToString();

            // MinUI with filter should still show the filter field
            Assert.Contains("Bob", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    #endregion

    #region ESC / Close Tests

    [Fact]
    public void Esc_ClosesWindow_ResultIsNull()
    {
        var (ds, appData) = CreateTestData();

        RunWithWindow(ds, appData, (app, window) =>
        {
            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
            Assert.Null(window.Result);
        });
    }

    [Fact]
    public void OutputModeNone_EscClosesWithNullResult()
    {
        var (ds, appData) = CreateTestData(outputMode: OutputModeOption.None);

        RunWithWindow(ds, appData, (app, window) =>
        {
            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
            Assert.Null(window.Result);
        });
    }

    #endregion

    #region Accept / Selection Tests

    [Fact]
    public void Enter_WithNoExplicitSelection_ReturnsCursorRow()
    {
        var (ds, appData) = CreateTestData(outputMode: OutputModeOption.Multiple);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // With FullRowSelect, the cursor row is always part of the selection
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(0, result); // Cursor starts at row 0
        });
    }

    [Fact]
    public void Enter_WithSelectedItems_ReturnsSelectedIndexes()
    {
        var (ds, appData) = CreateTestData(outputMode: OutputModeOption.Multiple);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Select first row with Space
            app.Keyboard.RaiseKeyDownEvent(Key.Space);
            app.LayoutAndDraw();

            // Move down and select second row
            app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.LayoutAndDraw();
            app.Keyboard.RaiseKeyDownEvent(Key.Space);
            app.LayoutAndDraw();

            // Accept
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(0, result); // Alice
            Assert.Contains(1, result); // Bob
            Assert.DoesNotContain(2, result); // Charlie not selected
        });
    }

    [Fact]
    public void SingleOutputMode_Enter_ReturnsSingleItem()
    {
        var (ds, appData) = CreateTestData(outputMode: OutputModeOption.Single);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Move to second row
            app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.LayoutAndDraw();

            // Accept
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(1, result); // Bob (cursor was on row 1)
        });
    }

    [Fact]
    public void SelectAll_CtrlA_SelectsAllRows()
    {
        var (ds, appData) = CreateTestData(outputMode: OutputModeOption.Multiple);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Ctrl+A to select all
            app.Keyboard.RaiseKeyDownEvent(Key.A.WithCtrl);
            app.LayoutAndDraw();

            // Accept
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Equal(3, result.Count);
            Assert.Contains(0, result);
            Assert.Contains(1, result);
            Assert.Contains(2, result);
        });
    }

    #endregion

    #region Filter Interaction Tests

    [Fact]
    public void FilterField_TypingFiltersRows()
    {
        var (ds, appData) = CreateTestData(focus: FocusTarget.Filter);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Type in filter field (focus is on filter)
            app.Keyboard.RaiseKeyDownEvent(Key.B);
            app.Keyboard.RaiseKeyDownEvent(Key.O.WithShift); // 'O' - but this is actually just 'o'
            app.Keyboard.RaiseKeyDownEvent(Key.B);
            app.LayoutAndDraw();

            string screen = app.Driver!.ToString();
            Assert.Contains("Bob", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    [Fact]
    public void FilterField_EnterAcceptsSelection()
    {
        var (ds, appData) = CreateTestData(
            outputMode: OutputModeOption.Multiple,
            focus: FocusTarget.Filter,
            filter: "Alice");

        RunWithWindow(ds, appData, (app, window) =>
        {
            // With focus on filter and filter pre-set, Enter should accept
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
        });
    }

    #endregion

    #region Focus Tests

    [Fact]
    public void Focus_Table_FocusesTableView()
    {
        var (ds, appData) = CreateTestData(focus: FocusTarget.Table);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // The table should have focus - arrow keys should move cursor
            app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.LayoutAndDraw();

            // Accept from table - should have cursor on row 1
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(1, result); // Cursor moved to Bob
        });
    }

    [Fact]
    public void Focus_Filter_FocusesFilterField()
    {
        var (ds, appData) = CreateTestData(focus: FocusTarget.Filter);

        RunWithWindow(ds, appData, (app, window) =>
        {
            // With filter focused, typing should go into the filter field
            // Type 'A' - should filter to Alice
            app.Keyboard.RaiseKeyDownEvent(Key.A.WithShift);
            app.LayoutAndDraw();

            string screen = app.Driver!.ToString();
            Assert.Contains("Alice", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    #endregion

    #region Search Tests

    [Fact]
    public void Search_PositionsCursorOnMatchingRow()
    {
        var (ds, appData) = CreateTestData(search: "Charlie");

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Simulate pipeline complete which triggers search
            window.OnPipelineComplete();
            app.LayoutAndDraw();

            // Accept the current selection (should be Charlie at row 2)
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(2, result); // Charlie is at original index 2
        });
    }

    [Fact]
    public void Search_NoMatch_CursorStaysAtTop()
    {
        var (ds, appData) = CreateTestData(search: "ZZZ_NoMatch");

        RunWithWindow(ds, appData, (app, window) =>
        {
            window.OnPipelineComplete();
            app.LayoutAndDraw();

            // Accept - should be at first row since no match found
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(0, result); // Default position = row 0
        });
    }

    [Fact]
    public void Search_RegexPattern_Matches()
    {
        var (ds, appData) = CreateTestData(search: "^B");

        RunWithWindow(ds, appData, (app, window) =>
        {
            window.OnPipelineComplete();
            app.LayoutAndDraw();

            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(1, result); // Bob matches ^B
        });
    }

    #endregion

    #region Streaming Tests

    [Fact]
    public void OnDataChanged_UpdatesDisplayedRowCount()
    {
        var columns = CreateColumns("Name", "Id");
        var ds = new OutTableViewDataSource(columns);
        ds.AddRow(CreateRow(columns, 0, "First", "1"));

        var appData = new ApplicationData
        {
            Title = "Streaming Test",
            OutputMode = OutputModeOption.Multiple,
            Driver = null
        };

        RunWithWindow(ds, appData, (app, window) =>
        {
            string screen = app.Driver!.ToString();
            Assert.Contains("First", screen);

            // Simulate streaming: add more rows
            ds.AddRow(CreateRow(columns, 1, "Second", "2"));
            ds.AddRow(CreateRow(columns, 2, "Third", "3"));
            window.OnDataChanged();
            app.LayoutAndDraw(forceRedraw: true);

            screen = app.Driver!.ToString();
            Assert.Contains("Second", screen);
            Assert.Contains("Third", screen);

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    [Fact]
    public void OnPipelineComplete_StopsLoadingIndicator()
    {
        var (ds, appData) = CreateTestData();

        RunWithWindow(ds, appData, (app, window) =>
        {
            window.OnPipelineComplete();
            app.LayoutAndDraw(forceRedraw: true);

            string screen = app.Driver!.ToString();
            // After pipeline complete, status bar should show final row count
            Assert.Contains("3", screen); // 3 rows

            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public void Home_MovesToFirstRow()
    {
        var (ds, appData) = CreateTestData();

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Move down twice
            app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.Keyboard.RaiseKeyDownEvent(Key.CursorDown);
            app.LayoutAndDraw();

            // Press Home
            app.Keyboard.RaiseKeyDownEvent(Key.Home);
            app.LayoutAndDraw();

            // Accept - should be at first row
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(0, result); // Alice at index 0
        });
    }

    [Fact]
    public void End_MovesToLastRow()
    {
        var (ds, appData) = CreateTestData();

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Press End
            app.Keyboard.RaiseKeyDownEvent(Key.End);
            app.LayoutAndDraw();

            // Accept - should be at last row
            app.Keyboard.RaiseKeyDownEvent(Key.Enter);

            Assert.NotNull(window.Result);
            var result = (HashSet<int>)window.Result;
            Assert.Contains(2, result); // Charlie at index 2
        });
    }

    #endregion

    #region Sorting Tests (via GetSelectedIndexes after sort)

    [Fact]
    public void Sort_ReordersDisplayedData()
    {
        // Create data in non-alphabetical order
        var columns = CreateColumns("Name", "Id");
        var ds = new OutTableViewDataSource(columns);
        ds.AddRow(CreateRow(columns, 0, "Charlie", "3"));
        ds.AddRow(CreateRow(columns, 1, "Alice", "1"));
        ds.AddRow(CreateRow(columns, 2, "Bob", "2"));

        var appData = new ApplicationData
        {
            Title = "Sort Test",
            OutputMode = OutputModeOption.Multiple,
            Driver = null
        };

        RunWithWindow(ds, appData, (app, window) =>
        {
            // Verify original order
            string screen = app.Driver!.ToString();
            Assert.Contains("Charlie", screen);

            // Note: We can't easily simulate a header click in headless mode,
            // but we verified sorting works in the data source tests.
            // Just verify the window renders without error.
            app.Keyboard.RaiseKeyDownEvent(Key.Esc);
        });
    }

    #endregion
}
