// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyDiagnosticId = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2314ObsoleteWithoutDiagnosticIdAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2314 (obsolete attributes should carry a DiagnosticId).</summary>
public class ObsoleteWithoutDiagnosticIdAnalyzerUnitTest
{
    /// <summary>Verifies an explained deprecation with no id is reported: the caller cannot act on it alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MessageWithoutDiagnosticIdIsReportedAsync()
        => await VerifyOnNet80Async(
            """
            using System;

            [{|SST2314:Obsolete("Use Mailer instead.")|}]
            public sealed class Legacy
            {
                [{|SST2314:Obsolete("Use SendAsync(Message).")|}]
                public void Send()
                {
                }

                [{|SST2314:Obsolete("Gone in 5.0.", true)|}]
                public void Broadcast()
                {
                }

                [{|SST2314:Obsolete("Use SendAsync(Message).", UrlFormat = "https://acme.dev/{0}")|}]
                public void Publish()
                {
                }
            }
            """);

    /// <summary>Verifies a deprecation carrying an id is what the rule is asking for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiagnosticIdIsCleanAsync()
        => await VerifyOnNet80Async(
            """
            using System;

            public sealed class Legacy
            {
                [Obsolete("Use SendAsync(Message).", DiagnosticId = "ACME001")]
                public void Send()
                {
                }

                [Obsolete("Use SendAsync(Message).", DiagnosticId = "ACME002", UrlFormat = "https://acme.dev/deprecations/{0}")]
                public void Publish()
                {
                }

                [Obsolete("Gone in 5.0.", true, DiagnosticId = "ACME003")]
                public void Broadcast()
                {
                }
            }
            """);

    /// <summary>Verifies an attribute with no message at all is left to SST2308, so the two never both report.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AttributeWithoutMessageIsLeftToSst2308Async()
        => await VerifyOnNet80Async(
            """
            using System;

            public sealed class Legacy
            {
                [Obsolete]
                public void Bare()
                {
                }

                [Obsolete("")]
                public void Empty()
                {
                }

                [Obsolete(null)]
                public void Null()
                {
                }
            }
            """);

    /// <summary>Verifies the rule stays silent on a target whose ObsoleteAttribute has no DiagnosticId.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The property arrived in .NET 5. On netstandard2.0 there is nothing to set, so a rule that reported here
    /// would be asking for code that cannot compile — it registers nothing instead.
    /// </remarks>
    [Test]
    public async Task TargetWithoutDiagnosticIdPropertyIsCleanAsync()
    {
        const string Source = """
                              using System;

                              public sealed class Legacy
                              {
                                  [Obsolete("Use SendAsync(Message).")]
                                  public void Send()
                                  {
                                  }
                              }
                              """;
        var test = new VerifyDiagnosticId.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an attribute of the same name that is not the framework's is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignObsoleteAttributeIsCleanAsync()
        => await VerifyOnNet80Async(
            """
            namespace Vendor
            {
                using System;

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObsoleteAttribute : Attribute
                {
                    public ObsoleteAttribute(string message) => Message = message;

                    public string Message { get; }
                }

                public sealed class C
                {
                    [Obsolete("not the framework's")]
                    public void Run()
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies generated code is not asked for an id.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeIsCleanAsync()
    {
        const string Generated = """
                                 // <auto-generated/>
                                 using System;

                                 public sealed class Proxy
                                 {
                                     [Obsolete("Use SendAsync(Message).")]
                                     public void Send()
                                     {
                                     }
                                 }
                                 """;
        var test = new VerifyDiagnosticId.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestState = { Sources = { ("Proxy.g.cs", Generated) } },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a source against reference assemblies whose ObsoleteAttribute has a DiagnosticId.</summary>
    /// <param name="source">The test source, with markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOnNet80Async(string source)
    {
        var test = new VerifyDiagnosticId.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
