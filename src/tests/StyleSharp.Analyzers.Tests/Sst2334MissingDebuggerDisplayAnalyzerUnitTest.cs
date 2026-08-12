// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDisplay = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2334MissingDebuggerDisplayAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2334 (give a public type a debugger-display attribute).</summary>
public class Sst2334MissingDebuggerDisplayAnalyzerUnitTest
{
    /// <summary>Verifies a public class with no debugger-display attribute is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicClassWithoutAttributeIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class {|SST2334:Money|}
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies a public struct with no debugger-display attribute is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PublicStructWithoutAttributeIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public struct {|SST2334:Point|}
            {
                public int X { get; set; }
            }
            """);

    /// <summary>Verifies a type that already carries the attribute is clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeWithAttributeIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            using System.Diagnostics;

            [DebuggerDisplay("{Amount}")]
            public class Money
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies an internal type, invisible outside the assembly, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task InternalTypeIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            internal class Money
            {
                public int Amount { get; set; }
            }
            """);

    /// <summary>Verifies a static class, which has no instances to display, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task StaticClassIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public static class Money
            {
                public static int Amount { get; set; }
            }
            """);

    /// <summary>Verifies a type with nothing to show is not reported — a display string could only repeat the type name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeWithNoMembersIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class Marker
            {
            }
            """);

    /// <summary>Verifies a type holding only statics and constants, which identify no instance, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TypeOfConstantsAndStaticsIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class Limits
            {
                public const int Max = 3;

                public static int Count { get; set; }

                private static readonly int Seed = 1;
            }
            """);

    /// <summary>Verifies an empty positional record, whose every member is compiler-generated, is not reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task EmptyRecordIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync("public record Nothing();");

    /// <summary>Verifies a write-only property, which cannot be shown, does not qualify a type on its own.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WriteOnlyPropertyIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class Sink
            {
                public int Value { set { } }
            }
            """);

    /// <summary>Verifies an indexer, which has no name to drop into a display string, does not qualify a type on its own.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IndexerOnlyTypeIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class Bag
            {
                public int this[int index] => index;
            }
            """);

    /// <summary>Verifies a property that only answers a base contract does not qualify a behavioural type.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task OverridingPropertyIsCleanAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public abstract class {|SST2334:Shape|}
            {
                public abstract int Sides { get; }
            }

            public class Triangle : Shape
            {
                public override int Sides => 3;
            }
            """);

    /// <summary>Verifies a private field counts, because a display string binds in the type's own context.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PrivateFieldIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public struct {|SST2334:Wrapper|}
            {
                private readonly int _value;

                public Wrapper(int value) => _value = value;
            }
            """);

    /// <summary>Verifies a ToString override qualifies a type, since that is what the fallback display string names.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ToStringOverrideIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync(
            """
            public class {|SST2334:Marker|}
            {
                public override string ToString() => "marker";
            }
            """);

    /// <summary>Verifies a positional record's generated property counts as state worth showing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task PositionalRecordIsReportedAsync()
        => await VerifyDisplay.VerifyAnalyzerAsync("public record {|SST2334:Money|}(int Amount);");
}
