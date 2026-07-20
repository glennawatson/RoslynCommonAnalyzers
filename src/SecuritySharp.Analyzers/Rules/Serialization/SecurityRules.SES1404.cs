// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1404 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1404 — an Activator by-name overload instantiates a type whose name comes from non-constant data.</summary>
    public static readonly DiagnosticDescriptor NonConstantActivatorTypeName = Create(
        "SES1404",
        "A type must not be instantiated by name from non-constant data",
        "'{0}' instantiates the type named by this non-constant argument; an attacker who controls the type name can construct an unexpected or dangerous type",
        Serialization,
        NonConstantActivatorTypeNameDescription);

    /// <summary>The SES1404 rule description.</summary>
    private const string NonConstantActivatorTypeNameDescription =
        "The string overloads of 'Activator.CreateInstance(assemblyName, typeName, ...)' and "
        + "'Activator.CreateInstanceFrom(assemblyFile, typeName, ...)' load and construct whatever type the 'typeName' string "
        + "names. Building that name from data an attacker can influence lets them pick the type that gets created, and merely "
        + "running a hostile type's constructor -- before any method is called -- can touch the file system, open a connection, "
        + "or start a process, turning a crafted type name into code execution. Resolve the type from a fixed allow-list of "
        + "expected type names, or pass a constant type name, instead of instantiating whatever the incoming string names. "
        + "The rule reports only the direct shape where a non-constant expression is passed as the 'typeName' argument of these "
        + "by-name overloads; a name first stored in a local is out of scope because confirming it safely would require "
        + "data-flow tracking.";
}
