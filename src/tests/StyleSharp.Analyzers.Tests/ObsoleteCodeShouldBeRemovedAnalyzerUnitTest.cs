// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyObsoleteRemoval = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2310ObsoleteCodeShouldBeRemovedAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2310 (deprecated code should be removed).</summary>
public class ObsoleteCodeShouldBeRemovedAnalyzerUnitTest
{
    /// <summary>Verifies a message does not exempt the attribute: the rule wants the code gone, not explained.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExplainedDeprecationIsStillReportedAsync()
        => await VerifyObsoleteRemoval.VerifyAnalyzerAsync(
            """
            using System;

            [{|SST2310:Obsolete("Use Mailer instead.")|}]
            public sealed class Legacy
            {
                [{|SST2310:Obsolete|}]
                public void Bare()
                {
                }

                [{|SST2310:Obsolete("Use SendAsync(Message).")|}]
                public void Explained()
                {
                }

                [{|SST2310:Obsolete("Gone in 5.0.", true)|}]
                public void AsError()
                {
                }
            }
            """);

    /// <summary>Verifies a fully specified, modern deprecation is still a reminder to delete the code.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DiagnosticIdDoesNotExemptAsync()
    {
        const string Source = """
                              using System;

                              public sealed class C
                              {
                                  [{|SST2310:Obsolete("Use SendAsync(Message).", DiagnosticId = "ACME001", UrlFormat = "https://acme.dev/{0}")|}]
                                  public void Send()
                                  {
                                  }
                              }
                              """;
        var test = new VerifyObsoleteRemoval.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the attribute is reported on every kind of declaration it can be written on.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EveryAnnotatedDeclarationKindIsReportedAsync()
    {
        const string Source = """
                              using System;

                              [{|SST2310:Obsolete("gone")|}]
                              public delegate void Handler();

                              [{|SST2310:Obsolete("gone")|}]
                              public interface ILegacy;

                              [{|SST2310:Obsolete("gone")|}]
                              public struct LegacyValue;

                              [{|SST2310:Obsolete("gone")|}]
                              public record LegacyRecord(int Value);

                              public enum Level
                              {
                                  [{|SST2310:Obsolete("gone")|}]
                                  Old,
                                  Current,
                              }

                              public sealed class C
                              {
                                  [{|SST2310:Obsolete("gone")|}]
                                  public int Field;

                                  [{|SST2310:Obsolete("gone")|}]
                                  public int Value { get; set; }

                                  [{|SST2310:Obsolete("gone")|}]
                                  public event Handler Changed;

                                  [{|SST2310:Obsolete("gone")|}]
                                  public C()
                                  {
                                  }

                                  [{|SST2310:Obsolete("gone")|}]
                                  public int this[int index] => index;

                                  public void Raise() => Changed?.Invoke();
                              }
                              """;

        // Records need IsExternalInit, which the default reference assemblies do not carry.
        var test = new VerifyObsoleteRemoval.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies an override is reported: the author wrote the attribute there and can delete it there.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Unlike a rule that demands a different member shape, this one asks for the attribute — and the code under
    /// it — to go away, which is something the overriding type can do on its own.
    /// </remarks>
    [Test]
    public async Task OverrideIsReportedAsync()
        => await VerifyObsoleteRemoval.VerifyAnalyzerAsync(
            """
            using System;

            public abstract class Base
            {
                [{|SST2310:Obsolete("Use Run(RunOptions).")|}]
                public virtual void Run()
                {
                }
            }

            public sealed class Derived : Base
            {
                [{|SST2310:Obsolete("Use Run(RunOptions).")|}]
                public override void Run()
                {
                }
            }
            """);

    /// <summary>Verifies an attribute of the same name that is not the framework's is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForeignObsoleteAttributeIsCleanAsync()
        => await VerifyObsoleteRemoval.VerifyAnalyzerAsync(
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

    /// <summary>Verifies code carrying no deprecation is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CurrentCodeIsCleanAsync()
        => await VerifyObsoleteRemoval.VerifyAnalyzerAsync(
            """
            using System;

            [Serializable]
            public sealed class C
            {
                public void Run()
                {
                }
            }
            """);

    /// <summary>Verifies generated code is not asked to retire itself.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedCodeIsCleanAsync()
    {
        const string Generated = """
                                 // <auto-generated/>
                                 using System;

                                 public sealed class Proxy
                                 {
                                     [Obsolete("gone")]
                                     public void Run()
                                     {
                                     }
                                 }
                                 """;
        var test = new VerifyObsoleteRemoval.Test
        {
            TestState = { Sources = { ("Proxy.g.cs", Generated) } },
        };

        await test.RunAsync(CancellationToken.None);
    }
}
