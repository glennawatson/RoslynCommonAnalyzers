// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1108 readability descriptor.</summary>
internal static partial class ReadabilityRules
{
    /// <summary>SST1108 — a source file consists of commented-out C# code (opt-in).</summary>
    public static readonly DiagnosticDescriptor EntireFileCommentedOut = CreateOptIn(
        "SST1108",
        "Remove files that contain only commented-out code",
        "Remove this commented-out source file; source control preserves old code",
        "A source file whose meaningful content is commented-out C# code has no live implementation. "
        + "Off by default; comments that document code remain valid when the file also contains live declarations.");
}
