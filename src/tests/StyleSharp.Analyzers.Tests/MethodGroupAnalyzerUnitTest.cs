// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyMethodGroup = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2239MethodGroupAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2239MethodGroupAnalyzer"/>.</summary>
public class MethodGroupAnalyzerUnitTest
{
    /// <summary>Verifies a lambda that only forwards its parameter is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ForwardingLambdaIsReportedAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Func<int, int> M() => {|SST2239:x => Square(x)|};

                private static int Square(int value) => value * value;
            }
            """);

    /// <summary>Verifies a lambda that changes the argument shape is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TransformingLambdaIsCleanAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Func<int, int> M() => x => Square(x + 1);

                private static int Square(int value) => value * value;
            }
            """);

    /// <summary>Verifies a lambda forwarding into an expanded params call is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpandedParamsCallIsCleanAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Action<int> M() => x => Log(x);

                private static void Log(params object[] args) { }
            }
            """);

    /// <summary>Verifies a lambda forwarding an array into a params call in normal form is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalFormParamsCallIsReportedAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Action<object[]> M() => {|SST2239:x => Log(x)|};

                private static void Log(params object[] args) { }
            }
            """);

    /// <summary>Verifies a lambda forwarding into an expanded params collection call is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpandedParamsCollectionCallIsCleanAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Action<object> M() => x => Log(x);

                private static void Log(params ReadOnlySpan<object> args) { }
            }
            """);

    /// <summary>Verifies a lambda that omits an optional parameter is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OmittedOptionalParameterIsCleanAsync()
        => await RunAsync(
            """
            using System;

            public sealed class C
            {
                public Action<int> M() => x => Log(x);

                private static void Log(int value, int extra = 0) { }
            }
            """);

    /// <summary>Verifies a forwarding lambda converted to an expression tree is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExpressionTreeLambdaIsCleanAsync()
        => await RunAsync(
            """
            using System;
            using System.Linq.Expressions;

            public sealed class C
            {
                public Expression<Func<int, int>> M() => x => Square(x);

                private static int Square(int value) => value * value;
            }
            """);

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task RunAsync(string source)
        => await new VerifyMethodGroup.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source
        }.RunAsync(CancellationToken.None);
}
