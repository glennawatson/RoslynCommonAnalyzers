// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2459OptionalByRefParameterAnalyzer,
    StyleSharp.Analyzers.Sst2459OptionalByRefParameterCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Tests for SST2459 ([Optional] on a ref or out parameter).</summary>
public class OptionalByRefParameterAnalyzerUnitTest
{
    /// <summary>An [Optional] ref parameter.</summary>
    private const string RefParameterSource = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Load([{|SST2459:Optional|}] ref int value) => value = 1;
        }
        """;

    /// <summary>The ref parameter case after the fix.</summary>
    private const string RefParameterFixed = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Load(ref int value) => value = 1;
        }
        """;

    /// <summary>An [Optional] out parameter.</summary>
    private const string OutParameterSource = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public bool TryLoad([{|SST2459:Optional|}] out int value)
            {
                value = 0;
                return false;
            }
        }
        """;

    /// <summary>The out parameter case after the fix.</summary>
    private const string OutParameterFixed = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public bool TryLoad(out int value)
            {
                value = 0;
                return false;
            }
        }
        """;

    /// <summary>[Optional] sharing an attribute list with [In] on a ref parameter.</summary>
    private const string BesideInAttributeSource = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Fill([In, {|SST2459:Optional|}] ref int buffer) => buffer = 1;
        }
        """;

    /// <summary>The shared-list case after the fix: only [Optional] is gone.</summary>
    private const string BesideInAttributeFixed = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Fill([In] ref int buffer) => buffer = 1;
        }
        """;

    /// <summary>[Optional] sharing an attribute list with [Out] on an out parameter.</summary>
    private const string BesideOutAttributeSource = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Extract([Out, {|SST2459:Optional|}] out int value) => value = 0;
        }
        """;

    /// <summary>The shared-list out case after the fix: only [Optional] is gone.</summary>
    private const string BesideOutAttributeFixed = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Extract([Out] out int value) => value = 0;
        }
        """;

    /// <summary>A fully qualified [Optional] on a ref parameter.</summary>
    private const string QualifiedSource = """
        public sealed class Session
        {
            public void Load([{|SST2459:System.Runtime.InteropServices.Optional|}] ref int value) => value = 1;
        }
        """;

    /// <summary>The qualified case after the fix.</summary>
    private const string QualifiedFixed = """
        public sealed class Session
        {
            public void Load(ref int value) => value = 1;
        }
        """;

    /// <summary>Two [Optional] by-reference parameters in one declaration.</summary>
    private const string TwoParametersSource = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Load([{|SST2459:Optional|}] ref int first, [{|SST2459:Optional|}] out int second)
            {
                first = 0;
                second = 0;
            }
        }
        """;

    /// <summary>The two-parameter case after both fixes.</summary>
    private const string TwoParametersFixed = """
        using System.Runtime.InteropServices;

        public sealed class Session
        {
            public void Load(ref int first, out int second)
            {
                first = 0;
                second = 0;
            }
        }
        """;

    /// <summary>An [Optional] out parameter on a delegate declaration.</summary>
    private const string DelegateSource = """
        using System.Runtime.InteropServices;

        public delegate bool Loader([{|SST2459:Optional|}] out int value);
        """;

    /// <summary>The delegate case after the fix.</summary>
    private const string DelegateFixed = """
        using System.Runtime.InteropServices;

        public delegate bool Loader(out int value);
        """;

    /// <summary>Verifies an [Optional] ref parameter is reported and the attribute removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalRefParameterIsReportedAndAttributeRemovedAsync()
        => await Verify.VerifyCodeFixAsync(RefParameterSource, RefParameterFixed);

    /// <summary>Verifies an [Optional] out parameter is reported and the attribute removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalOutParameterIsReportedAndAttributeRemovedAsync()
        => await Verify.VerifyCodeFixAsync(OutParameterSource, OutParameterFixed);

    /// <summary>Verifies removing [Optional] keeps the rest of a shared attribute list on a ref parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalBesideInAttributeKeepsRestOfListAsync()
        => await Verify.VerifyCodeFixAsync(BesideInAttributeSource, BesideInAttributeFixed);

    /// <summary>Verifies removing [Optional] keeps the rest of a shared attribute list on an out parameter.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalBesideOutAttributeKeepsRestOfListAsync()
        => await Verify.VerifyCodeFixAsync(BesideOutAttributeSource, BesideOutAttributeFixed);

    /// <summary>Verifies a fully qualified [Optional] is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedOptionalIsReportedAndAttributeRemovedAsync()
        => await Verify.VerifyCodeFixAsync(QualifiedSource, QualifiedFixed);

    /// <summary>Verifies every [Optional] by-reference parameter in a declaration is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryOptionalByRefParameterIsFixedAsync()
        => await Verify.VerifyCodeFixAsync(TwoParametersSource, TwoParametersFixed);

    /// <summary>Verifies an [Optional] by-reference parameter on a delegate is reported and fixed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DelegateOptionalOutParameterIsReportedAsync()
        => await Verify.VerifyCodeFixAsync(DelegateSource, DelegateFixed);

    /// <summary>Verifies an [Optional] by-value parameter is not reported: callers really can omit it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalByValueParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class Session
            {
                public void Load([Optional] int value)
                {
                }
            }
            """);

    /// <summary>Verifies an [Optional] in parameter is not reported: callers really can omit it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalInParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class Session
            {
                public int Read([Optional] in int value) => value;
            }
            """);

    /// <summary>Verifies an [Optional] ref readonly parameter is not reported: callers really can omit it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OptionalRefReadonlyParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class Session
            {
                public int Read([Optional] ref readonly int value) => value;
            }
            """);

    /// <summary>Verifies members of a COM-imported type are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComImportMemberIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Runtime.InteropServices;

            [ComImport]
            [Guid("8f7cd353-7d60-47a4-8bcf-1a1eac2af9b4")]
            [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            public interface INativeSession
            {
                void Configure([Optional] ref object options);

                void Fetch([Optional] out object result);
            }
            """);

    /// <summary>Verifies an unrelated attribute that happens to be named Optional is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserDefinedOptionalAttributeIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public sealed class OptionalAttribute : System.Attribute
            {
            }

            public sealed class Session
            {
                public void Load([Optional] ref int value) => value = 1;
            }
            """);

    /// <summary>Verifies by-reference parameters without [Optional] are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnattributedByRefParametersAreCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Runtime.InteropServices;

            public sealed class Session
            {
                public void Copy([In] ref int source, ref int target, out int written)
                {
                    target = source;
                    written = 1;
                }
            }
            """);
}
