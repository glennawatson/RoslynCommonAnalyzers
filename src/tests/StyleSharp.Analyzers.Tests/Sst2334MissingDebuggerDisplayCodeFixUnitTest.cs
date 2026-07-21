// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDisplay = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayAnalyzer,
    StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Sst2334MissingDebuggerDisplayCodeFixProvider"/> (SST2334 add [DebuggerDisplay]).</summary>
public class Sst2334MissingDebuggerDisplayCodeFixUnitTest
{
    /// <summary>A public type with a public property to name.</summary>
    private const string WithPropertySource = """
        public class {|SST2334:Money|}
        {
            public int Amount { get; set; }
        }
        """;

    /// <summary>The type after the fix names its first public property.</summary>
    private const string WithPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("{Amount}")]
        public class Money
        {
            public int Amount { get; set; }
        }
        """;

    /// <summary>A public type with no public property.</summary>
    private const string NoPropertySource = """
        public class {|SST2334:Money|}
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>The type after the fix falls back to <c>ToString()</c>.</summary>
    private const string NoPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("{ToString(),nq}")]
        public class Money
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>Verifies the fix names the type's first public property in the display string.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsAttributeNamingFirstPropertyAsync()
        => await VerifyDisplay.VerifyCodeFixAsync(WithPropertySource, WithPropertyFixed);

    /// <summary>Verifies the fix falls back to <c>ToString()</c> when the type has no public property.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AddsAttributeFallingBackToToStringAsync()
        => await VerifyDisplay.VerifyCodeFixAsync(NoPropertySource, NoPropertyFixed);
}
