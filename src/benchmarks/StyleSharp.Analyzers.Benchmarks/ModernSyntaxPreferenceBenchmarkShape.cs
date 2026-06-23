// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Modern-syntax preference benchmark shapes.</summary>
public enum ModernSyntaxPreferenceBenchmarkShape
{
    /// <summary>Lambda with explicit parameter types.</summary>
    Lambda,

    /// <summary>Invocation argument lambda with explicit parameter types.</summary>
    InvocationLambda,

    /// <summary>Property accessors with block bodies.</summary>
    Accessor
}
