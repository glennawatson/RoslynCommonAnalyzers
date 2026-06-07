// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Renders and reads the file header from the <c>file_header_template</c>
/// editorconfig option — the same key the .NET SDK's IDE0073 uses — so StyleSharp
/// enforces it as a normal analyzer (which runs by default, unlike the IDE rule).
/// The template's literal <c>\n</c> sequences separate lines and <c>{fileName}</c>
/// is substituted; each line renders as a <c>//</c> comment.
/// </summary>
internal static class FileHeaderHelper
{
    /// <summary>The editorconfig option key holding the header template.</summary>
    public const string TemplateKey = "file_header_template";

    /// <summary>The diagnostic property key carrying the rendered header for the code fix.</summary>
    public const string HeaderProperty = "Header";

    /// <summary>The literal <c>\n</c> sequence the template uses to separate header lines.</summary>
    private static readonly string[] LineSeparators = ["\\n"];

    /// <summary>Path separator characters used to extract a file name without touching the file system.</summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>Reads the configured header template, treating <c>unset</c>/empty as "no header required".</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="template">The configured template when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a non-empty template is configured.</returns>
    public static bool TryGetTemplate(AnalyzerConfigOptions options, out string template)
    {
        template = string.Empty;
        if (!options.TryGetValue(TemplateKey, out var value)
            || value.Length == 0
            || string.Equals(value, "unset", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        template = value;
        return true;
    }

    /// <summary>Renders the template into the expected header comment block (lines joined by <c>\n</c>).</summary>
    /// <param name="template">The configured template.</param>
    /// <param name="filePath">The source file path (for <c>{fileName}</c> substitution).</param>
    /// <returns>The rendered <c>//</c> comment block.</returns>
    public static string Render(string template, string? filePath)
    {
        var lines = template.Replace("{fileName}", FileName(filePath)).Split(LineSeparators, StringSplitOptions.None);

        var builder = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append("//");
            if (lines[i].Length > 0)
            {
                builder.Append(' ').Append(lines[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Extracts the file name from a path without touching the file system.</summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The file name, or an empty string.</returns>
    private static string FileName(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return string.Empty;
        }

        var index = filePath!.LastIndexOfAny(PathSeparators);
        return index >= 0 ? filePath[(index + 1)..] : filePath;
    }
}
