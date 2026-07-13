// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1223UseCompositeFormatAnalyzer,
    PerformanceSharp.Analyzers.Psh1223UseCompositeFormatCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1223UseCompositeFormatAnalyzer"/> (PSH1223 CompositeFormat).</summary>
public class UseCompositeFormatAnalyzerUnitTest
{
    /// <summary>Verifies a constant format is flagged and hoisted into a field the call then reads.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The call had no provider, so it was using the current culture; the rewrite names the current
    /// culture rather than passing a bare <see langword="null"/>, which would be ambiguous against the
    /// old <c>Format(string, object)</c> overload.
    /// </remarks>
    [Test]
    public async Task ConstantFormatIsFlaggedAndHoistedAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;
                              using System.Text;

                              public class C
                              {
                                  public string Greet(string name) => string.Format({|PSH1223:"Hello, {0}!"|}, name);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly CompositeFormat GreetFormat = CompositeFormat.Parse("Hello, {0}!");

                                       public string Greet(string name) => string.Format(CultureInfo.CurrentCulture, GreetFormat, name);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a call that already supplies a provider keeps it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingProviderIsKeptAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;
                              using System.Text;

                              public class C
                              {
                                  public string Render(int count)
                                      => string.Format(CultureInfo.InvariantCulture, {|PSH1223:"{0} items"|}, count);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly CompositeFormat RenderFormat = CompositeFormat.Parse("{0} items");

                                       public string Render(int count)
                                           => string.Format(CultureInfo.InvariantCulture, RenderFormat, count);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a constant field used as the format is reported, and reused as-is in the hoisted field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A <c>const</c> is a compile-time constant, so it binds at field-initializer level just as well
    /// as it did inside the method and the hoisted field can simply refer to it.
    /// </remarks>
    [Test]
    public async Task ConstFieldFormatIsReusedAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;
                              using System.Text;

                              public class C
                              {
                                  private const string Template = "{0}/{1}";

                                  public string Join(string a, string b) => string.Format({|PSH1223:Template|}, a, b);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly CompositeFormat JoinFormat = CompositeFormat.Parse(Template);

                                       private const string Template = "{0}/{1}";

                                       public string Join(string a, string b) => string.Format(CultureInfo.CurrentCulture, JoinFormat, a, b);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a name collision is avoided rather than shadowing an existing member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollidingFieldNameIsMadeUniqueAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;
                              using System.Text;

                              public class C
                              {
                                  private readonly string GreetFormat = "taken";

                                  public string Greet(string name) => string.Format({|PSH1223:"Hi {0}"|}, name);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly CompositeFormat GreetFormat2 = CompositeFormat.Parse("Hi {0}");

                                       private readonly string GreetFormat = "taken";

                                       public string Greet(string name) => string.Format(CultureInfo.CurrentCulture, GreetFormat2, name);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a format built at run time is not reported — there is nothing constant to hoist.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantFormatIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Text;

            public class C
            {
                public string M(string format, string name) => string.Format(format, name);
            }
            """);

    /// <summary>Verifies a malformed format is not hoisted, so a FormatException does not become a type-init failure.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>CompositeFormat.Parse</c> would throw on this format, and it would throw from a static field
    /// initializer — moving the failure away from the call that causes it and wrapping it in a
    /// <c>TypeInitializationException</c>. The rule refuses to move an exception, so it leaves this
    /// alone.
    /// </remarks>
    [Test]
    public async Task MalformedFormatIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Text;

            public class C
            {
                public string M(string name) => string.Format("Hello, {0!", name);
            }
            """);

    /// <summary>Verifies a constant with no placeholder at all is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormatWithoutPlaceholdersIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Text;

            public class C
            {
                public string M(string name) => string.Format("no holes here", name);
            }
            """);

    /// <summary>Verifies escaped braces are understood and do not stop a real placeholder being hoisted.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapedBracesAreUnderstoodAsync()
    {
        const string Source = """
                              using System;
                              using System.Globalization;
                              using System.Text;

                              public class C
                              {
                                  public string Wrap(string name) => string.Format({|PSH1223:"{{{0,-8:X}}}"|}, name);
                              }
                              """;
        const string FixedSource = """
                                   using System;
                                   using System.Globalization;
                                   using System.Text;

                                   public class C
                                   {
                                       private static readonly CompositeFormat WrapFormat = CompositeFormat.Parse("{{{0,-8:X}}}");

                                       public string Wrap(string name) => string.Format(CultureInfo.CurrentCulture, WrapFormat, name);
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a format call inside an expression tree is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FormatInsideExpressionTreeIsCleanAsync()
        => await VerifyAsync(
            """
            using System;
            using System.Linq.Expressions;
            using System.Text;

            public class C
            {
                public Expression<Func<string, string>> M() => name => string.Format("Hello, {0}", name);
            }
            """);

    /// <summary>
    /// Verifies the rule registers nothing against netstandard2.0, where <c>CompositeFormat</c> does
    /// not exist.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// <c>CompositeFormat</c> and the <c>string.Format</c> overloads that take one arrived in .NET 8.
    /// Both are resolved from the analyzed compilation, and the rule registers no syntax action at all
    /// when either is missing — so on netstandard2.0, .NET Framework, and every .NET before 8, the same
    /// <c>string.Format</c> call is simply left alone.
    /// </remarks>
    [Test]
    public async Task NetStandard20IsSilentAsync()
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = """
                       public class C
                       {
                           public string Greet(string name) => string.Format("Hello, {0}!", name);
                       }
                       """,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        await test.RunAsync(CancellationToken.None);
    }
}
