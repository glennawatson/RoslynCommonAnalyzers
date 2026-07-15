// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDisposeInterface = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2316DisposeWithoutInterfaceAnalyzer,
    StyleSharp.Analyzers.Sst2316DisposeWithoutInterfaceCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2316 (a Dispose/DisposeAsync method with no matching interface).</summary>
public class Sst2316DisposeWithoutInterfaceAnalyzerUnitTest
{
    /// <summary>A Dispose method with no interface to be fixed.</summary>
    private const string DisposeSource = """
        public class C
        {
            public void {|SST2316:Dispose|}()
            {
            }
        }
        """;

    /// <summary>The type after the fix.</summary>
    private const string DisposeFixed = """
        public class C : System.IDisposable
        {
            public void Dispose()
            {
            }
        }
        """;

    /// <summary>Verifies a Dispose method with no IDisposable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeWithoutInterfaceReportedAsync()
        => await VerifyDisposeInterface.VerifyAnalyzerAsync(
            """
            public class C
            {
                public void {|SST2316:Dispose|}()
                {
                }
            }
            """);

    /// <summary>Verifies a DisposeAsync method with no IAsyncDisposable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeAsyncWithoutInterfaceReportedAsync()
        => await VerifyDisposeInterface.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public class C
            {
                public ValueTask {|SST2316:DisposeAsync|}() => default;
            }
            """);

    /// <summary>Verifies a ref struct with a pattern Dispose is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RefStructDisposeIsCleanAsync()
        => await VerifyDisposeInterface.VerifyAnalyzerAsync(
            """
            public ref struct C
            {
                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies a type that implements IDisposable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ImplementsDisposableIsCleanAsync()
        => await VerifyDisposeInterface.VerifyAnalyzerAsync(
            """
            public class C : System.IDisposable
            {
                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies a duck-typed enumerator with a Dispose is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuckTypedEnumeratorIsCleanAsync()
        => await VerifyDisposeInterface.VerifyAnalyzerAsync(
            """
            public class C
            {
                public int Current => 0;

                public bool MoveNext() => false;

                public void Dispose()
                {
                }
            }
            """);

    /// <summary>Verifies the fix adds the IDisposable interface.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisposeFixedByAddingInterfaceAsync()
        => await VerifyDisposeInterface.VerifyCodeFixAsync(DisposeSource, DisposeFixed);
}
