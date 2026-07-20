// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeDiscard = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1513DiscardedAuthorizationResultAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1513 (an authorization result must be checked, not discarded).</summary>
public class DiscardedAuthorizationResultAnalyzerUnitTest
{
    /// <summary>Inline stubs for the ASP.NET Core authorization types the rule binds to.</summary>
    private const string AuthorizationStubs = """
        namespace Microsoft.AspNetCore.Authorization
        {
            using System.Collections.Generic;
            using System.Security.Claims;
            using System.Threading.Tasks;

            public class AuthorizationResult
            {
                public bool Succeeded { get; }
            }

            public interface IAuthorizationRequirement
            {
            }

            public interface IAuthorizationService
            {
                Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements);

                Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName);
            }

            public static class AuthorizationServiceExtensions
            {
                public static Task<AuthorizationResult> AuthorizeAsync(this IAuthorizationService service, ClaimsPrincipal user, string policyName)
                    => service.AuthorizeAsync(user, null, policyName);
            }
        }

        """;

    /// <summary>Verifies an awaited call used as an expression statement is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AwaitedExpressionStatementReportedAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        await {|SES1513:_authz.AuthorizeAsync(user, resource, "CanDelete")|};
                    }
                }
            }
            """);

    /// <summary>Verifies an awaited call assigned to a discard is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiscardAssignmentReportedAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        _ = await {|SES1513:_authz.AuthorizeAsync(user, resource, "CanDelete")|};
                    }
                }
            }
            """);

    /// <summary>Verifies a discarded call wrapped in <c>.ConfigureAwait(false)</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConfigureAwaitedDiscardReportedAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        await {|SES1513:_authz.AuthorizeAsync(user, resource, "CanDelete")|}.ConfigureAwait(false);
                    }
                }
            }
            """);

    /// <summary>Verifies the convenience policy-name extension overload is reported when discarded.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExtensionOverloadDiscardReportedAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user)
                    {
                        await {|SES1513:_authz.AuthorizeAsync(user, "CanDelete")|};
                    }
                }
            }
            """);

    /// <summary>Verifies a non-awaited fire-and-forget call whose task is discarded is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAwaitedFireAndForgetReportedAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public void Delete(ClaimsPrincipal user, object resource)
                    {
                        {|SES1513:_authz.AuthorizeAsync(user, resource, "CanDelete")|};
                    }
                }
            }
            """);

    /// <summary>Verifies a result stored in a variable and later branched on is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResultStoredAndCheckedIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        var result = await _authz.AuthorizeAsync(user, resource, "CanDelete");
                        if (!result.Succeeded)
                        {
                            return;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies reading <c>.Succeeded</c> inline is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SucceededReadInlineIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        if ((await _authz.AuthorizeAsync(user, resource, "CanDelete")).Succeeded)
                        {
                            return;
                        }
                    }
                }
            }
            """);

    /// <summary>Verifies a returned result is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ReturnedResultIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource)
                        => _authz.AuthorizeAsync(user, resource, "CanDelete");
                }
            }
            """);

    /// <summary>Verifies a result passed as an argument is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ResultPassedAsArgumentIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                        => Record(await _authz.AuthorizeAsync(user, resource, "CanDelete"));

                    private static void Record(AuthorizationResult result)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies an assignment to a real variable named <c>_</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AssignmentToRealUnderscoreVariableIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using Microsoft.AspNetCore.Authorization;
                using System.Security.Claims;
                using System.Threading.Tasks;

                public class Handler
                {
                    private readonly IAuthorizationService _authz;
                    private AuthorizationResult _;

                    public Handler(IAuthorizationService authz) => _authz = authz;

                    public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                    {
                        _ = await _authz.AuthorizeAsync(user, resource, "CanDelete");
                    }
                }
            }
            """);

    /// <summary>Verifies an unrelated <c>AuthorizeAsync</c> not on the service is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedAuthorizeAsyncIsCleanAsync()
        => await VerifyAsync(
            """
            namespace App
            {
                using System.Threading.Tasks;

                public sealed class CustomGate
                {
                    public Task AuthorizeAsync(string action) => Task.CompletedTask;
                }

                public class Handler
                {
                    public async Task DoAsync()
                    {
                        var gate = new CustomGate();
                        await gate.AuthorizeAsync("delete");
                    }
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the authorization service type is unavailable.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenAuthorizationServiceUnavailableAsync()
    {
        // The service lives in an unrelated namespace, so the 'Microsoft.AspNetCore.Authorization.IAuthorizationService'
        // marker type does not resolve and nothing is registered.
        const string Source = """
                              namespace Other
                              {
                                  using System.Security.Claims;
                                  using System.Threading.Tasks;

                                  public class AuthorizationResult
                                  {
                                      public bool Succeeded { get; }
                                  }

                                  public interface IAuthorizationService
                                  {
                                      Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName);
                                  }

                                  public class Handler
                                  {
                                      private readonly IAuthorizationService _authz;

                                      public Handler(IAuthorizationService authz) => _authz = authz;

                                      public async Task DeleteAsync(ClaimsPrincipal user, object resource)
                                      {
                                          await _authz.AuthorizeAsync(user, resource, "CanDelete");
                                      }
                                  }
                              }
                              """;

        var test = new AnalyzeDiscard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the authorization stubs prepended.</summary>
    /// <param name="consumer">The consumer source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string consumer)
    {
        var test = new AnalyzeDiscard.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = AuthorizationStubs + consumer
        };

        await test.RunAsync(CancellationToken.None);
    }
}
