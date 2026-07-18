// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2449 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2449 — an event or delegate is unsubscribed with a delegate created on the spot.</summary>
    public static readonly DiagnosticDescriptor LambdaUnsubscription = Create(
        "SST2449",
        "Unsubscribing with a new lambda removes nothing",
        "This lambda is a new delegate, so '-=' finds no match in '{0}' and the earlier handler stays attached",
        LambdaUnsubscriptionDescription);

    /// <summary>The LambdaUnsubscription rule description.</summary>
    private const string LambdaUnsubscriptionDescription =
        "Removing a handler with '-=' compares delegates for equality, and a lambda or anonymous method creates a brand-new delegate "
        + "every time it is evaluated — even one whose text matches the subscription character for character. The subtraction finds no "
        + "match and removes nothing: the original handler keeps firing, and it keeps its target alive. Store the delegate in a field "
        + "or local when it is added and pass that same reference to '-=', or subscribe with a method group, which compares equal by "
        + "method and target.";
}
