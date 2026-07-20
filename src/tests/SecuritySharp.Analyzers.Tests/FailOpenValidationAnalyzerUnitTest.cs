// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeFailOpen = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1508FailOpenValidationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1508 (a security-check method must not fail open by returning success from a catch).</summary>
public class FailOpenValidationAnalyzerUnitTest
{
    /// <summary>Verifies a bool validator that catches <c>Exception</c> and returns true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BroadCatchReturningTrueReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool ValidateToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    {|SES1508:catch|} (System.Exception)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a bare empty catch that falls through to a trailing <c>return true;</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBareCatchFallingThroughToTrueReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool VerifySignature(byte[] data)
                {
                    try
                    {
                        return data.Length > 0;
                    }
                    {|SES1508:catch|}
                    {
                    }

                    return true;
                }
            }
            """);

    /// <summary>Verifies a validator catching <c>CryptographicException</c> and returning true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CryptographicExceptionReturningTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Cryptography;

            public class C
            {
                public bool Authenticate(string user)
                {
                    try
                    {
                        return user.Length > 0;
                    }
                    {|SES1508:catch|} (CryptographicException)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies an async <c>Task&lt;bool&gt;</c> validator catching <c>AuthenticationException</c> and returning true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncTaskOfBoolReturningTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Security.Authentication;
            using System.Threading.Tasks;

            public class C
            {
                public async Task<bool> IsValidAsync(string s)
                {
                    try
                    {
                        await Task.Yield();
                        return s.Length > 0;
                    }
                    {|SES1508:catch|} (AuthenticationException)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a validator catching a <c>*SecurityTokenException</c> type and returning true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SecurityTokenExceptionReturningTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class AppSecurityTokenException : Exception
            {
            }

            public class C
            {
                public bool VerifyJwt(string jwt)
                {
                    try
                    {
                        return jwt.Length > 0;
                    }
                    {|SES1508:catch|} (AppSecurityTokenException)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a non-async <c>Task&lt;bool&gt;</c> validator returning <c>Task.FromResult(true)</c> from a catch is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TaskFromResultTrueReportedAsync()
        => await VerifyNet90Async(
            """
            using System.Threading.Tasks;

            public class C
            {
                public Task<bool> ValidatePayload(byte[] payload)
                {
                    try
                    {
                        return Task.FromResult(payload.Length > 0);
                    }
                    {|SES1508:catch|} (System.Exception)
                    {
                        return Task.FromResult(true);
                    }
                }
            }
            """);

    /// <summary>Verifies a bool local function that fails open is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LocalFunctionFailingOpenReportedAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool M(string token)
                {
                    return CheckToken(token);

                    bool CheckToken(string t)
                    {
                        try
                        {
                            return t.Length > 0;
                        }
                        {|SES1508:catch|} (System.Exception)
                        {
                            return true;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a fail-closed catch (returning false) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FailClosedReturningFalseIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool ValidateToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    catch (System.Exception)
                    {
                        return false;
                    }
                }
            }
            """);

    /// <summary>Verifies a catch returning true in a method without a security-check name is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSecurityCheckMethodNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool ProcessToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    catch (System.Exception)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a catch that logs before returning true (two statements) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CatchWithSideEffectBeforeReturnIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public bool ValidateToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a validator catching a narrow, non-security exception and returning true is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NarrowNonSecurityExceptionIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public bool ValidateToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    catch (FormatException)
                    {
                        return true;
                    }
                }
            }
            """);

    /// <summary>Verifies a catch that rethrows is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RethrowingCatchIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool ValidateToken(string token)
                {
                    try
                    {
                        return token.Length > 0;
                    }
                    catch (System.Exception)
                    {
                        throw;
                    }
                }
            }
            """);

    /// <summary>Verifies a non-bool return type (a security-check name that returns void) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonBoolReturnTypeIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public void ValidateToken(string token)
                {
                    try
                    {
                        System.Console.WriteLine(token);
                    }
                    catch (System.Exception)
                    {
                        return;
                    }
                }
            }
            """);

    /// <summary>Verifies an empty catch that falls through to a trailing <c>return false;</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyCatchFallingThroughToFalseIsCleanAsync()
        => await VerifyNet90Async(
            """
            public class C
            {
                public bool VerifySignature(byte[] data)
                {
                    try
                    {
                        return data.Length > 0;
                    }
                    catch (System.Exception)
                    {
                    }

                    return false;
                }
            }
            """);

    /// <summary>Verifies a catch that returns true from a lambda inside a validator is not reported (the return leaves the lambda).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnTrueFromLambdaInsideValidatorIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public bool ValidateAll(string token)
                {
                    Func<bool> probe = () =>
                    {
                        try
                        {
                            return token.Length > 0;
                        }
                        catch (Exception)
                        {
                            return true;
                        }
                    };

                    return probe();
                }
            }
            """);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeFailOpen.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
