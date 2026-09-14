// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using RoslynCommon.Analyzers.Tests;

using AnalyzeFailOpen = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1508FailOpenValidationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1508 (a security-check method must not fail open by returning success from a catch).</summary>
public class FailOpenValidationAnalyzerUnitTest
{
    /// <summary>Checks success-expression and fall-through boundaries, including incomplete code.</summary>
    /// <param name="returnType">The security method's return type.</param>
    /// <param name="body">The method body.</param>
    /// <param name="expected">The expected diagnostic count.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("bool", "try { } catch { ; return ((true)); }", 1)]
    [Arguments("bool", "try { } catch { ; } ; return ((true));", 1)]
    [Arguments("bool", "if (value) try { } catch { } return true;", 0)]
    [Arguments("bool", "{ try { } catch { } } return true;", 0)]
    [Arguments("bool", "try { } catch { } value = true; return true;", 0)]
    [Arguments("bool", "try { } catch { return value; } return false;", 0)]
    [Arguments("bool", "try { } catch { return; } return false;", 0)]
    [Arguments("bool", "try { } catch { return GetValue(); } return false;", 0)]
    [Arguments("Task<bool>", "try { } catch { return Task.FromResult((true)); } return null;", 1)]
    [Arguments("System.Threading.Tasks.ValueTask<bool>", "try { } catch { return ValueTask.FromResult((true)); } return default;", 1)]
    [Arguments("Task<bool>", "try { } catch { return Task.FromResult(false); } return null;", 0)]
    [Arguments("Task<bool>", "try { } catch { return Task.FromResult(value); } return null;", 0)]
    [Arguments("Task<bool>", "try { } catch { return Task.FromResult(true, false); } return null;", 0)]
    [Arguments("Task<bool>", "try { } catch { return Task.Other(true); } return null;", 0)]
    [Arguments("Task<bool>", "try { } catch { return FromResult(true); } return null;", 0)]
    [Arguments("System.Boolean", "try { } catch { return true; } return false;", 0)]
    [Arguments("bool?", "try { } catch { return true; } return false;", 0)]
    [Arguments("Task<int>", "try { } catch { return Task.FromResult(true); } return null;", 0)]
    [Arguments("Task<System.Boolean>", "try { } catch { return Task.FromResult(true); } return null;", 0)]
    [Arguments("Task<bool, bool>", "try { } catch { return Task.FromResult(true); } return null;", 0)]
    [Arguments("Other<bool>", "try { } catch { return Task.FromResult(true); } return null;", 0)]
    public async Task OnlyRecognizedSuccessShapesReportAsync(string returnType, string body, int expected)
    {
        var source = $$"""
            using System.Threading.Tasks;
            class C { {{returnType}} EnsureValid(bool value) { {{body}} } }
            """;
        var compilation = CSharpCompilation.Create(nameof(OnlyRecognizedSuccessShapesReportAsync), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Ses1508FailOpenValidationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics.Length).IsEqualTo(expected);
        await Assert.That(diagnostics.All(static diagnostic => diagnostic.Id == "SES1508")).IsTrue();
    }

    /// <summary>Checks function boundaries and semantic exception near misses.</summary>
    /// <param name="source">The complete source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("try { } catch { return true; }")]
    [Arguments("class C { C() { try { } catch { } } }")]
    [Arguments("class C { bool Validate { get { try { } catch { return true; } return false; } } }")]
    [Arguments("class C { bool Validate() { bool Read() { try { } catch { return true; } return false; } return Read(); } }")]
    [Arguments("class C { bool Validate<T>() { try { } catch (T) { return true; } return false; } }")]
    [Arguments("class C { bool Validate() { try { } catch (Missing) { return true; } return false; } }")]
    [Arguments("class Exception : System.Exception {} class C { bool Validate() { try { } catch (Exception) { return true; } return false; } }")]
    [Arguments("class CryptographicException : System.Exception {} class C { bool Validate() { try { } catch (CryptographicException) { return true; } return false; } }")]
    [Arguments("class AuthenticationException : System.Exception {} class C { bool Validate() { try { } catch (AuthenticationException) { return true; } return false; } }")]
    [Arguments("class C { bool Validate() { System.Func<bool> run = delegate { try { } catch { return true; } return false; }; return run(); } }")]
    public async Task UnrelatedFunctionsAndExceptionTypesStayCleanAsync(string source)
    {
        var compilation = CSharpCompilation.Create(nameof(UnrelatedFunctionsAndExceptionTypesStayCleanAsync), [CSharpSyntaxTree.ParseText(source)], RuntimeMetadataReferences.Platform);
        var diagnostics = await compilation.WithAnalyzers([new Ses1508FailOpenValidationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        await Assert.That(diagnostics).IsEmpty();
    }

    /// <summary>Checks every security-check prefix with a filtered broad catch.</summary>
    /// <param name="name">The method name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    [Arguments("ValidateToken")]
    [Arguments("VerifyToken")]
    [Arguments("AuthenticateToken")]
    [Arguments("AuthorizeToken")]
    [Arguments("CheckToken")]
    [Arguments("IsValidToken")]
    [Arguments("IsAuthenticToken")]
    [Arguments("EnsureToken")]
    public Task SecurityPrefixesReportFilteredSuccessAsync(string name) =>
        VerifyNet90Async($$"""
            class C
            {
                bool {{name}}(bool filter)
                {
                    try { return false; }
                    {|SES1508:catch|} (System.Exception) when (filter) { return true; }
                }
            }
            """);

    /// <summary>Verifies a bool validator that catches <c>Exception</c> and returns true is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BroadCatchReturningTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyBareCatchFallingThroughToTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CryptographicExceptionReturningTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AsyncTaskOfBoolReturningTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SecurityTokenExceptionReturningTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task TaskFromResultTrueReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task LocalFunctionFailingOpenReportedAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task FailClosedReturningFalseIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonSecurityCheckMethodNameIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task CatchWithSideEffectBeforeReturnIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NarrowNonSecurityExceptionIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task RethrowingCatchIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonBoolReturnTypeIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task EmptyCatchFallingThroughToFalseIsCleanAsync() =>
        VerifyNet90Async(
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ReturnTrueFromLambdaInsideValidatorIsCleanAsync() =>
        VerifyNet90Async(
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
        var test = new AnalyzeFailOpen.Test { ReferenceAssemblies = ReferenceAssemblies.Net.Net90, TestCode = source };

        await test.RunAsync(CancellationToken.None);
    }
}
