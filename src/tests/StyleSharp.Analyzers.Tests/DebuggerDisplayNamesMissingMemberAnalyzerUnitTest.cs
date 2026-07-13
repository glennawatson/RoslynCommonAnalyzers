// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDebuggerDisplay = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2405DebuggerDisplayNamesMissingMemberAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2405 (a debugger display string naming a member the type does not declare).</summary>
public class DebuggerDisplayNamesMissingMemberAnalyzerUnitTest
{
    /// <summary>Verifies a display string naming a member the type does not have is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MissingMemberIsReportedAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task SpecifierAndCallAreReadPastAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task EachMissingMemberIsReportedAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task DeclaredMembersAreCleanAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task InheritedMemberIsCleanAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task ComplexExpressionIsCleanAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task PlainDisplayStringIsCleanAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
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
    [Test]
    public async Task AttributeOnAFieldIsCleanAsync()
        => await VerifyDebuggerDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            public sealed class Order
            {
                [DebuggerDisplay("{Length}")]
                public string Code = string.Empty;
            }
            """);
}
