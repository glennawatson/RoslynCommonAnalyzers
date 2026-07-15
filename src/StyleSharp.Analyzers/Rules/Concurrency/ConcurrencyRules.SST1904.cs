// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST1904 descriptor.</summary>
internal static partial class ConcurrencyRules
{
    /// <summary>SST1904 — a <c>lock</c> targets a non-readonly field of the declaring type.</summary>
    public static readonly DiagnosticDescriptor DoNotLockOnNonReadonlyField = Create(
        "SST1904",
        "Do not lock on a non-readonly field",
        "Do not lock on '{0}', which is not readonly; another assignment swaps the lock out from under callers already inside it",
        DoNotLockOnNonReadonlyFieldDescription);

    /// <summary>The DoNotLockOnNonReadonlyField rule description.</summary>
    private const string DoNotLockOnNonReadonlyFieldDescription =
        "A lock only coordinates callers that take the same instance. When the lock target is a field that is not readonly, any "
        + "assignment to that field replaces the instance, and a caller that enters the lock afterwards is holding a different "
        + "object than the one already inside — so both run at once and the protected state is corrupted. Declare the field "
        + "'readonly' so the instance is fixed for the life of the type. On .NET 9 and later, C# 13 and up, a dedicated lock "
        + "field is better declared as a 'System.Threading.Lock' the compiler enters through a typed scope; PSH1300 makes that "
        + "change once the field is readonly.";
}
