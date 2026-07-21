// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeRawHtml = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1701RawHtmlFromNonConstantAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1701 (raw HTML rendered from a non-constant value).</summary>
public class RawHtmlFromNonConstantAnalyzerUnitTest
{
    /// <summary>Inline stubs of the Blazor raw-markup surface the rule gates on.</summary>
    private const string BlazorStub =
        """

        namespace Microsoft.AspNetCore.Components
        {
            public readonly struct MarkupString
            {
                public MarkupString(string value) => Value = value;

                public string Value { get; }

                public static explicit operator MarkupString(string value) => default;
            }
        }

        namespace Microsoft.AspNetCore.Components.Rendering
        {
            public sealed class RenderTreeBuilder
            {
                public void AddMarkupContent(int sequence, string markupContent) { }
            }
        }
        """;

    /// <summary>Verifies <c>new MarkupString(x)</c> over a non-constant value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                public MarkupString M(string html) => new MarkupString({|SES1701:html|});
            }
            """);

    /// <summary>Verifies a fully-qualified constructor over a non-constant value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedConstructorNonConstantReportedAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public Microsoft.AspNetCore.Components.MarkupString M(string html)
                    => new Microsoft.AspNetCore.Components.MarkupString({|SES1701:html|});
            }
            """);

    /// <summary>Verifies a non-constant interpolated value passed to the constructor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorInterpolatedReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                public MarkupString M(string name) => new MarkupString({|SES1701:$"<b>{name}</b>"|});
            }
            """);

    /// <summary>Verifies the explicit <c>(MarkupString)x</c> conversion over a non-constant value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                public MarkupString M(string html) => (MarkupString){|SES1701:html|};
            }
            """);

    /// <summary>Verifies <c>RenderTreeBuilder.AddMarkupContent(seq, x)</c> over a non-constant value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddMarkupContentNonConstantReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Rendering;

            public class C
            {
                public void M(RenderTreeBuilder builder, string html) => builder.AddMarkupContent(0, {|SES1701:html|});
            }
            """);

    /// <summary>Verifies a constant literal passed to the constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorLiteralIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                public MarkupString M() => new MarkupString("<b>hello</b>");
            }
            """);

    /// <summary>Verifies a <c>const</c>-field value passed to the constructor is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructorConstFieldIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private const string Markup = "<b>hello</b>";

                public MarkupString M() => new MarkupString(Markup);
            }
            """);

    /// <summary>Verifies a constant literal cast is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CastLiteralIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                public MarkupString M() => (MarkupString)"<b>hello</b>";
            }
            """);

    /// <summary>Verifies a constant literal passed to <c>AddMarkupContent</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddMarkupContentLiteralIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components.Rendering;

            public class C
            {
                public void M(RenderTreeBuilder builder) => builder.AddMarkupContent(0, "<b>hello</b>");
            }
            """);

    /// <summary>Verifies a value wrapped in an allow-listed sanitizer call is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SanitizerWrappedValueIsCleanAsync()
    {
        var test = MakeTest(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private static string Sanitize(string value) => value;

                public MarkupString M(string html) => new MarkupString(Sanitize(html));
            }
            """);

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1701.sanitizers = Sanitize, Clean

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a member-access sanitizer call honoured via the project-wide key is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberAccessSanitizerProjectWideKeyIsCleanAsync()
    {
        var test = MakeTest(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private readonly Sanitizer _sanitizer = new Sanitizer();

                public MarkupString M(string html) => new MarkupString(_sanitizer.Clean(html));
            }

            public sealed class Sanitizer
            {
                public string Clean(string value) => value;
            }
            """);

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.sanitizers = Clean

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the same wrapped call is reported when no sanitizer allow-list is configured.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SanitizerCallWithoutOptionReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private static string Sanitize(string value) => value;

                public MarkupString M(string html) => new MarkupString({|SES1701:Sanitize(html)|});
            }
            """);

    /// <summary>Verifies a non-constant call whose callee is itself an invocation (no simple name) is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InvokedFactoryValueReportedAsync()
    {
        var test = MakeTest(
            """
            using System;
            using Microsoft.AspNetCore.Components;

            public class C
            {
                private static Func<string, string> GetSanitizer() => v => v;

                public MarkupString M(string html) => new MarkupString({|SES1701:GetSanitizer()(html)|});
            }
            """);

        test.TestState.AnalyzerConfigFiles.Add(
            ("/.editorconfig", """
            root = true
            [*.cs]
            securitysharp.SES1701.sanitizers = Sanitize

            """));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a constructor of a same-named type in another namespace is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedTypeConstructorIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public Custom.MarkupString M(string html) => new Custom.MarkupString(html);
            }

            namespace Custom
            {
                public readonly struct MarkupString
                {
                    public MarkupString(string value) { }

                    public static explicit operator MarkupString(string value) => new MarkupString(value);
                }
            }
            """);

    /// <summary>Verifies a cast to a same-named type in another namespace is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedTypeCastIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public Custom.MarkupString M(string html) => (Custom.MarkupString)html;
            }

            namespace Custom
            {
                public readonly struct MarkupString
                {
                    public MarkupString(string value) { }

                    public static explicit operator MarkupString(string value) => new MarkupString(value);
                }
            }
            """);

    /// <summary>Verifies a non-<c>MarkupString</c> object creation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedObjectCreationIsCleanAsync()
        => await VerifyAsync(
            """
            using System.Text;

            public class C
            {
                public StringBuilder M(string html) => new StringBuilder(html);
            }
            """);

    /// <summary>Verifies a parameterless object creation is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessObjectCreationIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public object M() => new object();
            }
            """);

    /// <summary>Verifies a cast to a predefined type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PredefinedTypeCastIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public int M(double value) => (int)value;
            }
            """);

    /// <summary>Verifies a same-named <c>AddMarkupContent</c> of a lower arity on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LowerArityAddMarkupContentIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(Widget widget) => widget.AddMarkupContent(0);
            }

            public sealed class Widget
            {
                public void AddMarkupContent(int sequence) { }
            }
            """);

    /// <summary>Verifies a same-named <c>AddMarkupContent</c> on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedAddMarkupContentOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public class C
            {
                public void M(Widget widget, string html) => widget.AddMarkupContent(0, html);
            }

            public sealed class Widget
            {
                public void AddMarkupContent(int sequence, string markupContent) { }
            }
            """);

    /// <summary>Verifies the rule stays silent when the Blazor markup surface is absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenMarkupStringUnavailableAsync()
    {
        var test = new AnalyzeRawHtml.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = """
                       public class C
                       {
                           public Custom.MarkupString M(string html) => new Custom.MarkupString(html);
                       }

                       namespace Custom
                       {
                           public readonly struct MarkupString
                           {
                               public MarkupString(string value) { }
                           }
                       }
                       """,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Builds an analyzer test with the Blazor stub appended and .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>The configured test.</returns>
    private static AnalyzeRawHtml.Test MakeTest(string source)
        => new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + BlazorStub,
        };

    /// <summary>Runs an analyzer-only verification with the inline Blazor stub appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
        => await MakeTest(source).RunAsync(CancellationToken.None);
}
