// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1211RemoveIntermediateToStringAnalyzer,
    PerformanceSharp.Analyzers.Psh1211RemoveIntermediateToStringCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1211RemoveIntermediateToStringAnalyzer"/> (PSH1211 intermediate ToString).</summary>
public class RemoveIntermediateToStringAnalyzerUnitTest
{
    /// <summary>Verifies a ToString argument with a direct overload is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ArgumentWithDirectOverloadIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(int value) => Console.Write({|PSH1211:value.ToString()|});
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public void M(int value) => Console.Write(value);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString inside an interpolation hole is flagged and unwrapped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolationHoleIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public string M(int count) => $"{{|PSH1211:count.ToString()|}} items";
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       public string M(int count) => $"{count} items";
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a ToString with a format argument stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FormattedToStringIsCleanAsync() =>
        VerifyAsync(
            """
            using System;

            public class C
            {
                public void M(int value) => Console.Write(value.ToString("X"));
            }
            """);

    /// <summary>Verifies a ToString feeding a method without a direct overload stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NoDirectOverloadIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public void M(System.Guid id) => Use(id.ToString());

                private static void Use(string text)
                {
                }
            }
            """);

    /// <summary>Verifies a single-hole interpolation wrapper stays clean; PSH1205 owns that shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SingleHoleInterpolationIsCleanAsync() =>
        VerifyAsync(
            """
            public class C
            {
                public string M(int count) => $"{count.ToString()}";
            }
            """);

    /// <summary>Verifies a span hole keeps its ToString where the framework cannot format one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Without <c>DefaultInterpolatedStringHandler</c> an interpolated string goes through
    /// <c>string.Format(object)</c>, and a ref struct has no conversion to <c>object</c> (CS0029).
    /// </remarks>
    [Test]
    public async Task SpanHoleOnFrameworkWithoutHandlerIsCleanAsync()
    {
        const string Source = """
                              public ref struct Slice
                              {
                                  public override string ToString() => "slice";
                              }

                              public class C
                              {
                                  public string M(Slice slice) => $"[{slice.ToString()}]";
                              }
                              """;
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20, TestCode = Source, };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a span hole is still reported where the framework can format one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpanHoleOnFrameworkWithHandlerIsReportedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M(string text)
                                  {
                                      var span = text.AsSpan();
                                      return $"[{{|PSH1211:span.ToString()|}}]";
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       public string M(string text)
                                       {
                                           var span = text.AsSpan();
                                           return $"[{span}]";
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies consumers without a compatible direct overload keep the string conversion.</summary>
    /// <param name="members">The members defining the consumer and conversion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("string M(int value) => value.ToString();")]
    [Arguments("void M(string value) => Use(value.ToString()); void Use(string value) { } void Use(int value) { }")]
    [Arguments("void M<T>(T value) => Use(value.ToString()); void Use(string value) { } void Use(int value) { }")]
    [Arguments("void M(dynamic value) => Use(value.ToString()); void Use(string value) { } void Use(int value) { }")]
    [Arguments("void M(int value) => Use(text: value.ToString()); void Use(string text) { } void Use(int text) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(object value) { } void Use(int value) { }")]
    [Arguments("void M(int value) => Use(\"first\", value.ToString()); void Use(params string[] values) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(string value) { } void Use(object value) { }")]
    [Arguments("void M(int value) => Use(value.ToString(), 1); void Use(string value, int other) { } void Use(int value, string other) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(string value) { } void Use(System.IFormattable value) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(string value) { } void Use<T>(int value) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(string value) { } static void Use(int value) { }")]
    [Arguments("void M(int value) => Use(value.ToString()); void Use(string value) { } void Use(int value, int other) { }")]
    [Arguments("void M(int value) => new System.Text.StringBuilder().Append(value.ToString());")]
    [Arguments("System.FormattableString M(int value) => $\"Value: {value.ToString()}\";")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task IncompatibleConsumersAreSilentAsync(string members) => VerifyAsync($"class C {{ {members} }}");

    /// <summary>Verifies extension calls and user-defined conversions are not substituted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ExtensionAndUserDefinedConversionsAreSilentAsync() => VerifyAsync(
        """
        class C
        {
            void M(int value, Value custom)
            {
                this.Use(value.ToString());
                Extensions.Use(this, value.ToString());
                Consume(custom.ToString());
            }
            void Consume(string text) { }
            void Consume(int value) { }
        }
        struct Value
        {
            public static implicit operator int(Value value) => 0;
        }
        static class Extensions
        {
            public static void Use(this C receiver, string value) { }
            public static void Use(this C receiver, int value) { }
        }
        """);

    /// <summary>Verifies unmatched symbols are ignored while binding incomplete code.</summary>
    /// <param name="statement">The incomplete consumer invocation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("Missing(value.ToString());")]
    [Arguments("Use(unknown.ToString());")]
    [Arguments("Use((null).ToString());")]
    public async Task UnresolvedConsumersAreSilentAsync(string statement)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.Net90,
            TestCode = $"class C {{ void M(int value) {{ {statement} }} void Use(string text) {{ }} void Use(int value) {{ }} }}",
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies matching surrounding parameters permit a numeric widening overload.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task MatchingParametersAndNumericConversionAreFixedAsync() => VerifyAsync(
        """
        class C
        {
            void M(int value) => Use(1, {|PSH1211:value.ToString()|}, true);
            void Use(int first, string value, bool last) { }
            void Use(int first, long value, bool last) { }
        }
        """,
        """
        class C
        {
            void M(int value) => Use(1, value, true);
            void Use(int first, string value, bool last) { }
            void Use(int first, long value, bool last) { }
        }
        """);

    /// <summary>Verifies a nullable value can use a matching nullable overload directly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task NullableReceiverWithDirectOverloadIsFixedAsync() => VerifyAsync(
        "class C { void M(int? value) => Use({|PSH1211:value.ToString()|}); void Use(string value) { } void Use(int? value) { } }",
        "class C { void M(int? value) => Use(value); void Use(string value) { } void Use(int? value) { } }");

    /// <summary>Verifies ordinary values can be formatted without a handler on older frameworks.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValueHoleWithoutHandlerIsFixedAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = AnalyzerFrameworks.NetStandard20,
            TestCode = "class C { string M(int value) => $\"Value: {{|PSH1211:value.ToString()|}}\"; }",
            FixedCode = "class C { string M(int value) => $\"Value: {value}\"; }",
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source, };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
