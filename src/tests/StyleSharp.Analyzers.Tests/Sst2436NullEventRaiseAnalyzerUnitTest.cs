// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    [Test]
    public async Task NullSenderIsFixedAsync()
        => await VerifyNullEventRaise.VerifyCodeFixAsync(NullSenderSource, NullSenderFixed);

    /// <summary>Verifies a null-forgiving null sender is reported and replaced with <c>this</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullBangSenderIsFixedAsync()
        => await VerifyNullEventRaise.VerifyCodeFixAsync(NullBangSenderSource, NullBangSenderFixed);

    /// <summary>Verifies null event args are reported and replaced with <c>EventArgs.Empty</c>.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullArgsIsFixedAsync()
        => await VerifyNullEventRaise.VerifyCodeFixAsync(NullArgsSource, NullArgsFixed);

    /// <summary>Verifies Fix All repairs every null-sender raise in the document.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRepairsEveryRaiseAsync()
        => await VerifyNullEventRaise.VerifyCodeFixAsync(FixAllSource, FixAllFixed);

    /// <summary>Verifies raising with <c>EventArgs.Empty</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EventArgsEmptyIsCleanAsync()
        => await VerifyNullEventRaise.VerifyAnalyzerAsync(
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
    [Test]
    public async Task StaticEventNullSenderIsCleanAsync()
        => await VerifyNullEventRaise.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class Widget
            {
                public static event EventHandler Changed;

                public static void Raise(EventArgs e) => Changed?.Invoke(null, e);
            }
            """);
}
