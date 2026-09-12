// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using VerifyUnion = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2338PreferUnionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the prefer-union rule (SST2338, opt-in).</summary>
public class Sst2338PreferUnionAnalyzerUnitTest
{
    /// <summary>A stand-in union marker, so the rule sees a runtime that supports unions.</summary>
    private const string Marker = """
                                  #nullable enable
                                  namespace System.Runtime.CompilerServices { public interface IUnion { } }

                                  """;

    /// <summary>Verifies a tag beside two differently typed payloads is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TagWithTwoPayloadsReportedAsync() =>
        RunAsync(Marker + """
            public enum PayloadKind
            {
                Text,
                Bytes,
            }

            public sealed class {|SST2338:Payload|}
            {
                public PayloadKind Kind { get; set; }

                public string? Text { get; set; }

                public byte[]? Bytes { get; set; }
            }
            """);

    /// <summary>Verifies a tag beside a single payload is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SinglePayloadIsCleanAsync() =>
        RunAsync(Marker + """
            public enum PayloadKind
            {
                Text,
            }

            public sealed class Payload
            {
                public PayloadKind Kind { get; set; }

                public string? Text { get; set; }
            }
            """);

    /// <summary>Verifies payloads with no discriminator are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoDiscriminatorIsCleanAsync() =>
        RunAsync(Marker + """
            public sealed class Payload
            {
                public string? Text { get; set; }

                public byte[]? Bytes { get; set; }
            }
            """);

    /// <summary>Verifies a type that is already a union is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ExistingUnionIsCleanAsync() =>
        RunAsync(Marker + """
            public enum PayloadKind
            {
                Text,
                Bytes,
            }

            public sealed class Payload : System.Runtime.CompilerServices.IUnion
            {
                public PayloadKind Kind { get; set; }

                public string? Text { get; set; }

                public byte[]? Bytes { get; set; }
            }
            """);

    /// <summary>Verifies nothing is reported when the runtime has no union support.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoMarkerIsCleanAsync() =>
        RunAsync("""
            #nullable enable
            public enum PayloadKind
            {
                Text,
                Bytes,
            }

            public sealed class Payload
            {
                public PayloadKind Kind { get; set; }

                public string? Text { get; set; }

                public byte[]? Bytes { get; set; }
            }
            """);

    /// <summary>Verifies a value-typed payload is not pushed toward a union, because the union form boxes it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ValueTypePayloadIsCleanAsync() =>
        RunAsync(Marker + """
            public enum ReadingKind
            {
                Count,
                Ratio,
            }

            public sealed class Reading
            {
                public ReadingKind Kind { get; set; }

                public int? Count { get; set; }

                public double? Ratio { get; set; }
            }
            """);

    /// <summary>Verifies one value-typed payload is enough to leave a mixed type alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MixedPayloadWithValueTypeIsCleanAsync() =>
        RunAsync(Marker + """
            public enum PayloadKind
            {
                Text,
                Count,
            }

            public sealed class Payload
            {
                public PayloadKind Kind { get; set; }

                public string? Text { get; set; }

                public int? Count { get; set; }
            }
            """);

    /// <summary>Verifies the boxing carve-out can be opted out of for a type where boxing does not matter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValueTypePayloadReportedWhenOptedInAsync()
    {
        var test = new VerifyUnion.Test
        {
            TestCode = Marker + """
                public enum ReadingKind
                {
                    Count,
                    Ratio,
                }

                public sealed class {|SST2338:Reading|}
                {
                    public ReadingKind Kind { get; set; }

                    public int? Count { get; set; }

                    public double? Ratio { get; set; }
                }
                """,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", """
            root = true
            [*.cs]
            stylesharp.SST2338.report_value_type_payloads = true

            """));
        test.SolutionTransforms.Add(static (solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(LanguageVersion.Preview));
        });

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer verifier at the requested language version.</summary>
    /// <param name="source">The source code, including diagnostic markup, to analyze.</param>
    /// <param name="languageVersion">The language version to parse with.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task RunAsync(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var test = new VerifyUnion.Test { TestCode = source };

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var parseOptions = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, parseOptions.WithLanguageVersion(languageVersion));
        });

        await test.RunAsync(CancellationToken.None);
    }
}
