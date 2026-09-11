// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;

using VerifyNestedPropertyPattern = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2238NestedPropertyPatternAnalyzer>;
using VerifyNestedPropertyPatternFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2238NestedPropertyPatternAnalyzer,
    StyleSharp.Analyzers.Sst2238NestedPropertyPatternCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2238NestedPropertyPatternAnalyzer"/>.</summary>
public class NestedPropertyPatternAnalyzerUnitTest
{
    /// <summary>Verifies a nested property-only pattern is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NestedPropertyPatternIsReportedAsync() =>
        RunAsync(
            """
            public sealed class Person
            {
                public Address Address { get; set; } = new();
            }

            public sealed class Address
            {
                public string City { get; set; } = "";
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Address: {|SST2238:{ City: "Melbourne" }|} };
            }
            """);

    /// <summary>Verifies declaration patterns are clean because flattening would change the pattern shape.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeclarationPatternIsCleanAsync() =>
        RunAsync(
            """
            public sealed class Person
            {
                public object Value { get; set; } = new();
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Value: string text };
            }
            """);

    /// <summary>Verifies a typed nested pattern is clean, because the path form drops the type test.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TypedNestedPatternIsCleanAsync() =>
        RunAsync(
            """
            public sealed class Person
            {
                public object Address { get; set; } = new();
            }

            public sealed class Address
            {
                public string City { get; set; } = "";
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Address: Address { City: "Melbourne" } };
            }
            """);

    /// <summary>Verifies a nested clause holding two subpatterns is clean, since one path cannot carry both.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MultipleNestedSubpatternsAreCleanAsync() =>
        RunAsync(
            """
            public sealed class Person
            {
                public Address Address { get; set; } = new();
            }

            public sealed class Address
            {
                public string City { get; set; } = "";

                public string Country { get; set; } = "";
            }

            public sealed class C
            {
                public bool M(Person person) => person is { Address: { City: "Melbourne", Country: "AU" } };
            }
            """);

    /// <summary>Verifies the fix folds the nested pattern into a property path.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NestedPatternIsFlattenedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public Address Address { get; set; } = new();
                              }

                              public sealed class Address
                              {
                                  public string City { get; set; } = "";
                              }

                              public sealed class C
                              {
                                  public bool M(Person person) => person is { Address: {|SST2238:{ City: "Melbourne" }|} };
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                       public Address Address { get; set; } = new();
                                   }

                                   public sealed class Address
                                   {
                                       public string City { get; set; } = "";
                                   }

                                   public sealed class C
                                   {
                                       public bool M(Person person) => person is { Address.City: "Melbourne" };
                                   }
                                   """;
        await new VerifyNestedPropertyPatternFix.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = Source, FixedCode = FixedSource, }.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier with modern reference assemblies.</summary>
    /// <param name="source">The source code to analyze.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task RunAsync(string source) =>
        new VerifyNestedPropertyPattern.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net80, TestCode = source }.RunAsync(CancellationToken.None);
}
