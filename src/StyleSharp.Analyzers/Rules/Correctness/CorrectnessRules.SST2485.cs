// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The SST2485 descriptor.</summary>
internal static partial class CorrectnessRules
{
    /// <summary>SST2485 — a <c>throw new NotImplementedException</c> is left in shipped code.</summary>
    public static readonly DiagnosticDescriptor NotImplementedExceptionThrown = Create(
        "SST2485",
        "A NotImplementedException should not be left in shipped code",
        "'throw new NotImplementedException' marks unfinished code that crashes at runtime when reached; complete the member or remove the stub",
        NotImplementedExceptionThrownDescription);

    /// <summary>The NotImplementedExceptionThrown rule description.</summary>
    private const string NotImplementedExceptionThrownDescription =
        "A 'throw new NotImplementedException' is a placeholder the compiler accepts and the runtime does not: the member "
        + "builds, but any path that reaches the throw fails at runtime. Left in a shipped build it turns a stubbed-out "
        + "member into a crash waiting for the first caller. Finish the member, or delete it until it is ready. A "
        + "'throw new NotSupportedException' is a different thing and is never reported — it is a deliberate, permanent "
        + "statement that an operation is genuinely unsupported, not an unfinished stub.";
}
