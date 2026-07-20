// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeTypedResults = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1500PreferTypedResultsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1500 (prefer <c>TypedResults</c> over <c>Results</c> in a minimal-API handler).</summary>
public class PreferTypedResultsAnalyzerUnitTest
{
    /// <summary>The inline stubs for the minimal-API result factories; the referenced framework does not carry them.</summary>
    private const string AspNetStubs = """

        namespace Microsoft.AspNetCore.Http
        {
            public interface IResult
            {
            }

            public static class Results
            {
                public static IResult Ok(object value) => throw new System.NotImplementedException();

                public static IResult NotFound() => throw new System.NotImplementedException();

                public static IResult Created(string uri, object value) => throw new System.NotImplementedException();

                public static IResult NoContent() => throw new System.NotImplementedException();

                public static IResult LegacyOnly() => throw new System.NotImplementedException();
            }

            public static class TypedResults
            {
                public static IResult Ok(object value) => throw new System.NotImplementedException();

                public static IResult NotFound() => throw new System.NotImplementedException();

                public static IResult Created(string uri, object value) => throw new System.NotImplementedException();

                public static IResult NoContent() => throw new System.NotImplementedException();
            }
        }
        """;

    /// <summary>Verifies a simple <c>Results.Ok(...)</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResultsOkReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handlers
            {
                public IResult Get() => {|PSH1500:Results.Ok(new object())|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a parameterless <c>Results.NotFound()</c> call is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResultsNotFoundReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handlers
            {
                public IResult Get() => {|PSH1500:Results.NotFound()|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies a fully qualified <c>Results.X(...)</c> call is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FullyQualifiedResultsReportedAsync()
        => await VerifyAsync(
            """
            public class Handlers
            {
                public Microsoft.AspNetCore.Http.IResult Get()
                    => {|PSH1500:Microsoft.AspNetCore.Http.Results.Created("/x", new object())|};
            }
            """ + AspNetStubs);

    /// <summary>Verifies the strongly typed <c>TypedResults.X(...)</c> call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypedResultsIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handlers
            {
                public IResult Get() => TypedResults.Ok(new object());
            }
            """ + AspNetStubs);

    /// <summary>Verifies a same-named factory in the user's own namespace is not reported (containing-type identity, not name text).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedResultsTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public class Handlers
            {
                public object Get() => MyApp.Results.Ok(new object());
            }

            namespace MyApp
            {
                public static class Results
                {
                    public static object Ok(object value) => value;
                }
            }
            """ + AspNetStubs);

    /// <summary>Verifies a <c>Results</c> member with no matching <c>TypedResults</c> member is not reported (the suggestion must be actionable).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResultsMemberWithoutTypedCounterpartIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handlers
            {
                public IResult Get() => Results.LegacyOnly();
            }
            """ + AspNetStubs);

    /// <summary>Verifies the rule stays silent when <c>TypedResults</c> is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenTypedResultsAbsentAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http;

            public class Handlers
            {
                public IResult Get() => Results.Ok(new object());
            }

            namespace Microsoft.AspNetCore.Http
            {
                public interface IResult
                {
                }

                public static class Results
                {
                    public static IResult Ok(object value) => throw new System.NotImplementedException();
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeTypedResults.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
