// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyDebuggerDisplay = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2405DebuggerDisplayNamesMissingMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2405 (a debugger display string naming a member the type does not declare).</summary>
public class DebuggerDisplayNamesMissingMemberAnalyzerUnitTest
{
    /// <summary>Verifies malformed and escaped expressions do not produce guessed member names.</summary>
    /// <param name="display">The literal source for the display attribute.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("\"{Unclosed\"")]
    [Arguments("\"{} {   } {123} {()} {Call(x)}\"")]
    [Arguments("\"\\tplain\"")]
    [Arguments("@\"\u005c{Escaped}\"")]
    public Task UncheckableDisplayExpressionsAreCleanAsync(string display) =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync($$"""
            [System.Diagnostics.DebuggerDisplay({{display}})]
            class C { }
            """);

    /// <summary>Verifies trimming and commas outside the current expression preserve diagnostic locations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task WhitespaceAndLaterFormatSpecifiersPreserveNamesAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            [System.Diagnostics.DebuggerDisplayAttribute("{  {|SST2405:First|}  } { {|SST2405:Second|}(),nq} {{|SST2405:_third2|}}")]
            class C { }
            """);

    /// <summary>Verifies alias-qualified names are recognized.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AliasQualifiedAttributeIsReportedAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using D = System.Diagnostics;
            [D::DebuggerDisplay("{{|SST2405:Missing|}}")]
            class C { }
            """);

    /// <summary>Verifies missing arguments, named properties and constant expressions are left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AttributesWithoutPositionalLiteralAreCleanAsync() =>
        new VerifyDebuggerDisplay.Test
        {
            TestCode = """
                using System;
                using System.Diagnostics;
                [Obsolete] class A { }
                [DebuggerDisplay] class B { }
                [DebuggerDisplay()] class C { }
                [DebuggerDisplay(Name = "{Missing}")] class D { }
                [DebuggerDisplay(Text)] class E { const string Text = "{Missing}"; }
                [DebuggerDisplay(null)] class F { }
                """,
            CompilerDiagnostics = Microsoft.CodeAnalysis.Testing.CompilerDiagnostics.None,
        }.RunAsync(CancellationToken.None);

    /// <summary>Verifies a display string naming a member the type does not have is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task MissingMemberIsReportedAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("Order {{|SST2405:Total|}}")]
            public sealed class Order
            {
                public decimal Amount { get; }
            }
            """);

    /// <summary>Verifies the format specifier and the call parentheses are read past to find the member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SpecifierAndCallAreReadPastAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("{{|SST2405:Label|},nq} {{|SST2405:Describe|}(),nq}")]
            public sealed class Order
            {
                public decimal Amount { get; }
            }
            """);

    /// <summary>Verifies every member the display string names is checked, not only the first.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EachMissingMemberIsReportedAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("{Amount} of {{|SST2405:Currency|}} at {{|SST2405:Rate|}}")]
            public sealed class Order
            {
                public decimal Amount { get; }
            }
            """);

    /// <summary>Verifies a display string naming members the type has is clean, private ones included.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DeclaredMembersAreCleanAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("{Amount,nq} {_currency} {Describe(),nq}")]
            public sealed class Order
            {
                private readonly string _currency = "USD";

                public decimal Amount { get; }

                public string Describe() => _currency;
            }
            """);

    /// <summary>Verifies an inherited member is one the debugger will find.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task InheritedMemberIsCleanAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public abstract class Entity
            {
                public int Id { get; }
            }

            [DebuggerDisplay("Order {Id}")]
            public sealed class Order : Entity
            {
            }
            """);

    /// <summary>Verifies an expression the rule cannot be sure about is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ComplexExpressionIsCleanAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;
            using System.Diagnostics;

            [DebuggerDisplay("{Items.Count} items, first {Items[0]}")]
            public sealed class Basket
            {
                public List<string> Items { get; } = new();
            }
            """);

    /// <summary>Verifies a display string with no expressions in it at all is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task PlainDisplayStringIsCleanAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("an order")]
            public sealed class Order
            {
                public decimal Amount { get; }
            }
            """);

    /// <summary>Verifies the attribute on a field, whose expressions resolve against that field's type, is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AttributeOnAFieldIsCleanAsync() =>
        VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class Order
            {
                [DebuggerDisplay("{Length}")]
                public string Code = string.Empty;
            }
            """);
}
