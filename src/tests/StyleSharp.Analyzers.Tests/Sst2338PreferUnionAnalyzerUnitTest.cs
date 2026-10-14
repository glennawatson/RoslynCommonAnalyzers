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

    /// <summary>Verifies coexisting required metadata and nonnullable collections are not alternatives.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task RequiredMetadataIsCleanAsync() => RunAsync(Marker + """
        public enum OperationType { Create, Update }
        public sealed record Operation
        {
            public required OperationType Type { get; init; }
            public required string? Identity { get; init; }
            public required byte[] Payload { get; init; }
            public string? Version { get; init; }
            public System.Collections.Generic.IReadOnlyDictionary<string, string> Metadata { get; init; }
                = new System.Collections.Generic.Dictionary<string, string>();
        }
        """);

    /// <summary>Verifies storage and computed projections do not inflate the optional payload count.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ComputedProjectionIsCleanAsync() => RunAsync(Marker + """
        public enum PayloadKind { Text, Bytes }
        public sealed class Payload
        {
            private readonly byte[] _bytes = new byte[0];
            public PayloadKind Kind { get; init; }
            public string? Text { get; init; }
            public byte[]? Bytes => _bytes;
            public object? Value { get { return Text; } }
        }
        """);

    /// <summary>Verifies constructor-assigned readonly and auto-property alternatives remain reportable.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ReadonlyAlternativesReportedAsync() => RunAsync(Marker + """
        public enum PayloadKind { Text, Bytes }
        public sealed class {|SST2338:Payload|}
        {
            public required PayloadKind Kind { get; init; }
            public required object Metadata { get; init; }
            public readonly string? Text;
            public byte[]? Bytes { get; }
            public Payload(string? text, byte[]? bytes) { Text = text; Bytes = bytes; }
        }
        """);

    /// <summary>Verifies nullable-oblivious source retains its existing alternative detection.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ObliviousAlternativesReportedAsync() => RunAsync(Marker + """
        #nullable disable
        public enum PayloadKind { Text, Bytes }
        public sealed class {|SST2338:Payload|}
        {
            public PayloadKind Kind { get; set; }
            public string Text { get; set; }
            public byte[] Bytes { get; set; }
        }
        """);

    /// <summary>Verifies CRDT collection state coexists while input alternatives remain reportable.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task CollectionStateAndInputAlternativesAsync() => RunAsync(Marker + """
        public enum StateKind { Counter, Set, Register }
        public enum InputKind { Mutation, State }
        public sealed record State
        {
            private readonly byte[] _bytes = new byte[0];
            public required StateKind Kind { get; init; }
            public System.Collections.Generic.IReadOnlyDictionary<string, long> Components
            {
                get;
                init => field = value;
            } = new System.Collections.Generic.Dictionary<string, long>();
            public System.Collections.Generic.IReadOnlyList<string> Elements { get; init; } = new string[0];
            public System.Collections.Generic.IReadOnlyList<string> RemovedElements { get; init; } = new string[0];
            public System.ReadOnlyMemory<byte> Bytes { get => _bytes; init => _bytes = value.ToArray(); }
            public object? Stamp { get; init; }
            public object Value => Components;
        }
        public sealed record Mutation
        {
            private readonly byte[] _bytes = new byte[0];
            public required StateKind Kind { get; init; }
            public string? Actor { get; init; }
            public System.Collections.Generic.IReadOnlyList<string> Dots { get; init; } = new string[0];
            public byte[] Bytes { get => _bytes; init => _bytes = value; }
        }
        public sealed record {|SST2338:Input|}
        {
            public required InputKind Kind { get; init; }
            public Mutation? Mutation { get; init; }
            public State? State { get; init; }
        }
        """);

    /// <summary>Verifies duplicate reference types do not form two alternatives.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task DuplicateTypesAreCleanAsync() => RunAsync(Marker + """
        public enum PayloadKind { First, Second }
        public sealed class Payload
        {
            public PayloadKind Kind { get; init; }
            public string? First { get; init; }
            public string? Second { get; init; }
        }
        """);

    /// <summary>Verifies the rule cannot suggest C# 15 syntax in an earlier language.</summary>
    /// <returns>The asynchronous test operation.</returns>
    [Test]
    public async Task EarlierLanguageIsCleanAsync()
    {
        const string Source = Marker + """
        public enum PayloadKind { Text, Bytes }
        public sealed class Payload
        {
            public PayloadKind Kind { get; init; }
            public string? Text { get; init; }
            public byte[]? Bytes { get; init; }
        }
        """;
        await RunAsync(Source, LanguageVersion.CSharp12);
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
