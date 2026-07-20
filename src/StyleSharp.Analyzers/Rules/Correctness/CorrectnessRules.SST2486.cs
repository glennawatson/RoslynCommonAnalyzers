// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2486 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2486 — an assembly is loaded through a path or partial-name API instead of Assembly.Load.</summary>
    public static readonly DiagnosticDescriptor PreferAssemblyLoad = Create(
        "SST2486",
        "Prefer Assembly.Load over the path and partial-name load APIs",
        "Assembly.{0} {1}; call Assembly.Load with a full assembly display name instead",
        PreferAssemblyLoadDescription);

    /// <summary>The PreferAssemblyLoad rule description.</summary>
    private const string PreferAssemblyLoadDescription =
        "System.Reflection.Assembly.LoadFrom, Assembly.LoadFile, and Assembly.LoadWithPartialName each load an assembly outside the "
        + "normal name-based binding context, which causes duplicate-identity bugs at runtime. LoadFrom uses a separate binding "
        + "context whose types are a different identity from the ones the default context loads, so a cast or equality check between "
        + "an object from LoadFrom and the same type loaded normally silently fails. LoadFile loads a file with no binding context at "
        + "all, so two loads of one assembly produce two non-unified identities. LoadWithPartialName resolves a partial name to "
        + "whichever installed assembly the runtime finds first, which is nondeterministic, and the API is deprecated. Prefer "
        + "Assembly.Load with a full assembly display name so the assembly resolves through the default context and unifies with "
        + "everything else already loaded. Only LoadWithPartialName has a mechanical fix — swapping the call to Assembly.Load with the "
        + "same name — because changing the load context of LoadFrom or LoadFile can alter behaviour and is left to the author.";
}
