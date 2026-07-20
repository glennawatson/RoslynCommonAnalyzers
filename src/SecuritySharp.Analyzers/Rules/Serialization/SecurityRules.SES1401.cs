// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1401 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1401 — a type resolved from non-constant data is instantiated or deserialized into.</summary>
    public static readonly DiagnosticDescriptor NonConstantTypeActivation = Create(
        "SES1401",
        "A type resolved from non-constant data must not be instantiated or used as a deserialization target",
        "'{0}' builds its target type with 'Type.GetType' from non-constant data; an attacker who controls the type name can construct an unexpected or dangerous type",
        Serialization,
        NonConstantTypeActivationDescription);

    /// <summary>The SES1401 rule description.</summary>
    private const string NonConstantTypeActivationDescription =
        "'Type.GetType(name)' loads whatever type the string names, so resolving it from data an attacker can influence lets them "
        + "pick the type that gets built. Feeding that resolved type straight into 'Activator.CreateInstance' or a 'Deserialize' "
        + "call runs the chosen type's construction and deserialization logic -- a gadget that touches the file system, opens a "
        + "connection, or starts a process on load turns a crafted type name into code execution. Resolve the type from a fixed "
        + "allow-list of expected type names, or bind a constant type, instead of instantiating whatever 'Type.GetType' returns. "
        + "The rule reports only the inline shape 'Type.GetType(nonConstant)' passed directly as the type argument; a type first "
        + "stored in a local is out of scope because confirming it safely would require data-flow tracking.";
}
