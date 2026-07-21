// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The SES1406 descriptor.</summary>
internal static partial class SecurityRules
{
    /// <summary>SES1406 — reflection reaches a non-public member, bypassing the accessibility the type declared.</summary>
    public static readonly DiagnosticDescriptor NonPublicReflection = CreateOptIn(
        "SES1406",
        "Reflection must not reach non-public members to bypass their declared accessibility",
        NonPublicReflectionMessage,
        Serialization,
        NonPublicReflectionDescription);

    /// <summary>The SES1406 message format.</summary>
    private const string NonPublicReflectionMessage =
        "'{0}' looks up members with 'BindingFlags.NonPublic'; reaching a private or internal member through reflection defeats "
        + "the accessibility its author chose and couples this code to internals that can change without notice";

    /// <summary>The SES1406 rule description.</summary>
    private const string NonPublicReflectionDescription =
        "A member is declared private or internal so that only its own type or assembly may touch it -- that boundary is what lets the "
        + "author change, rename, or delete it safely, and it is often the only thing keeping a field holding a key, a token, or an "
        + "invariant out of reach. Passing 'BindingFlags.NonPublic' to 'Type.GetMethod', 'GetField', 'GetProperty', 'InvokeMember', or "
        + "any of their sibling lookups reaches straight past that boundary and binds to whatever hidden member the name resolves to, so "
        + "code can read a private secret, mutate an internal invariant, or call a method the author never meant to expose. It also "
        + "silently welds this code to another type's internals: the reflected member is not part of any contract, so a routine refactor "
        + "of the target breaks the caller at run time with no compiler warning. Prefer a public API, an interface, or an "
        + "'[InternalsVisibleTo]' contract that the target type deliberately offers; if a private member truly must be reached (a test "
        + "seam, a serializer over a type you do not own), isolate that reflection and treat the coupling as a known cost. This rule is "
        + "off by default: reflection over non-public members is legitimate in some tools and frameworks, so it reports only as a "
        + "deliberate opt-in. It flags a lookup only when the 'BindingFlags' argument is a compile-time constant that includes the "
        + "'NonPublic' bit; a flags value assembled at run time is out of scope because confirming it would require data-flow tracking.";
}
