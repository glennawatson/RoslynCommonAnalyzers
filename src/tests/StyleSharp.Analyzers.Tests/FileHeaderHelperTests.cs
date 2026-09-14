// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests header template substitution and escaped line separators.</summary>
public class FileHeaderHelperTests
{
    /// <summary>Checks disabled templates and configured content retain their documented meanings.</summary>
    /// <param name="value">The configured value, or null when absent.</param>
    /// <param name="expected">Whether a header is required.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(null, false)]
    [Arguments("", false)]
    [Arguments("UnSeT", false)]
    [Arguments("Copyright", true)]
    public async Task TemplateAvailabilityAsync(string? value, bool expected)
    {
        var actual = FileHeaderHelper.TryGetTemplate(new HeaderOptions(value), out var template);
        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(template).IsEqualTo(expected ? value : string.Empty);
    }

    /// <summary>Checks filenames, blank lines, and literal backslashes render without losing content.</summary>
    /// <param name="template">The template text.</param>
    /// <param name="path">The source path.</param>
    /// <param name="expected">The rendered comment block.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("", null, "//")]
    [Arguments("{fileName}", "", "//")]
    [Arguments("{fileName}", "Name.cs", "// Name.cs")]
    [Arguments("{fileName} / {fileName}", "/src/Name.cs", "// Name.cs / Name.cs")]
    [Arguments("{fileName}", "C:\\src\\Name.cs", "// Name.cs")]
    [Arguments("first\\n\\nlast\\n", null, "// first\n//\n// last\n//")]
    [Arguments("path\\value\\", null, "// path\\value\\")]
    [Arguments("\\{fileName}", "/src/name.cs", "//\n// ame.cs")]
    [Arguments("\\{fileName}n", null, "//\n//")]
    [Arguments("\\{fileName}", "/src/file.cs", "// \\file.cs")]
    public async Task RenderPreservesTemplateMeaningAsync(string template, string? path, string expected)
    {
        var actual = FileHeaderHelper.Render(template, path);
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>Provides one optional header setting.</summary>
    /// <param name="configuredValue">The configured value.</param>
    private sealed class HeaderOptions(string? configuredValue) : AnalyzerConfigOptions
    {
        /// <inheritdoc/>
        public override bool TryGetValue(string key, out string value)
        {
            value = configuredValue ?? string.Empty;
            return key == FileHeaderHelper.TemplateKey && configuredValue is not null;
        }
    }
}
