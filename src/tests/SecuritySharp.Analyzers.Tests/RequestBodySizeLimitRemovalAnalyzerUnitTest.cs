// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeLimit = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1505RequestBodySizeLimitRemovalAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1505 (the request body size limit must not be removed).</summary>
public class RequestBodySizeLimitRemovalAnalyzerUnitTest
{
    /// <summary>Inline stubs of the ASP.NET Core attribute and body-size-limit surfaces the rule gates on.</summary>
    private const string AspNetStubs = """

                                       namespace Microsoft.AspNetCore.Mvc
                                       {
                                           [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
                                           public sealed class DisableRequestSizeLimitAttribute : System.Attribute
                                           {
                                           }
                                       }

                                       namespace Microsoft.AspNetCore.Server.Kestrel.Core
                                       {
                                           public sealed class KestrelServerLimits
                                           {
                                               public long? MaxRequestBodySize { get; set; }
                                           }
                                       }

                                       namespace Microsoft.AspNetCore.Http.Features
                                       {
                                           public interface IHttpMaxRequestBodySizeFeature
                                           {
                                               long? MaxRequestBodySize { get; set; }
                                           }
                                       }
                                       """;

    /// <summary>Verifies <c>[DisableRequestSizeLimit]</c> on a controller class is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisableAttributeOnClassReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [{|SES1505:DisableRequestSizeLimit|}]
            public class UploadController
            {
            }
            """);

    /// <summary>Verifies <c>[DisableRequestSizeLimit]</c> on an action method is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisableAttributeOnMethodReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            public class UploadController
            {
                [{|SES1505:DisableRequestSizeLimit|}]
                public void Upload()
                {
                }
            }
            """);

    /// <summary>Verifies the full <c>DisableRequestSizeLimitAttribute</c> spelling is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DisableAttributeLongSpellingReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Mvc;

            [{|SES1505:DisableRequestSizeLimitAttribute|}]
            public class UploadController
            {
            }
            """);

    /// <summary>Verifies setting <c>KestrelServerLimits.MaxRequestBodySize</c> to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KestrelLimitAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Server.Kestrel.Core;

            public class C
            {
                public void M(KestrelServerLimits limits)
                {
                    {|SES1505:limits.MaxRequestBodySize = null|};
                }
            }
            """);

    /// <summary>Verifies the object-initializer form on KestrelServerLimits is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task KestrelLimitObjectInitializerReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Server.Kestrel.Core;

            public class C
            {
                public KestrelServerLimits M()
                    => new KestrelServerLimits { {|SES1505:MaxRequestBodySize = null|} };
            }
            """);

    /// <summary>Verifies setting <c>IHttpMaxRequestBodySizeFeature.MaxRequestBodySize</c> to null is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FeatureLimitAssignmentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Http.Features;

            public class C
            {
                public void M(IHttpMaxRequestBodySizeFeature feature)
                {
                    {|SES1505:feature.MaxRequestBodySize = null|};
                }
            }
            """);

    /// <summary>Verifies setting a finite numeric cap is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FiniteLimitIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.AspNetCore.Server.Kestrel.Core;

            public class C
            {
                public void M(KestrelServerLimits limits)
                {
                    limits.MaxRequestBodySize = 10_485_760;
                }
            }
            """);

    /// <summary>Verifies a same-named attribute on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedAttributeOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            using MyAttrs;

            namespace MyAttrs
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                public sealed class DisableRequestSizeLimitAttribute : System.Attribute
                {
                }
            }

            [DisableRequestSizeLimit]
            public class NotAController
            {
            }
            """);

    /// <summary>Verifies a same-named property on an unrelated type set to null is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedPropertyOnUnrelatedTypeIsCleanAsync()
        => await VerifyAsync(
            """
            public sealed class MyLimits
            {
                public long? MaxRequestBodySize { get; set; }
            }

            public class C
            {
                public void M(MyLimits limits)
                {
                    limits.MaxRequestBodySize = null;
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the ASP.NET surfaces are absent from the compilation.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenSurfacesUnavailableAsync()
    {
        const string Source = """
                              [System.AttributeUsage(System.AttributeTargets.Class)]
                              public sealed class DisableRequestSizeLimitAttribute : System.Attribute
                              {
                              }

                              public sealed class KestrelServerLimits
                              {
                                  public long? MaxRequestBodySize { get; set; }
                              }

                              [DisableRequestSizeLimit]
                              public class C
                              {
                                  public void M(KestrelServerLimits limits)
                                  {
                                      limits.MaxRequestBodySize = null;
                                  }
                              }
                              """;

        var test = new AnalyzeLimit.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification with the inline ASP.NET Core stubs appended.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeLimit.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + AspNetStubs
        };

        await test.RunAsync(CancellationToken.None);
    }
}
