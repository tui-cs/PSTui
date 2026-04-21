// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.PowerShell.OutGridView.Models;
using Xunit;

namespace Microsoft.PowerShell.ConsoleGuiTools.Tests;

public class SerializersTests
{
    [Fact]
    public void RoundTrip_String()
    {
        var original = "Hello, World!";
        var json = Serializers.ObjectToJson(original);
        var result = Serializers.ObjectFromJson<string>(json);
        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_Integer()
    {
        var original = 42;
        var json = Serializers.ObjectToJson(original);
        var result = Serializers.ObjectFromJson<int>(json);
        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_ComplexObject()
    {
        var original = new ApplicationData
        {
            Title = "Test",
            OutputMode = OutputModeOption.Multiple,
            Filter = "*.txt",
            MinUI = true
        };
        var json = Serializers.ObjectToJson(original);
        var result = Serializers.ObjectFromJson<ApplicationData>(json);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Title);
        Assert.Equal(OutputModeOption.Multiple, result.OutputMode);
        Assert.Equal("*.txt", result.Filter);
        Assert.True(result.MinUI);
    }

    [Fact]
    public void ObjectToJson_ReturnsBase64EncodedString()
    {
        var json = Serializers.ObjectToJson("test");
        // Should be valid base64
        var bytes = System.Convert.FromBase64String(json);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void RoundTrip_Null()
    {
        var json = Serializers.ObjectToJson<string?>(null);
        var result = Serializers.ObjectFromJson<string?>(json);
        Assert.Null(result);
    }
}
