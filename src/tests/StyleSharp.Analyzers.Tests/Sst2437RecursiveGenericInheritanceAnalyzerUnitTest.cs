// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2437RecursiveGenericInheritanceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2437 (a generic type nested inside its own base type arguments).</summary>
public class Sst2437RecursiveGenericInheritanceAnalyzerUnitTest
{
    /// <summary>Verifies a type nested inside its own base's arguments is reported and does not crash the walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecursiveGenericBaseIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base<T>
            {
            }

            public class {|SST2437:Recursive|}<T> : Base<Recursive<Recursive<T>>>
            {
            }
            """);

    /// <summary>Verifies the curiously-recurring self-reference (depth one) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CuriouslyRecurringBaseIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base<T>
            {
            }

            public class Fluent<T> : Base<Fluent<T>>
            {
            }
            """);

    /// <summary>Verifies a non-generic self-referential base is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGenericSelfReferenceIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class BaseNode<T>
            {
            }

            public class Node : BaseNode<Node>
            {
            }
            """);

    /// <summary>Verifies the type appearing deep in another generic, but not inside its own arguments, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeNotNestedInsideOwnArgumentsIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Base<T>
            {
            }

            public class Wrapper<T>
            {
            }

            public class Safe<T> : Base<Wrapper<Safe<T>>>
            {
            }
            """);

    /// <summary>Verifies a plain generic type with no base list is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainGenericTypeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class Container<T>
            {
                public T Value { get; set; }
            }
            """);
}
