// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyObsoleteMessage = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2308ObsoleteWithoutExplanationAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2308 (obsolete attributes should explain what to use instead).</summary>
public class ObsoleteWithoutExplanationAnalyzerUnitTest
{
    /// <summary>Verifies a bare obsolete attribute is reported wherever it is written.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BareAttributeIsReportedAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            using System;

            [{|SST2308:Obsolete|}]
            public sealed class Legacy
            {
                [{|SST2308:Obsolete|}]
                public int Field;

                [{|SST2308:ObsoleteAttribute|}]
                public int Value { get; set; }

                [{|SST2308:System.Obsolete|}]
                public void Run()
                {
                }
            }
            """);

    /// <summary>Verifies the error flag is not a message: turning the warning into an error explains nothing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The attribute has no constructor taking the flag alone, so the only way to write the flag without
    /// explaining anything is to hand the message a null.
    /// </remarks>
    [Test]
    public async Task ErrorFlagWithoutMessageIsReportedAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                [{|SST2308:Obsolete(message: null, error: true)|}]
                public void Run()
                {
                }
            }
            """);

    /// <summary>Verifies a message that says nothing is reported like a missing one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlankMessageIsReportedAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private const string Nothing = "   ";

                [{|SST2308:Obsolete("")|}]
                public void Empty()
                {
                }

                [{|SST2308:Obsolete("   ")|}]
                public void Whitespace()
                {
                }

                [{|SST2308:Obsolete(null)|}]
                public void Null()
                {
                }

                [{|SST2308:Obsolete(Nothing, true)|}]
                public void Constant()
                {
                }
            }
            """);

    /// <summary>Verifies a message, however it is supplied, is what the rule is asking for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SuppliedMessageIsCleanAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            using System;

            public sealed class C
            {
                private const string Migration = "Use Run(RunOptions) instead.";

                [Obsolete("Use Run(RunOptions) instead.")]
                public void Positional()
                {
                }

                [Obsolete("Use Run(RunOptions) instead.", true)]
                public void WithError()
                {
                }

                [Obsolete(message: "Use Run(RunOptions) instead.")]
                public void Named()
                {
                }

                [Obsolete(Migration)]
                public void FromConstant()
                {
                }
            }
            """);

    /// <summary>Verifies a property initializer is not a message, so an attribute carrying only one is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyInitializerIsNotAMessageAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  [{|SST2308:Obsolete(DiagnosticId = "LEGACY001")|}]
                                  public void Run()
                                  {
                                  }

                                  [Obsolete("Use Run(RunOptions) instead.", DiagnosticId = "LEGACY002")]
                                  public void Explained()
                                  {
                                  }
                              }
                              """;
        var test = new VerifyObsoleteMessage.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an attribute of the same name that is not the framework's is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignObsoleteAttributeIsCleanAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            namespace Vendor
            {
                using System;

                [AttributeUsage(AttributeTargets.Method)]
                public sealed class ObsoleteAttribute : Attribute
                {
                }

                public sealed class C
                {
                    [Obsolete]
                    public void Run()
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies generated code is not asked to explain itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeIsCleanAsync()
    {
        const string Generated = """
                                 // <auto-generated/>
                                 using System;

                                 public sealed class Proxy
                                 {
                                     [Obsolete]
                                     public void Run()
                                     {
                                     }
                                 }
                                 """;
        var test = new VerifyObsoleteMessage.Test
        {
            TestState = { Sources = { ("Proxy.g.cs", Generated) } },
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the attribute is reported on the many kinds of declaration it can be written on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryAnnotatedDeclarationKindIsReportedAsync()
        => await VerifyObsoleteMessage.VerifyAnalyzerAsync(
            """
            using System;

            [{|SST2308:Obsolete|}]
            public delegate void Handler();

            public enum Level
            {
                [{|SST2308:Obsolete|}]
                Old,
                Current,
            }

            public sealed class C
            {
                [{|SST2308:Obsolete|}]
                public event Handler Changed;

                [{|SST2308:Obsolete|}]
                public C()
                {
                }

                [{|SST2308:Obsolete|}]
                public int this[int index] => index;

                public void Raise() => Changed?.Invoke();
            }
            """);
}
