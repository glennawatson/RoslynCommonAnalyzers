// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The bounds of a cref's last name segment, and the last identifier token read inside it.</summary>
/// <param name="Start">The segment's start, moved past each qualifier.</param>
/// <param name="End">The segment's end, cut at the first generic or parameter suffix.</param>
internal record struct CrefNameScan(int Start, int End)
{
    /// <summary>Gets or sets the last identifier token read.</summary>
    public SyntaxToken Name { get; set; }
}
