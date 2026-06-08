// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyWriteOnly = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.Sst1421WriteOnlyPropertyAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1421 (write-only properties should not be used).</summary>
public class WriteOnlyPropertyAnalyzerUnitTest
{
    /// <summary>Verifies set-only and init-only properties are reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task WriteOnlyPropertiesAreReportedAsync()
        => await VerifyWriteOnly.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _value;

                public int {|SST1421:SetOnly|} { set => _value = value; }

                public int {|SST1421:InitOnly|} { init => _value = value; }
            }

            namespace System.Runtime.CompilerServices
            {
                internal static class IsExternalInit { }
            }
            """);

    /// <summary>Verifies readable, overriding, and explicit-interface properties are clean.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ExemptPropertiesAreCleanAsync()
        => await VerifyWriteOnly.VerifyAnalyzerAsync(
            """
            public interface I
            {
                int Value { set; }
            }

            public abstract class B
            {
                public abstract int Value { set; }
            }

            public class C : B, I
            {
                private int _value;

                public int Normal { get => _value; set => _value = value; }

                public override int Value { set => _value = value; }

                int I.Value { set => _value = value; }
            }
            """);
}
