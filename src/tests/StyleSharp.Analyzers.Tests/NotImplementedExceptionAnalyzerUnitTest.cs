// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyNotImplemented = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2485NotImplementedExceptionAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2485 (a NotImplementedException left in shipped code).</summary>
public class NotImplementedExceptionAnalyzerUnitTest
{
    /// <summary>Verifies a throw statement of a new NotImplementedException is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowStatementIsReportedAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public int M()
                {
                    throw {|SST2485:new NotImplementedException()|};
                }
            }
            """);

    /// <summary>Verifies a NotImplementedException with a message argument is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowWithMessageIsReportedAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public int M() => throw {|SST2485:new NotImplementedException("later")|};
            }
            """);

    /// <summary>Verifies a throw expression is reported like a throw statement.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowExpressionIsReportedAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public string M(string? value) => value ?? throw {|SST2485:new NotImplementedException()|};
            }
            """);

    /// <summary>Verifies a fully qualified System.NotImplementedException is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedNotImplementedExceptionIsReportedAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M() => throw {|SST2485:new System.NotImplementedException()|};
            }
            """);

    /// <summary>Verifies an alias-qualified <c>global::System.NotImplementedException</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AliasQualifiedNotImplementedExceptionIsReportedAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            public sealed class C
            {
                public int M() => throw {|SST2485:new global::System.NotImplementedException()|};
            }
            """);

    /// <summary>Verifies a NotSupportedException is left alone as a deliberate, permanent signal.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NotSupportedExceptionIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public int M() => throw new NotSupportedException("read-only");
            }
            """);

    /// <summary>Verifies a specific, meaningful exception type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SpecificExceptionIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public sealed class C
            {
                public void M(string? value)
                {
                    if (value is null)
                    {
                        throw new ArgumentNullException(nameof(value));
                    }
                }
            }
            """);

    /// <summary>Verifies a rethrow is not the creation of a new exception.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(Action work)
                {
                    try
                    {
                        work();
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                }
            }
            """);

    /// <summary>Verifies throwing an already-built exception value is not a new-object creation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowOfExistingValueIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                public void M(NotImplementedException error)
                {
                    throw error;
                }
            }
            """);

    /// <summary>Verifies a project type that merely shares the name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The bind confirms the framework's type; a same-named type of the project's own is a different symbol.</remarks>
    [Test]
    public async Task ProjectTypeSharingNameIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            namespace Contoso
            {
                public sealed class NotImplementedException : System.Exception
                {
                }

                public sealed class C
                {
                    public void M() => throw new NotImplementedException();
                }
            }
            """);

    /// <summary>Verifies a generic exception type is not treated as the framework's NotImplementedException.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GenericExceptionTypeIsCleanAsync()
        => await VerifyNotImplemented.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class StubException<T> : Exception
            {
            }

            public sealed class C
            {
                public void M() => throw new StubException<int>();
            }
            """);
}
