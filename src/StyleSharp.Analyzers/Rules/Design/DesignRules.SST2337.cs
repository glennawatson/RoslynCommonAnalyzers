// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2337 descriptor.</summary>
internal static partial class DesignRules
{
    /// <summary>SST2337 — a declared parameter is never read by the member that declares it.</summary>
    public static readonly DiagnosticDescriptor UnreadParameter = Create(
        "SST2337",
        "Remove a parameter the body never reads",
        "Remove '{0}'; the body never reads it",
        UnreadParameterDescription);

    /// <summary>The UnreadParameter rule description.</summary>
    private const string UnreadParameterDescription =
        "A parameter that the body never mentions is a promise the member does not keep: every caller computes and passes a "
        + "value that is thrown away, and the next reader has to prove for themselves that it does not matter. It is usually "
        + "the residue of a change that removed the last use, and occasionally a bug where the wrong value is read instead. "
        + "Only a parameter whose name appears nowhere in the body is reported, so a use inside a lambda, a local function, or "
        + "a 'nameof' still counts. Signatures the author cannot change are left alone: an override, an interface "
        + "implementation, a partial or extern member, an attribute constructor, an event-handler or framework callback shape, "
        + "and a method handed on as a method group. Externally visible members are excluded by default, because dropping a "
        + "parameter there is a breaking change for every caller outside the assembly.";
}
