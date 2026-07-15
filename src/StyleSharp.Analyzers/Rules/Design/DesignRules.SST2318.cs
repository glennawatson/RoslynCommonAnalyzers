// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2318 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2318 — two methods in one type have token-identical, non-trivial bodies.</summary>
    public static readonly DiagnosticDescriptor DuplicateMemberBody = DescriptorFactory.CreateOptIn(
        "SST2318",
        "Members should not have identical bodies",
        "'{0}' has the same body as '{1}'; if that is intended, call one from the other, otherwise this is a copy that was meant to differ",
        "Design",
        DuplicateMemberBodyDescription);

    /// <summary>The DuplicateMemberBody rule description.</summary>
    private const string DuplicateMemberBodyDescription =
        "Two methods declared in the same type have exactly the same body, token for token. That is almost always a copy-paste where the "
        + "second method was meant to do something different and never got changed, so it silently does the first one's work. If the shared "
        + "body is intentional, call one method from the other so the logic lives in one place; otherwise the duplicate hides a bug. Off by "
        + "default: identical bodies are sometimes legitimate, so this reports only as a deliberate opt-in.";
}
