// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2422BackingFieldMismatchAnalyzer,
    StyleSharp.Analyzers.Sst2422BackingFieldMismatchCodeFixProvider>;
using VerifyMismatch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2422BackingFieldMismatchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2422 (a property getter reads a different field than its setter writes).</summary>
public class BackingFieldMismatchAnalyzerUnitTest
{
    /// <summary>A property whose accessors use two different fields.</summary>
    private const string MismatchSource = """
        public sealed class C
        {
            private int _width;
            private int _height;

            public int {|SST2422:Width|}
            {
                get => _height;
                set => _width = value;
            }
        }
        """;

    /// <summary>The property after pointing the getter at the setter's field.</summary>
    private const string MismatchFixed = """
        public sealed class C
        {
            private int _width;
            private int _height;

            public int Width
            {
                get => _width;
                set => _width = value;
            }
        }
        """;

    /// <summary>Verifies block getters, init accessors, and qualified fields still identify mismatched storage.</summary>
    /// <param name="accessors">The accessors that read and write different fields.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("get { return this.first; } set { this.second = value; }")]
    [Arguments("get { return first; } init => second = value;")]
    [Arguments("set { second = value; } get => first;")]
    [Arguments("get => this.first; set { if (value < 0) return; second = 0; second = value; }")]
    public Task SupportedAccessorShapesReportMismatchAsync(string accessors) =>
        VerifyMismatch.VerifyAnalyzerAsync($$"""class C { int first, second; public int {|SST2422:Value|} { {{accessors}} } }""");

    /// <summary>Verifies properties without one provable instance-field read and write remain unchanged.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("public int Value => first;")]
    [Arguments("public int Value { set => second = value; }")]
    [Arguments("public int Value { get => first; }")]
    [Arguments("public int Value { get; set; }")]
    [Arguments("public int Value { get { return first + 1; } set => second = value; }")]
    [Arguments("public int Value { get { var result = first; return result; } set => second = value; }")]
    [Arguments("public int Value { get { throw new System.Exception(); } set => second = value; }")]
    [Arguments("public int Value { get => first; set => Consume(value); }")]
    [Arguments("public int Value { get => first; set { Consume(value); } }")]
    [Arguments("public int Value { get => first; set { first = value; second = value; } }")]
    [Arguments("public int Value { get => first; set => second += value; }")]
    [Arguments("public int Value { get => first; set => second = 1; }")]
    [Arguments("public int Value { get => first; set => values[0] = value; }")]
    [Arguments("public int Value { get => shared; set => second = value; }")]
    [Arguments("public int Value { get => first; set => shared = value; }")]
    [Arguments("public int Value { get => Other; set => second = value; }")]
    [Arguments("public int Value { get => first; set => Other = value; }")]
    public Task UnprovenBackingFieldsAreIgnoredAsync(string property) =>
        VerifyMismatch.VerifyAnalyzerAsync(
            $$"""
            class C
            {
                int first, second;
                static int shared;
                int[] values = new int[1];
                int Other { get; set; }
                void Consume(int value) { }
                {{property}}
            }
            """);

    /// <summary>Verifies incomplete returns and unresolved accessor symbols cannot establish a mismatch.</summary>
    /// <param name="accessors">The unfinished accessors.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("get { return; } set => second = value;")]
    [Arguments("get => missing; set => second = value;")]
    [Arguments("get => first; set => missing = value;")]
    public Task IncompleteAccessorBindingsAreIgnoredAsync(string accessors) =>
        new VerifyMismatch.Test { TestCode = $$"""class C { int first, second; public int Value { {{accessors}} } }""", CompilerDiagnostics = CompilerDiagnostics.None }
            .RunAsync(CancellationToken.None);

    /// <summary>Verifies a getter and setter using different fields is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MismatchedFieldsIsReportedAsync() =>
        VerifyMismatch.VerifyAnalyzerAsync(MismatchSource);

    /// <summary>Verifies a property that round-trips one field is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SameFieldIsCleanAsync() =>
        VerifyMismatch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _width;

                public int Width
                {
                    get => _width;
                    set => _width = value;
                }
            }
            """);

    /// <summary>Verifies the change-notification shape, which round-trips one field, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ChangeNotificationShapeIsCleanAsync() =>
        VerifyMismatch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private int _width;

                public event EventHandler Changed;

                public int Width
                {
                    get => _width;
                    set
                    {
                        if (_width == value)
                        {
                            return;
                        }

                        _width = value;
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            """);

    /// <summary>Verifies a computed getter is clean: it does not reduce to one field read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComputedGetterIsCleanAsync() =>
        VerifyMismatch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _width;
                private int _height;

                public int Area
                {
                    get => _width * _height;
                    set => _width = value;
                }
            }
            """);

    /// <summary>Verifies the fix points the getter at the setter's field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixPointsGetterAtSetterFieldAsync() =>
        VerifyFix.VerifyCodeFixAsync(MismatchSource, MismatchFixed);
}
