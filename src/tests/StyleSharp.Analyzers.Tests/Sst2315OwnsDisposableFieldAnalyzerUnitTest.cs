// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOwnsDisposable = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2315OwnsDisposableFieldAnalyzer,
    StyleSharp.Analyzers.Sst2315OwnsDisposableFieldCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2315 (a type owns a disposable but is not IDisposable).</summary>
public class Sst2315OwnsDisposableFieldAnalyzerUnitTest
{
    /// <summary>A custom disposable used by every case, so the framework's stream special-casing never applies.</summary>
    private const string Resource = """

        public sealed class Res : System.IDisposable
        {
            public static Res Create() => new Res();

            public string Name => string.Empty;

            public void Dispose()
            {
            }
        }
        """;

    /// <summary>A factory-owned disposable field to be fixed.</summary>
    private const string FactoryFieldSource = """
        public sealed class {|SST2315:C|}
        {
            private readonly Res _r = Res.Create();
        }
        """ + Resource;

    /// <summary>The type after the fix.</summary>
    private const string FactoryFieldFixed = """
        public sealed class C : System.IDisposable
        {
            private readonly Res _r = Res.Create();

            public void Dispose()
            {
                _r.Dispose();
            }
        }
        """ + Resource;

    /// <summary>Verifies a field assigned from a static factory is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoryAssignedFieldReportedAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2315:C|}
            {
                private readonly Res _r = Res.Create();
            }
            """ + Resource);

    /// <summary>Verifies an auto-property initialized with new is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AutoPropertyNewReportedAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class {|SST2315:C|}
            {
                public Res R { get; } = new Res();
            }
            """ + Resource);

    /// <summary>Verifies a collection the type fills with new disposables is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionOfDisposablesReportedAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class {|SST2315:C|}
            {
                private readonly List<Res> _all = new();

                public void Add() => _all.Add(new Res());
            }
            """ + Resource);

    /// <summary>Verifies an injected disposable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InjectedFieldIsCleanAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly Res _r;

                public C(Res r) => _r = r;
            }
            """ + Resource);

    /// <summary>Verifies a field constructed directly with new is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NewAssignedFieldIsCleanAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                private readonly Res _r = new Res();
            }
            """ + Resource);

    /// <summary>Verifies a type that already implements IDisposable is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AlreadyDisposableIsCleanAsync()
        => await VerifyOwnsDisposable.VerifyAnalyzerAsync(
            """
            public sealed class C : System.IDisposable
            {
                private readonly Res _r = Res.Create();

                public void Dispose() => _r.Dispose();
            }
            """ + Resource);

    /// <summary>Verifies the fix adds IDisposable and a Dispose that releases the owned member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FactoryFieldFixedToDisposableAsync()
        => await VerifyOwnsDisposable.VerifyCodeFixAsync(FactoryFieldSource, FactoryFieldFixed);
}
