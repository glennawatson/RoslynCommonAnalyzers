// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace RoslynCommon.Analyzers.Tests;

/// <summary>
/// Settles the newline a verified source is written and compared with. Fix and refactoring cleanup takes
/// its newline from <c>end_of_line</c> and falls back to the host's when nothing configures it, so without
/// a pin the same fix emits CRLF on Windows and LF elsewhere while the expected source is LF either way.
/// Pinning also lets a second pass re-run the same case as CRLF, proving a fix honours the edited file's
/// own line endings instead of hard-coding one form.
/// </summary>
internal static class TestSourceLineEndings
{
    /// <summary>Where the newline config goes, beside the sources and clear of each test's own "/.editorconfig".</summary>
    internal const string NestedEditorConfigPath = "/0/.editorconfig";

    /// <summary>The config pinning LF, matching the line endings the expected sources are written with.</summary>
    internal const string LineFeedConfig = "[*]\nend_of_line = lf\n";

    /// <summary>The config pinning CRLF, as a repo that stores CRLF would.</summary>
    internal const string CarriageReturnLineFeedConfig = "[*]\nend_of_line = crlf\n";

    /// <summary>Returns whether any source carries a line break the CRLF variant could exercise.</summary>
    /// <param name="sources">The state's source list.</param>
    /// <returns><see langword="true"/> when a line break exists.</returns>
    internal static bool AnySourceHasLineBreak(SourceFileList sources)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i].content.ToString().Contains('\n', StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Rewrites every source in a state to CRLF line endings.</summary>
    /// <param name="sources">The state's source list.</param>
    internal static void ConvertSourcesToCrlf(SourceFileList sources)
    {
        for (var i = 0; i < sources.Count; i++)
        {
            var (name, content) = sources[i];
            var text = content.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
            sources[i] = (name, SourceText.From(text, content.Encoding));
        }
    }

    /// <summary>Adds the nested config to one state, replacing the content set by an earlier pass.</summary>
    /// <param name="configs">The state's analyzer config files.</param>
    /// <param name="config">The analyzer config content.</param>
    internal static void SetNestedEditorConfig(SourceFileCollection configs, string config)
    {
        for (var i = 0; i < configs.Count; i++)
        {
            var (filename, content) = configs[i];
            if (!string.Equals(filename, NestedEditorConfigPath, StringComparison.Ordinal))
            {
                continue;
            }

            configs[i] = (filename, SourceText.From(config, content.Encoding));
            return;
        }

        configs.Add((NestedEditorConfigPath, config));
    }
}
