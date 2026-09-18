// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers.Benchmarks;

/// <summary>Supplies classes whose generic constraints affect whether a code fix can seal them.</summary>
internal static class GenericConstraintSealingBenchmarkSource
{
    /// <summary>Creates one complete compilation for a sealing decision.</summary>
    /// <param name="scenario">The location and shape of the target class's uses.</param>
    /// <returns>A source file containing one abstract class named Runner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Generate(string scenario) => scenario switch
    {
        "SelfConstraint" => """
            public abstract class Runner<T> where T : Runner<T>, new()
            {
                public int Value => 1;
            }
            """,
        "MethodConstraint" => """
            public abstract class Runner
            {
                public int Run<T>() where T : Runner => 1;
            }
            """,
        "ExternalConstraint" => """
            public abstract class Runner { public int Value => 1; }
            public class Consumer<T> where T : Runner { }
            """,
        "GenericArgument" => """
            public abstract class Runner { public int Value => 1; }
            public class Container<T> { }
            public class Consumer<T> where T : Container<Runner> { }
            """,
        "ProtectedMember" => """
            public abstract class Runner
            {
                protected Runner() { }
                public int Value => 1;
            }
            """,
        _ => "public abstract class Runner { public int Value => 1; }",
    };
}
