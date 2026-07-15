// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst2429AccessorIgnoresValueAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2429 (a write accessor that never reads value).</summary>
public class Sst2429AccessorIgnoresValueAnalyzerUnitTest
{
    /// <summary>Verifies a set accessor whose body never reads value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetIgnoresValueIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _h;
                private int _height;
                private int _width;

                public int Height { get => _h; {|SST2429:set|} => _height = _width; }
            }
            """);

    /// <summary>Verifies an init accessor whose body never reads value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InitIgnoresValueIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _w;
                private int _other;

                public int Width { get => _w; {|SST2429:init|} => _w = _other; }
            }
            """);

    /// <summary>Verifies an add accessor whose body never reads value is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AddIgnoresValueIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public class C
            {
                private EventHandler? _handlers;

                private void OnFoo(object? sender, EventArgs e)
                {
                }

                public event EventHandler? Changed
                {
                    {|SST2429:add|} { _handlers += OnFoo; }
                    remove { _handlers -= value; }
                }
            }
            """);

    /// <summary>Verifies a set accessor that reads value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SetReadsValueIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _h;

                public int Height { get => _h; set => _h = value; }
            }
            """);

    /// <summary>Verifies an empty set body is a deliberate no-op and left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptySetBodyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Height { get => 0; set { } }
            }
            """);

    /// <summary>Verifies a throw-only expression-bodied set is a deliberate refusal and left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowingSetExpressionIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public int Height { get => 0; set => throw new NotSupportedException(); }
            }
            """);

    /// <summary>Verifies a throw-only block-bodied set is a deliberate refusal and left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowingSetBlockIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public int Height { get => 0; set { throw new NotSupportedException(); } }
            }
            """);

    /// <summary>Verifies an auto-implemented accessor carries no body to scan and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Height { get; set; }
            }
            """);
}
