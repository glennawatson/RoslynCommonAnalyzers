// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyTest = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2508IncompleteAssertionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2508 (a fluent assertion that is started but never completed).</summary>
public class IncompleteAssertionAnalyzerUnitTest
{
    /// <summary>
    /// Minimal FluentAssertions stubs: the <c>AssertionExtensions</c> host of the <c>Should()</c> extensions
    /// and subject types both directly under <c>FluentAssertions</c> and under a nested namespace.
    /// </summary>
    private const string FluentAssertionsStubs = """
        namespace FluentAssertions
        {
            public static class AssertionExtensions
            {
                public static FluentAssertions.Numeric.NumericAssertions Should(this int value) => new FluentAssertions.Numeric.NumericAssertions();
                public static FluentAssertions.Primitives.StringAssertions Should(this string value) => new FluentAssertions.Primitives.StringAssertions();
                public static FluentAssertions.BooleanAssertions Should(this bool value) => new FluentAssertions.BooleanAssertions();
            }

            public class BooleanAssertions
            {
                public BooleanAssertions Be(bool expected) => this;
            }
        }
        namespace FluentAssertions.Numeric
        {
            public class NumericAssertions
            {
                public NumericAssertions Be(int expected) => this;
            }
        }
        namespace FluentAssertions.Primitives
        {
            public class StringAssertions
            {
                public StringAssertions Be(string expected) => this;
            }
        }
        """;

    /// <summary>Minimal AwesomeAssertions stubs mirroring the FluentAssertions shape under its own namespace.</summary>
    private const string AwesomeAssertionsStubs = """
        namespace AwesomeAssertions
        {
            public static class AssertionExtensions
            {
                public static AwesomeAssertions.Numeric.NumericAssertions Should(this int value) => new AwesomeAssertions.Numeric.NumericAssertions();
            }
        }
        namespace AwesomeAssertions.Numeric
        {
            public class NumericAssertions
            {
                public NumericAssertions Be(int expected) => this;
            }
        }
        """;

    /// <summary>Verifies a bare <c>Should()</c> on a subject whose assertions type is nested under the library namespace is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FluentBareShouldOnNestedSubjectIsReportedAsync()
        => await VerifyAsync("""
            using FluentAssertions;

            """ + FluentAssertionsStubs + """

            public class Tests
            {
                public void Case(int value)
                {
                    {|SST2508:value.Should()|};
                }
            }
            """);

    /// <summary>Verifies a bare <c>Should()</c> whose assertions type sits directly in the library namespace is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FluentBareShouldOnRootNamespaceSubjectIsReportedAsync()
        => await VerifyAsync("""
            using FluentAssertions;

            """ + FluentAssertionsStubs + """

            public class Tests
            {
                public void Case(bool value)
                {
                    {|SST2508:value.Should()|};
                }
            }
            """);

    /// <summary>Verifies a completed fluent assertion that chains a check is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CompletedFluentAssertionIsCleanAsync()
        => await VerifyAsync("""
            using FluentAssertions;

            """ + FluentAssertionsStubs + """

            public class Tests
            {
                public void Case(string value)
                {
                    value.Should().Be("expected");
                }
            }
            """);

    /// <summary>Verifies a bare <c>Should()</c> from the AwesomeAssertions namespace is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwesomeAssertionsBareShouldIsReportedAsync()
        => await VerifyAsync("""
            using AwesomeAssertions;

            """ + AwesomeAssertionsStubs + """

            public class Tests
            {
                public void Case(int value)
                {
                    {|SST2508:value.Should()|};
                }
            }
            """);

    /// <summary>Verifies an unrelated <c>Should()</c> whose result type is in the global namespace is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedShouldReturningGlobalTypeIsCleanAsync()
        => await VerifyAsync(FluentAssertionsStubs + """

            public class Marker { }

            public static class MarkerExtensions
            {
                public static Marker Should(this object value) => new Marker();
            }

            public class Tests
            {
                public void Case(object value)
                {
                    value.Should();
                }
            }
            """);

    /// <summary>Verifies an unrelated <c>Should()</c> whose result type is in a non-library namespace is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedShouldReturningOtherNamespaceTypeIsCleanAsync()
        => await VerifyAsync("""
            using Contoso;

            """ + FluentAssertionsStubs + """

            namespace Contoso
            {
                public class Widget { }

                public static class WidgetExtensions
                {
                    public static Contoso.Widget Should(this object value) => new Contoso.Widget();
                }
            }

            public class Tests
            {
                public void Case(object value)
                {
                    value.Should();
                }
            }
            """);

    /// <summary>Verifies a <c>Should()</c> on a dynamic subject, which binds to no method symbol, is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DynamicShouldIsCleanAsync()
        => await VerifyAsync(FluentAssertionsStubs + """

            public class Tests
            {
                public void Case(dynamic value)
                {
                    value.Should();
                }
            }
            """);

    /// <summary>Verifies an expression statement that is not an invocation is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonInvocationStatementIsCleanAsync()
        => await VerifyAsync(FluentAssertionsStubs + """

            public class Tests
            {
                public void Case(int value)
                {
                    value = value + 1;
                }
            }
            """);

    /// <summary>Verifies an invocation with no receiver (a plain method call) is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReceiverlessInvocationStatementIsCleanAsync()
        => await VerifyAsync(FluentAssertionsStubs + """

            public class Tests
            {
                public void Helper() { }

                public void Case()
                {
                    Helper();
                }
            }
            """);

    /// <summary>Verifies a bare <c>Should()</c> is never reported when no fluent-assertion library is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NoFluentLibraryReferencedIsCleanAsync()
        => await VerifyAsync(
            """
            public class Subject { }

            public static class SubjectExtensions
            {
                public static Subject Should(this object value) => new Subject();
            }

            public class Tests
            {
                public void Case(object value)
                {
                    value.Should();
                }
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies with the source's own library stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyTest.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
