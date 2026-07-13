// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1416CacheSerializerOptionsAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1416CacheSerializerOptionsAnalyzer"/> (PSH1416 cached serializer options).</summary>
public class CacheSerializerOptionsAnalyzerUnitTest
{
    /// <summary>Verifies options built inside a method are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionsBuiltInMethodAreReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;

            public class C
            {
                public string M(object value)
                {
                    var options = {|PSH1416:new JsonSerializerOptions { WriteIndented = true }|};
                    return JsonSerializer.Serialize(value, options);
                }
            }
            """);

    /// <summary>Verifies target-typed options built inside a method are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedOptionsInMethodAreReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;

            public class C
            {
                public string M(object value)
                {
                    JsonSerializerOptions options = {|PSH1416:new() { WriteIndented = true }|};
                    return JsonSerializer.Serialize(value, options);
                }
            }
            """);

    /// <summary>Verifies options rebuilt on every read of an expression-bodied property are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionsInExpressionBodiedPropertyAreReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;

            public class C
            {
                private static JsonSerializerOptions Options => {|PSH1416:new JsonSerializerOptions { WriteIndented = true }|};
            }
            """);

    /// <summary>Verifies the cached static readonly field the rule steers toward is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CachedStaticReadonlyFieldIsNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;

            public class C
            {
                private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

                public string M(object value) => JsonSerializer.Serialize(value, Options);
            }
            """);

    /// <summary>Verifies options built in a constructor are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionsBuiltInConstructorAreNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;

            public class C
            {
                private readonly JsonSerializerOptions _options;

                public C() => _options = new JsonSerializerOptions { WriteIndented = true };
            }
            """);

    /// <summary>Verifies options shaped by a parameter are not reported.</summary>
    /// <remarks>
    /// The suggestion is a <c>static readonly</c> field, and these options cannot live in one: a shared
    /// instance would hand every caller the first caller's resolver. They are built per call because they
    /// are built per caller, and the rule has nothing to offer.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionsBuiltAroundAParameterAreNotReportedAsync()
        => await VerifyAsync(
            """
            using System.Text.Json;
            using System.Text.Json.Serialization.Metadata;

            public static class C
            {
                public static JsonSerializerOptions Make(IJsonTypeInfoResolver resolver) =>
                    new() { TypeInfoResolver = resolver };
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
