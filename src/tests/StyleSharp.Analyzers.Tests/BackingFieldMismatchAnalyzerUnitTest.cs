// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFix = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2422BackingFieldMismatchAnalyzer,
    StyleSharp.Analyzers.Sst2422BackingFieldMismatchCodeFixProvider>;
using VerifyMismatch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2422BackingFieldMismatchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2422 (a property getter reads a different field than its setter writes).</summary>
public class BackingFieldMismatchAnalyzerUnitTest
{
    /// <summary>A property whose accessors use two different fields.</summary>
    private const string MismatchSource = """
        public sealed class C
        {
            private int _width;
            private int _height;

            public int {|SST2422:Width|}
            {
                get => _height;
                set => _width = value;
            }
        }
        """;

    /// <summary>The property after pointing the getter at the setter's field.</summary>
    private const string MismatchFixed = """
        public sealed class C
        {
            private int _width;
            private int _height;

            public int Width
            {
                get => _width;
                set => _width = value;
            }
        }
        """;

    /// <summary>Verifies a getter and setter using different fields is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MismatchedFieldsIsReportedAsync()
        => await VerifyMismatch.VerifyAnalyzerAsync(MismatchSource);

    /// <summary>Verifies a property that round-trips one field is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameFieldIsCleanAsync()
        => await VerifyMismatch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _width;

                public int Width
                {
                    get => _width;
                    set => _width = value;
                }
            }
            """);

    /// <summary>Verifies the change-notification shape, which round-trips one field, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ChangeNotificationShapeIsCleanAsync()
        => await VerifyMismatch.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private int _width;

                public event EventHandler Changed;

                public int Width
                {
                    get => _width;
                    set
                    {
                        if (_width == value)
                        {
                            return;
                        }

                        _width = value;
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            """);

    /// <summary>Verifies a computed getter is clean: it does not reduce to one field read.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ComputedGetterIsCleanAsync()
        => await VerifyMismatch.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private int _width;
                private int _height;

                public int Area
                {
                    get => _width * _height;
                    set => _width = value;
                }
            }
            """);

    /// <summary>Verifies the fix points the getter at the setter's field.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixPointsGetterAtSetterFieldAsync()
        => await VerifyFix.VerifyCodeFixAsync(MismatchSource, MismatchFixed);
}
