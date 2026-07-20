// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1402 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1402 — an assembly is loaded from raw bytes or a non-constant location.</summary>
    public static readonly DiagnosticDescriptor UnsafeAssemblyLoad = Create(
        "SES1402",
        "Do not load an assembly from raw bytes or a non-constant location",
        "'{0}' loads an assembly whose code cannot be verified before it runs with full trust; load assemblies only from a trusted, fixed location",
        Serialization,
        UnsafeAssemblyLoadDescription);

    /// <summary>The SES1402 rule description.</summary>
    private const string UnsafeAssemblyLoadDescription =
        "Loading an assembly executes its module initializers and makes every type in it callable, all with the full trust of the "
        + "loading process. 'Assembly.Load(byte[])' and 'AssemblyLoadContext.LoadFromStream' take the assembly as an in-memory "
        + "buffer, so whatever produced those bytes -- a download, a deserialized blob, a database column -- decides what code "
        + "runs; there is no signature or location to anchor trust to. 'Assembly.LoadFrom', 'Assembly.LoadFile', and "
        + "'Assembly.UnsafeLoadFrom' with a non-constant path let a caller-controlled string decide which file is executed, so a "
        + "planted or swapped file runs as your process. Load only assemblies you ship, referenced normally or from a fixed, "
        + "trusted path, and verify a strong-name or Authenticode signature before loading anything sourced at run time.";
}
