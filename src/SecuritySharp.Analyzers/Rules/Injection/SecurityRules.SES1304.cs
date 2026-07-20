// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1304 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1304 — an archive entry name is joined into a filesystem path with no containment check.</summary>
    public static readonly DiagnosticDescriptor ArchiveEntryPathTraversal = Create(
        "SES1304",
        "An archive entry name must not build a write path without a containment check",
        ArchiveEntryPathTraversalMessage,
        Injection,
        ArchiveEntryPathTraversalDescription);

    /// <summary>The SES1304 rule message format.</summary>
    private const string ArchiveEntryPathTraversalMessage =
        "The '{0}' entry name is joined into the destination path and passed straight to a file-writing call "
        + "with no containment check; a crafted '../' or absolute entry name escapes the target directory (path traversal)";

    /// <summary>The SES1304 rule description.</summary>
    private const string ArchiveEntryPathTraversalDescription =
        "Writing an archive entry to a path built from the entry's own name lets a crafted entry -- a name containing '../' "
        + "segments, or a rooted/absolute path -- write outside the directory you intended, overwriting arbitrary files. This "
        + "is the 'zip slip' path-traversal class. The rule reports the local, inline shape where an entry name (a "
        + "'System.IO.Compression.ZipArchiveEntry.FullName' or a 'System.Formats.Tar.TarEntry.Name') is combined with "
        + "'System.IO.Path.Combine' or string concatenation and passed directly as the destination of an extraction or "
        + "file-writing call, so the join and the write sit in one expression with no room for a guard. Before writing, resolve "
        + "the combined path with 'Path.GetFullPath' and confirm it starts with the fully-qualified destination root followed "
        + "by a directory separator, rejecting the entry otherwise. The rule stays silent unless one of the archive-entry "
        + "types resolves in the compilation, so a project that cannot reference them pays nothing.";
}
