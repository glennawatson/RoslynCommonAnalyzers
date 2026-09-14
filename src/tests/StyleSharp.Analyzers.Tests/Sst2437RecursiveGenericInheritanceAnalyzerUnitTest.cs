// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;
using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2437RecursiveGenericInheritanceAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2437 (a generic type nested inside its own base type arguments).</summary>
public class Sst2437RecursiveGenericInheritanceAnalyzerUnitTest
{
    /// <summary>Verifies qualified and aliased references are scanned at both nesting levels.</summary>
    /// <param name="baseType">The base type containing a recursive reference.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Suite.Base<Suite.Recursive<Suite.Recursive<T>>>")]
    [Arguments("Alias::Base<Alias::Recursive<Alias::Recursive<T>>>")]
    [Arguments("Base<Recursive<Wrapper<Recursive<T>>>>")]
    [Arguments("Base<Recursive<Wrapper<int, Recursive<T>>>>")]
    public Task QualifiedAndWrappedRecursiveReferencesAreReportedAsync(string baseType) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            using Alias = Suite;
            namespace Suite
            {
                public class Base<T> { }
                public class Wrapper<T> { }
                public class Wrapper<T, U> { }
                public class {|SST2437:Recursive|}<T> : {{baseType}} { }
            }
            """);

    /// <summary>Verifies the scan continues through nonrecursive bases and earlier type arguments.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LaterBaseAndLaterTypeArgumentAreReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public interface IMarker { }
            public interface IBase<T, U> { }
            public class {|SST2437:Recursive|}<T, U> : IMarker, IBase<int, Recursive<int, Recursive<T, U>>> { }
            """);

    /// <summary>Verifies matching names with different generic arities do not count as self references.</summary>
    /// <param name="baseType">The base type whose same-named references have different arity.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Base<Recursive<Recursive<T>, int>>")]
    [Arguments("Base<Recursive<Recursive<T, int>>>")]
    public Task DifferentGenericArityIsCleanAsync(string baseType) =>
        Verify.VerifyAnalyzerAsync(
            $$"""
            public class Base<T> { }
            public class Recursive<T, U> { }
            public class Recursive<T> : {{baseType}} { }
            """);

    /// <summary>Verifies recursive interface arguments are detected for every registered declaration kind.</summary>
    /// <param name="declarationKind">The type declaration syntax.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("struct")]
    [Arguments("record")]
    [Arguments("record struct")]
    public Task RecursiveInterfaceArgumentsAreReportedAsync(string declarationKind) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                public interface IBase<T> { }
                public {{declarationKind}} {|SST2437:Recursive|}<T> : IBase<Recursive<Recursive<T>>> { }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies arrays and tuples remain opaque to the current syntactic recursion check.</summary>
    /// <param name="baseType">The base type containing an opaque syntax shape.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("Base<Recursive<Recursive<T>[]>>")]
    [Arguments("Base<Recursive<Recursive<T>>[]>")]
    [Arguments("Base<Recursive<(Recursive<T>, int)>>")]
    public Task ArrayAndTupleWrappedReferencesAreCurrentlyCleanAsync(string baseType) =>
        new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $$"""
                public class Base<T> { }
                public class Recursive<T> : {{baseType}} { }
                """,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies incomplete generic arguments and missing base types can be scanned safely.</summary>
    /// <param name="baseType">The incomplete base-list syntax.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("")]
    [Arguments("Base<>")]
    [Arguments("Base<Recursive<>>")]
    public Task IncompleteBaseListsAreCleanAsync(string baseType) =>
        new Verify.Test { CompilerDiagnostics = CompilerDiagnostics.None, TestCode = $$"""public class Recursive<T> : {{baseType}} { }""" }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a type nested inside its own base's arguments is reported and does not crash the walk.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RecursiveGenericBaseIsReportedAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CuriouslyRecurringBaseIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonGenericSelfReferenceIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypeNotNestedInsideOwnArgumentsIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlainGenericTypeIsCleanAsync() =>
        Verify.VerifyAnalyzerAsync(
            """
            public class Container<T>
            {
                public T Value { get; set; }
            }
            """);
}
