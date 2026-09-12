// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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

    /// <summary>The type after the fix leads with the type name and names its first public property.</summary>
    private const string WithPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
        public class Money
        {
            public int Amount { get; set; }
        }
        """;

    /// <summary>A public type whose only state is a private field.</summary>
    private const string NoPropertySource = """
        public class {|SST2334:Money|}
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>The type after the fix names the field, which a display string may read in the type's own context.</summary>
    private const string NoPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {_amount}")]
        public class Money
        {
            private int _amount;

            public int Read() => _amount;
        }
        """;

    /// <summary>A public type whose only property is implemented explicitly, so it is not nameable.</summary>
    private const string ExplicitImplementationSource = """
        public interface IHolder
        {
            object Value { get; }
        }

        public class {|SST2334:Holder|} : IHolder
        {
            object IHolder.Value => new object();
        }
        """;

    /// <summary>The type after the fix falls back to <c>ToString()</c> rather than naming the explicit member.</summary>
    private const string ExplicitImplementationFixed = """
        public interface IHolder
        {
            object Value { get; }
        }

        [System.Diagnostics.DebuggerDisplay("Holder: {ToString(),nq}")]
        public class Holder : IHolder
        {
            object IHolder.Value => new object();
        }
        """;

    /// <summary>A public type whose only readable member is its own <c>ToString()</c>.</summary>
    private const string ToStringOnlySource = """
        public class {|SST2334:Money|}
        {
            public override string ToString() => "money";
        }
        """;

    /// <summary>The type after the fix falls back to <c>ToString()</c>.</summary>
    private const string ToStringOnlyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {ToString(),nq}")]
        public class Money
        {
            public override string ToString() => "money";
        }
        """;

    /// <summary>A public type whose only property is not publicly exposed.</summary>
    private const string NonPublicPropertySource = """
        public class {|SST2334:Money|}
        {
            internal int Amount { get; set; }
        }
        """;

    /// <summary>The type after the fix names the internal property in preference to <c>ToString()</c>.</summary>
    private const string NonPublicPropertyFixed = """
        [System.Diagnostics.DebuggerDisplay("Money: {Amount}")]
        public class Money
        {
            internal int Amount { get; set; }
        }
        """;

    /// <summary>A public generic type, whose prefix must not carry its type parameter list.</summary>
    private const string GenericSource = """
        public class {|SST2334:Box|}<T>
        {
            public T Value { get; set; }
        }
        """;

    /// <summary>The type after the fix, prefixed with the bare type name.</summary>
    private const string GenericFixed = """
        [System.Diagnostics.DebuggerDisplay("Box: {Value}")]
        public class Box<T>
        {
            public T Value { get; set; }
        }
        """;

    /// <summary>Verifies the fix names the type's first public property in the display string.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingFirstPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(WithPropertySource, WithPropertyFixed);

    /// <summary>Verifies the fix names a private field rather than saying nothing about the instance.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingPrivateFieldAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(NoPropertySource, NoPropertyFixed);

    /// <summary>Verifies the fix prefers a non-public property over the <c>ToString()</c> fallback.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeNamingNonPublicPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(NonPublicPropertySource, NonPublicPropertyFixed);

    /// <summary>Verifies the fix falls back to <c>ToString()</c> when the type has no member to name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributeFallingBackToToStringAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(ToStringOnlySource, ToStringOnlyFixed);

    /// <summary>Verifies a generic type is prefixed with its bare name, without the type parameter list.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AddsAttributePrefixedWithBareGenericNameAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(GenericSource, GenericFixed);

    /// <summary>Verifies an explicitly implemented property is not named, because the display string cannot read it.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A display string binds in the type's own context, where an explicit implementation is reachable only
    /// through a cast to the interface. Naming it produces an attribute that points at nothing.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DoesNotNameAnExplicitlyImplementedPropertyAsync() =>
        VerifyDisplay.VerifyCodeFixAsync(ExplicitImplementationSource, ExplicitImplementationFixed);
}
