// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyReturnedFromUsing = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2423DisposableReturnedFromUsingAnalyzer,
    StyleSharp.Analyzers.Sst2423DisposableReturnedFromUsingCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2423 (a disposable owned by a using is returned out of scope).</summary>
public class Sst2423DisposableReturnedFromUsingAnalyzerUnitTest
{
    /// <summary>A custom disposable used by every case.</summary>
    private const string Resource = """

        public sealed class Res : System.IDisposable
        {
            public string Name => string.Empty;

            public void Dispose()
            {
            }
        }
        """;

    /// <summary>A returned using declaration to be fixed.</summary>
    private const string ReturnedUsingSource = """
        public sealed class C
        {
            public Res M()
            {
                using var r = new Res();
                return {|SST2423:r|};
            }
        }
        """ + Resource;

    /// <summary>The method after the fix.</summary>
    private const string ReturnedUsingFixed = """
        public sealed class C
        {
            public Res M()
            {
                var r = new Res();
                return r;
            }
        }
        """ + Resource;

    /// <summary>Verifies returning a using-declaration local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationReturnReportedAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public Res M()
                {
                    using var r = new Res();
                    return {|SST2423:r|};
                }
            }
            """ + Resource);

    /// <summary>Verifies returning a using-statement local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingStatementReturnReportedAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public Res M()
                {
                    using (var r = new Res())
                    {
                        return {|SST2423:r|};
                    }
                }
            }
            """ + Resource);

    /// <summary>Verifies a using local returned inside a tuple is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingLocalInTupleReportedAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public (Res, int) M()
                {
                    using var r = new Res();
                    return ({|SST2423:r|}, 1);
                }
            }
            """ + Resource);

    /// <summary>Verifies a using local yielded out of an iterator is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingLocalYieldReturnedReportedAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            using System.Collections.Generic;

            public sealed class C
            {
                public IEnumerable<Res> M()
                {
                    using var r = new Res();
                    yield return {|SST2423:r|};
                }
            }
            """ + Resource);

    /// <summary>Verifies returning a member of the local, rather than the local, is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturningAMemberIsCleanAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public string M()
                {
                    using var r = new Res();
                    return r.Name;
                }
            }
            """ + Resource);

    /// <summary>Verifies returning a different object is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturningADifferentObjectIsCleanAsync()
        => await VerifyReturnedFromUsing.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public Res M()
                {
                    using var r = new Res();
                    r.Dispose();
                    return new Res();
                }
            }
            """ + Resource);

    /// <summary>Verifies the fix drops the using to transfer ownership to the caller.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UsingDeclarationFixedByTransferringOwnershipAsync()
        => await VerifyReturnedFromUsing.VerifyCodeFixAsync(ReturnedUsingSource, ReturnedUsingFixed);
}
