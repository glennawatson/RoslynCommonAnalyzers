// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Renders and reads the file header from the <c>file_header_template</c>
/// editorconfig option — the same key the .NET SDK's own file-header rule uses — so
/// StyleSharp enforces it as a normal analyzer, which runs by default.
/// The template's literal <c>\n</c> sequences separate lines and <c>{fileName}</c>
/// is substituted; each line renders as a <c>//</c> comment.
/// </summary>
internal static class FileHeaderHelper
{
    /// <summary>The editorconfig option key holding the header template.</summary>
    public const string TemplateKey = "file_header_template";

    /// <summary>The diagnostic property key carrying the rendered header for the code fix.</summary>
    public const string HeaderProperty = "Header";

    /// <summary>The placeholder replaced by the source file's name.</summary>
    private const string FileNamePlaceholder = "{fileName}";

    /// <summary>Path separator characters used to extract a file name without touching the file system.</summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>Reads the configured header template, treating <c>unset</c>/empty as "no header required".</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <param name="template">The configured template when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when a non-empty template is configured.</returns>
    internal static bool TryGetTemplate(AnalyzerConfigOptions options, out string template)
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
    internal static string Render(string template, string? filePath)
    {
        var builder = new StringBuilder(template.Length + "// ".Length);
        _ = builder.Append("//");
        var lineHasContent = false;
        var pendingBackslash = false;
        var start = 0;
        var fileName = FileName(filePath);
        while (template.IndexOf(FileNamePlaceholder, start, StringComparison.Ordinal) is var placeholder && placeholder >= 0)
        {
            AppendPart(builder, template.AsSpan(start, placeholder - start), ref lineHasContent, ref pendingBackslash);
            AppendPart(builder, fileName, ref lineHasContent, ref pendingBackslash);
            start = placeholder + FileNamePlaceholder.Length;
        }

        AppendPart(builder, template.AsSpan(start), ref lineHasContent, ref pendingBackslash);
        if (pendingBackslash)
        {
            AppendContent(builder, '\\', ref lineHasContent);
        }

        return builder.ToString();
    }

    /// <summary>Renders a template or filename slice, preserving separators that cross substitution boundaries.</summary>
    /// <param name="builder">The rendered header.</param>
    /// <param name="part">The next slice after filename substitution.</param>
    /// <param name="lineHasContent">Whether the current comment already has content after its prefix.</param>
    /// <param name="pendingBackslash">Whether the preceding slice or character ended with a possible separator.</param>
    private static void AppendPart(StringBuilder builder, ReadOnlySpan<char> part, ref bool lineHasContent, ref bool pendingBackslash)
    {
        foreach (var character in part)
        {
            if (pendingBackslash)
            {
                pendingBackslash = false;
                if (character == 'n')
                {
                    _ = builder.Append('\n').Append("//");
                    lineHasContent = false;
                    continue;
                }

                AppendContent(builder, '\\', ref lineHasContent);
            }

            if (character == '\\')
            {
                pendingBackslash = true;
            }
            else
            {
                AppendContent(builder, character, ref lineHasContent);
            }
        }
    }

    /// <summary>Appends a content character, separating the first character from the comment prefix.</summary>
    /// <param name="builder">The rendered header.</param>
    /// <param name="character">The content character.</param>
    /// <param name="lineHasContent">Whether the current comment already has content after its prefix.</param>
    private static void AppendContent(StringBuilder builder, char character, ref bool lineHasContent)
    {
        if (!lineHasContent)
        {
            _ = builder.Append(' ');
            lineHasContent = true;
        }

        _ = builder.Append(character);
    }

    /// <summary>Extracts the file name from a path without touching the file system.</summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The file name slice, or an empty span.</returns>
    private static ReadOnlySpan<char> FileName(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return default;
        }

        var index = filePath!.LastIndexOfAny(PathSeparators);
        return filePath.AsSpan(index + 1);
    }
}
