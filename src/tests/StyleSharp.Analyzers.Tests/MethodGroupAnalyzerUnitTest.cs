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

    /// <summary>Verifies a lambda that throws away a return value the delegate cannot hold is not reported.</summary>
    /// <remarks>
    /// A lambda body may discard a result; a method group may not. Offering the method group for
    /// <c>error =&gt; source.TrySetException(error)</c> against an <c>Action&lt;Exception&gt;</c> would hand
    /// the reader CS0407, because <c>TrySetException</c> returns <see langword="bool"/>.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LambdaDiscardingAReturnValueIsNotReportedAsync()
        => await RunAsync(
            """
            using System;
            using System.Threading.Tasks;

            public class C
            {
                public Action<Exception> M(TaskCompletionSource<int> completion)
                    => error => completion.TrySetException(error);
            }
            """);

    /// <summary>Verifies a lambda whose argument must widen on the way through is not reported.</summary>
    /// <remarks>
    /// A method group may only take an identity or implicit reference conversion on each parameter.
    /// <c>List&lt;object&gt;.Add</c> bound to an <c>Action&lt;int&gt;</c> needs <c>int</c> to box, so the
    /// method group is CS0123 and the lambda is the only form that compiles.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LambdaWhoseArgumentMustBoxIsNotReportedAsync()
        => await RunAsync(
            """
            using System;
            using System.Collections.Generic;

            public class C
            {
                public Action<int> M(List<object> values) => value => values.Add(value);
            }
            """);

    /// <summary>Verifies a lambda whose method group would make the enclosing call ambiguous is not reported.</summary>
    /// <remarks>
    /// A lambda states its own shape, and that shape can be what picks the overload around it. A method
    /// group carries every overload of its name, so where more than one of them fits the enclosing call,
    /// the rewrite is CS0121 and the lambda is the only form that compiles.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LambdaWhoseMethodGroupWouldBeAmbiguousIsNotReportedAsync()
        => await RunAsync(
            """
            using System;

            public class Matcher
            {
                public bool IsMatch(string value) => true;

                public bool IsMatch(string value, int start) => true;
            }

            public static class Pipe
            {
                public static void Where(Func<string, bool> predicate)
                {
                }

                public static void Where(Func<string, int, bool> predicate)
                {
                }
            }

            public class C
            {
                public void M(Matcher matcher) => Pipe.Where(value => matcher.IsMatch(value));
            }
            """);

    /// <summary>Verifies a forwarding lambda reached through a conditional access is left alone.</summary>
    /// <remarks>
    /// Detaching the invocation to rebind the method group speculatively orphans the conditional-access
    /// binding and crashes the binder, so the rule stays silent on the <c>receiver?.M(...)</c> form.
    /// </remarks>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConditionalAccessForwardingLambdaIsLeftAloneAsync()
        => await RunAsync(
            """
            using System.Collections.Generic;
            using System.Linq;

            public sealed class C
            {
                private static bool Filter(string value) => true;

                public void Use(List<string> items)
                {
                    var result = items?.Where(x => Filter(x));
                }
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
