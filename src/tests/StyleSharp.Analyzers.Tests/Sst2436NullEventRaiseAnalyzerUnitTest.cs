// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using VerifyNullEventRaise = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2436NullEventRaiseAnalyzer,
    StyleSharp.Analyzers.Sst2436NullEventRaiseCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2436 (an event raised with a null sender or null args).</summary>
public class Sst2436NullEventRaiseAnalyzerUnitTest
{
    /// <summary>A raise with a null sender.</summary>
    private const string NullSenderSource = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise(EventArgs e) => Changed?.Invoke({|SST2436:null|}, e);
        }
        """;

    /// <summary>The null sender after the fix.</summary>
    private const string NullSenderFixed = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise(EventArgs e) => Changed?.Invoke(this, e);
        }
        """;

    /// <summary>A raise with a null-forgiving null sender.</summary>
    private const string NullBangSenderSource = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise(EventArgs e) => Changed?.Invoke({|SST2436:null!|}, e);
        }
        """;

    /// <summary>The null-forgiving sender after the fix.</summary>
    private const string NullBangSenderFixed = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise(EventArgs e) => Changed?.Invoke(this, e);
        }
        """;

    /// <summary>A raise with null event args.</summary>
    private const string NullArgsSource = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise() => Changed?.Invoke(this, {|SST2436:null|});
        }
        """;

    /// <summary>The null event args after the fix.</summary>
    private const string NullArgsFixed = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;

            public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
        }
        """;

    /// <summary>Two raises with a null sender in one document.</summary>
    private const string FixAllSource = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;
            public event EventHandler Updated;

            public void RaiseChanged(EventArgs e) => Changed?.Invoke({|SST2436:null|}, e);

            public void RaiseUpdated(EventArgs e) => Updated?.Invoke({|SST2436:null|}, e);
        }
        """;

    /// <summary>Both raises after the fix.</summary>
    private const string FixAllFixed = """
        using System;

        public sealed class Widget
        {
            public event EventHandler Changed;
            public event EventHandler Updated;

            public void RaiseChanged(EventArgs e) => Changed?.Invoke(this, e);

            public void RaiseUpdated(EventArgs e) => Updated?.Invoke(this, e);
        }
        """;

    /// <summary>Verifies a null sender is reported and replaced with <c>this</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullSenderIsFixedAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(NullSenderSource, NullSenderFixed);

    /// <summary>Verifies a null-forgiving null sender is reported and replaced with <c>this</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullBangSenderIsFixedAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(NullBangSenderSource, NullBangSenderFixed);

    /// <summary>Verifies null event args are reported and replaced with <c>EventArgs.Empty</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NullArgsIsFixedAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(NullArgsSource, NullArgsFixed);

    /// <summary>Verifies Fix All repairs every null-sender raise in the document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FixAllRepairsEveryRaiseAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(FixAllSource, FixAllFixed);

    /// <summary>Verifies raising with <c>EventArgs.Empty</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EventArgsEmptyIsCleanAsync() =>
        VerifyNullEventRaise.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Widget
            {
                public event EventHandler Changed;

                public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies a static event raised with a null sender is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticEventNullSenderIsCleanAsync() =>
        VerifyNullEventRaise.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Widget
            {
                public static event EventHandler Changed;

                public static void Raise(EventArgs e) => Changed?.Invoke(null, e);
            }
            """);

    /// <summary>Verifies direct delegate member access and parentheses preserve both null diagnostics.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task DirectInvocationWithParenthesizedNullsIsFixedAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(
            """
            using System;
            public class C
            {
                public event EventHandler Changed;
                public void Raise() => Changed.Invoke({|SST2436:(null)|}, {|SST2436:(null!)|});
            }
            """,
            """
            using System;
            public class C
            {
                public event EventHandler Changed;
                public void Raise() => Changed.Invoke(this, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies a derived event-args type is reported without offering an incompatible empty instance.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task DerivedEventArgsHaveNoAutomaticFixAsync()
    {
        const string Source = """
            using System;
            public class CustomArgs : EventArgs { }
            public class C
            {
                public event EventHandler<CustomArgs> Changed;
                public void Raise() => Changed?.Invoke(this, {|SST2436:null|});
            }
            """;
        var test = new VerifyNullEventRaise.Test { TestCode = Source, FixedCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the static-sender exemption does not permit null event args.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task StaticEventNullArgsAreFixedAsync() =>
        VerifyNullEventRaise.VerifyCodeFixAsync(
            """
            using System;
            public class C
            {
                public static event EventHandler Changed;
                public static void Raise() => Changed?.Invoke(null, {|SST2436:null|});
            }
            """,
            """
            using System;
            public class C
            {
                public static event EventHandler Changed;
                public static void Raise() => Changed?.Invoke(null, EventArgs.Empty);
            }
            """);

    /// <summary>Verifies syntax and symbol near misses are not treated as event raises.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonEventInvocationsAreCleanAsync() =>
        VerifyNullEventRaise.VerifyAnalyzerAsync(
            """
            using System;
            public delegate void OptionalHandler(object sender, EventArgs args, int extra = 0);
            public class C
            {
                public event EventHandler Changed;
                public event Action<string, EventArgs> StringSender;
                public event Action<object, string> StringArgs;
                public event OptionalHandler Optional;
                public void Invoke(object sender, EventArgs args) { }
                public void Raise(EventHandler handler, Action one, Action<object, EventArgs, int> three)
                {
                    one.Invoke();
                    three.Invoke(null, null, 0);
                    Invoke(null, null);
                    this.Invoke(null, null);
                    handler.Invoke(null, null);
                    handler?.Invoke(null, null);
                    Changed(null, null);
                    StringSender?.Invoke(null, null);
                    StringArgs?.Invoke(null, null);
                    Optional?.Invoke(null, null);
                    Changed?.Invoke(default, default);
                    Changed?.Invoke(0, EventArgs.Empty);
                    int counter = 0;
                    Changed?.Invoke(counter++, EventArgs.Empty);
                }
            }
            """);

    /// <summary>Verifies unresolved event calls are ignored while source is incomplete.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task UnresolvedInvocationIsCleanAsync()
    {
        var test = new VerifyNullEventRaise.Test { CompilerDiagnostics = CompilerDiagnostics.None, TestCode = "class C { void M() { missing.Invoke(null, null); } }" };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a framework without EventArgs never triggers event analysis.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MissingEventArgsTypeIsCleanAsync()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { void M() { missing.Invoke(null, null); } }");
        var compilation = CSharpCompilation.Create(nameof(MissingEventArgsTypeIsCleanAsync), [tree]);
        var diagnostics = await compilation.WithAnalyzers([new Sst2436NullEventRaiseAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }
}
