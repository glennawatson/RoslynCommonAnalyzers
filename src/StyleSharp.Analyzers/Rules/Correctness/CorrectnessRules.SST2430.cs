// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2430 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2430 — a serialization callback whose signature stops the serializer from ever calling it.</summary>
    public static readonly DiagnosticDescriptor SerializationCallbackSignature = Create(
        "SST2430",
        "A serialization callback has the wrong signature",
        "'{0}' will never run as a serialization callback; it must be a non-generic instance method returning void with a single StreamingContext parameter",
        SerializationCallbackSignatureDescription);

    /// <summary>The SerializationCallbackSignature rule description.</summary>
    private const string SerializationCallbackSignatureDescription =
        "The serializer only invokes a method marked with a serialization callback attribute when that method has the exact shape it looks "
        + "for: a non-generic, non-static method that returns void and takes a single StreamingContext parameter. A method that deviates in "
        + "any of those — static, generic, non-void, or a different parameter list — is simply skipped at runtime, so the state it was meant "
        + "to fix up or the resource it was meant to reattach is silently left undone. Correct the signature so the callback runs.";
}
