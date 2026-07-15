// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>One placeholder found in a message template, described by offsets into the template's value text.</summary>
/// <param name="Kind">What the placeholder resolves to.</param>
/// <param name="ValueStart">The offset of the opening brace in the value text.</param>
/// <param name="ValueEnd">The offset just past the closing brace in the value text.</param>
/// <param name="NameStart">The offset of the name's first character in the value text.</param>
/// <param name="NameEnd">The offset just past the name's last character in the value text.</param>
/// <remarks>
/// Offsets are kept rather than substrings so the scan allocates nothing: a name is materialized only when a
/// rule reports and needs it for a message. Comparisons run directly over the offset ranges.
/// </remarks>
internal readonly record struct LogPlaceholder(LogPlaceholderKind Kind, int ValueStart, int ValueEnd, int NameStart, int NameEnd)
{
    /// <summary>Gets the length of the name, in characters.</summary>
    public int NameLength => NameEnd - NameStart;
}
