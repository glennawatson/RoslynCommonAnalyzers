// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeGuidSecret = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1004GuidAsSecretAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1004 (a secret must not be produced from <c>Guid.NewGuid()</c>).</summary>
public class GuidAsSecretAnalyzerUnitTest
{
    /// <summary>Verifies a GUID string stored in a secret-named local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToSecretNamedLocalReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string M()
                {
                    var token = {|SES1004:Guid.NewGuid()|}.ToString();
                    return token;
                }
            }
            """);

    /// <summary>Verifies a bare (non-stringified) GUID stored in a secret-named local is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareGuidToSecretNamedLocalReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public Guid M()
                {
                    Guid resetToken = {|SES1004:Guid.NewGuid()|};
                    return resetToken;
                }
            }
            """);

    /// <summary>Verifies a GUID stored in a secret-named field via a <c>ToString("N")</c> wrapper is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToSecretNamedFieldReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private readonly string _apiKey = {|SES1004:Guid.NewGuid()|}.ToString("N");
            }
            """);

    /// <summary>Verifies an all-caps acronym field name (<c>APIKEY</c>) is matched as a whole word.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToAllCapsSecretNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private readonly string APIKEY = {|SES1004:Guid.NewGuid()|}.ToString();
            }
            """);

    /// <summary>Verifies an acronym-cased field name (<c>APIKey</c>) is split into api/key and matched.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToAcronymCasedSecretNameReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private readonly string APIKey = {|SES1004:Guid.NewGuid()|}.ToString();
            }
            """);

    /// <summary>Verifies a GUID stored in a secret-named auto-property initializer is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToSecretNamedPropertyInitializerReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string Secret { get; } = {|SES1004:Guid.NewGuid()|}.ToString();
            }
            """);

    /// <summary>Verifies a GUID assigned to a secret-named member is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidAssignedToSecretNamedMemberReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                private string _sessionId = "";

                public void M() => this._sessionId = {|SES1004:Guid.NewGuid()|}.ToString();
            }
            """);

    /// <summary>Verifies a GUID assigned to a secret-named local through a digit-bearing name is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidAssignedToSecretNamedLocalReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    string passwordHash2;
                    passwordHash2 = {|SES1004:Guid.NewGuid()|}.ToString();
                    _ = passwordHash2;
                }
            }
            """);

    /// <summary>Verifies a GUID returned from a secret-named method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidReturnedFromSecretNamedMethodReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string CreatePassword()
                {
                    return {|SES1004:Guid.NewGuid()|}.ToString();
                }
            }
            """);

    /// <summary>Verifies a GUID returned from a secret-named local function is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidReturnedFromSecretNamedLocalFunctionReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string M()
                {
                    string GenerateOtp() => {|SES1004:Guid.NewGuid()|}.ToString();
                    return GenerateOtp();
                }
            }
            """);

    /// <summary>Verifies a GUID returned from a secret-named getter block is reported (attributed to the property).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidReturnedFromSecretNamedGetterReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string Nonce
                {
                    get { return {|SES1004:Guid.NewGuid()|}.ToString(); }
                }
            }
            """);

    /// <summary>Verifies a GUID as the body of a secret-named expression-bodied property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidAsExpressionBodiedSecretPropertyReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string Salt => {|SES1004:Guid.NewGuid()|}.ToString();
            }
            """);

    /// <summary>Verifies a GUID passed to a secret-named parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidPassedToSecretNamedParameterReportedAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void Store(string verificationCode)
                {
                }

                public void M() => Store({|SES1004:Guid.NewGuid()|}.ToString());
            }
            """);

    /// <summary>Verifies a GUID stored in a non-secret local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToNonSecretLocalIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public Guid M()
                {
                    var id = Guid.NewGuid();
                    return id;
                }
            }
            """);

    /// <summary>Verifies names that merely contain a secret substring across a boundary are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidToWordBoundaryNonMatchesIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    var tokenizer = Guid.NewGuid();
                    var correlationId = Guid.NewGuid();
                    var notProcessed = Guid.NewGuid();
                    var secretary = Guid.NewGuid();
                    var sessionValue = Guid.NewGuid();
                    _ = (tokenizer, correlationId, notProcessed, secretary, sessionValue);
                }
            }
            """);

    /// <summary>Verifies a same-named factory on an unrelated type is not reported (only System.Guid counts).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonGuidNewGuidIsCleanAsync()
        => await VerifyNet90Async(
            """
            public sealed class Ticket
            {
                public static Ticket NewGuid() => new Ticket();
            }

            public class C
            {
                public Ticket M()
                {
                    var token = Ticket.NewGuid();
                    return token;
                }
            }
            """);

    /// <summary>Verifies the empty <c>new Guid()</c> constructor into a secret-named local is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyGuidConstructorIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public Guid M()
                {
                    Guid token = new Guid();
                    return token;
                }
            }
            """);

    /// <summary>Verifies a GUID passed to a non-secret parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidPassedToNonSecretParameterIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void Log(string message)
                {
                }

                public void M() => Log(Guid.NewGuid().ToString());
            }
            """);

    /// <summary>Verifies a GUID assigned into a collection element (no simple target name) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidAssignedToElementIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M(string[] tokens)
                {
                    tokens[0] = Guid.NewGuid().ToString();
                }
            }
            """);

    /// <summary>Verifies a GUID as a named tuple element is not reported (tuple naming is not a sink here).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidInNamedTupleElementIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    var pair = (token: Guid.NewGuid().ToString(), count: 2);
                    _ = pair;
                }
            }
            """);

    /// <summary>Verifies a GUID returned from a lambda body is not attributed to the enclosing member.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidReturnedFromLambdaIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public string CreateToken()
                {
                    Func<string> factory = () => { return Guid.NewGuid().ToString(); };
                    return factory();
                }
            }
            """);

    /// <summary>Verifies a compound assignment (append) to a secret-named local is not reported (only fresh assignment counts).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidCompoundAssignedToSecretNameIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    var token = "";
                    token += Guid.NewGuid().ToString();
                    _ = token;
                }
            }
            """);

    /// <summary>Verifies a GUID in an expression-bodied conversion operator is not reported (no member sink name).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidInConversionOperatorIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public sealed class Widget
            {
                public static implicit operator string(Widget widget) => Guid.NewGuid().ToString();
            }
            """);

    /// <summary>Verifies a bare <c>Guid.NewGuid()</c> expression statement is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareGuidExpressionStatementIsCleanAsync()
        => await VerifyNet90Async(
            """
            using System;

            public class C
            {
                public void M()
                {
                    Guid.NewGuid();
                    _ = Guid.NewGuid().GetHashCode();
                }
            }
            """);

    /// <summary>Verifies a non-constant GUID default on a secret-named parameter is not reported (broken code stays silent).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidAsSecretNamedParameterDefaultIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public void M(Guid token = Guid.NewGuid())
                                  {
                                      _ = token;
                                  }
                              }
                              """;

        // The GUID default cannot be a compile-time constant, so the code does not compile; the analyzer
        // must not fault on it. Compiler diagnostics are ignored so the shape can be exercised in isolation.
        var test = new AnalyzeGuidSecret.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source,
            CompilerDiagnostics = CompilerDiagnostics.None
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the rule stays silent when RandomNumberGenerator is absent (netstandard1.2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenRandomNumberGeneratorUnavailableAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  public string M()
                                  {
                                      var token = Guid.NewGuid().ToString();
                                      return token;
                                  }
                              }
                              """;

        var test = new AnalyzeGuidSecret.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard12,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies (where RandomNumberGenerator exists).</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet90Async(string source)
    {
        var test = new AnalyzeGuidSecret.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
