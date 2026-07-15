// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2422 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2422 — a property's getter reads a different field than its setter writes.</summary>
    public static readonly DiagnosticDescriptor BackingFieldMismatch = Create(
        "SST2422",
        "A property's getter and setter should use the same field",
        "The getter reads '{0}' but the setter writes '{1}'; the property does not round-trip",
        BackingFieldMismatchDescription);

    /// <summary>The BackingFieldMismatch rule description.</summary>
    private const string BackingFieldMismatchDescription =
        "A property's getter returns one instance field and its setter assigns 'value' to a different one. What is written can never be "
        + "read back through the property, and what is read is whatever some other code last put in the getter's field. This is a "
        + "proof, not a guess about names: the two accessors resolve to two different field symbols. The setter may do more than "
        + "assign — validate, raise a change notification — and the getter may be anything that reduces to a single field read; the "
        + "rule reports only when exactly one field is assigned from 'value' and exactly one, different, field is returned.";
}
