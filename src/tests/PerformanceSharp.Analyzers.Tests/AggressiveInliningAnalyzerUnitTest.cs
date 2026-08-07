// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1410AggressiveInliningAnalyzer,
    PerformanceSharp.Analyzers.Psh1410AggressiveInliningCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1410AggressiveInliningAnalyzer"/> (PSH1410 aggressive inlining, opt-in).</summary>
public class AggressiveInliningAnalyzerUnitTest
{
    /// <summary>The editorconfig that opts into the disabled-by-default rule.</summary>
    private const string OptInConfig = """
        root = true

        [*.cs]
        dotnet_diagnostic.PSH1410.severity = warning
        """;

    /// <summary>Verifies a trivial forwarder is flagged and gains the attribute.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TrivialForwarderIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Runtime.CompilerServices;

                              public class C
                              {
                                  private readonly int _value;

                                  public C(int value) => _value = value;

                                  public int {|PSH1410:GetValue|}() => _value;
                              }
                              """;
        const string FixedSource = """
                                   using System.Runtime.CompilerServices;

                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;

                                       [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                                       public int GetValue() => _value;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a file with no CompilerServices import gets the same fully qualified attribute on any framework.</summary>
    /// <param name="framework">The target framework whose reference assemblies the source is compiled against.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The spelling is unconditional. A multi-targeted project compiles one linked file once per framework and
    /// Roslyn reconciles the results into one document; where they differ it writes conflict markers into the
    /// source. Running the same source against two frameworks pins that the emitted attribute does not move.
    /// </remarks>
    [Test]
    [Arguments("net8.0")]
    [Arguments("netstandard2.0")]
    public async Task UnimportedNamespaceGetsTheSameQualifiedAttributeOnEveryFrameworkAsync(string framework)
    {
        const string Source = """
                              public class C
                              {
                                  private readonly int _value;

                                  public C(int value) => _value = value;

                                  public int {|PSH1410:GetValue|}() => _value;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;

                                       [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                                       public int GetValue() => _value;
                                   }
                                   """;
        var test = new Verify.Test
        {
            TestCode = Source,
            FixedCode = FixedSource,
            ReferenceAssemblies = framework == "net8.0" ? ReferenceAssemblies.Net.Net80 : ReferenceAssemblies.NetStandard.NetStandard20
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a conditional import does not change the attribute the fix emits.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A <c>using</c> inside an <c>#if</c> is a syntax node in the framework that defines the symbol and
    /// inactive text in the one that does not, so reading the import to pick the spelling diverges across a
    /// multi-targeted project exactly as asking the semantic model does. Neither decides it.
    /// </remarks>
    [Test]
    public async Task ConditionalImportStillGetsTheQualifiedAttributeAsync()
    {
        const string Source = """
                              #if !NETSTANDARD
                              using System.Runtime.CompilerServices;
                              #endif

                              public class C
                              {
                                  private readonly int _value;

                                  public C(int value) => _value = value;

                                  public int {|PSH1410:GetValue|}() => _value;
                              }
                              """;
        const string FixedSource = """
                                   #if !NETSTANDARD
                                   using System.Runtime.CompilerServices;
                                   #endif

                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;

                                       [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                                       public int GetValue() => _value;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member that already carries an attribute keeps one copy of its doc comment.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The doc comment is the leading trivia of the existing attribute list, not of the member, so a fix that
    /// strips it from the member and then inserts ahead of the original lists leaves a second copy behind.
    /// </remarks>
    [Test]
    public async Task ExistingAttributeKeepsOneDocCommentAsync()
    {
        const string Source = """
                              using System.Diagnostics.CodeAnalysis;
                              using System.Runtime.CompilerServices;

                              public class C
                              {
                                  private readonly int _value;

                                  public C(int value) => _value = value;

                                  /// <summary>Reads the value.</summary>
                                  [SuppressMessage("Design", "CA1000", Justification = "test")]
                                  public int {|PSH1410:GetValue|}() => _value;
                              }
                              """;
        const string FixedSource = """
                                   using System.Diagnostics.CodeAnalysis;
                                   using System.Runtime.CompilerServices;

                                   public class C
                                   {
                                       private readonly int _value;

                                       public C(int value) => _value = value;

                                       /// <summary>Reads the value.</summary>
                                       [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                                       [SuppressMessage("Design", "CA1000", Justification = "test")]
                                       public int GetValue() => _value;
                                   }
                                   """;
        await VerifyOptInAsync(Source, FixedSource);
    }

    /// <summary>Verifies a member inside a conditional region is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// A multi-targeted project compiles the file once per framework, and the same member is commonly
    /// written once per branch — carrying the attribute where it is wanted and not where it is not. Only
    /// some compilations would take the edit, which leaves Roslyn reconciling a linked document that gained
    /// the attribute in one framework and not another, and it writes conflict markers into the source.
    /// </remarks>
    [Test]
    public async Task MemberInsideAConditionalRegionIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                private readonly int _value;

                public C(int value) => _value = value;

            #if !NETSTANDARD
                public int GetValue() => _value;
            #else
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int GetValue() => _value;
            #endif
            }
            """);

    /// <summary>Verifies the attribute this fix writes is recognised on a second pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The fix emits the fully qualified spelling. If the eligibility check only recognises the short one
    /// the member is reported again and a second application yields CS0579, a duplicate attribute.
    /// </remarks>
    [Test]
    public async Task QualifiedMethodImplAttributeIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                private readonly int _value;

                public C(int value) => _value = value;

                [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                public int GetValue() => _value;
            }
            """);

    /// <summary>Verifies a member that already carries a MethodImpl attribute stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingMethodImplIsCleanAsync()
        => await VerifyOptInAsync(
            """
            using System.Runtime.CompilerServices;

            public class C
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                public int GetValue() => 42;
            }
            """);

    /// <summary>Verifies a virtual member stays clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task VirtualMemberIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                public virtual int GetValue() => 42;
            }
            """);

    /// <summary>Verifies a block-bodied method stays clean; only expression bodies are forwarders.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task BlockBodiedMethodIsCleanAsync()
        => await VerifyOptInAsync(
            """
            public class C
            {
                public int GetValue()
                {
                    return 42;
                }
            }
            """);

    /// <summary>Verifies the rule ships disabled by default; blanket inlining is an opinionated convention.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsOffByDefaultAsync()
        => await Assert.That(ApiSelectionRules.InlineTrivialForwarders.IsEnabledByDefault).IsFalse();

    /// <summary>Runs an opted-in verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The test source.</param>
    /// <param name="fixedSource">The expected fixed source, when a fix should apply.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyOptInAsync(string source, string? fixedSource = null)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
            test.FixedState.AnalyzerConfigFiles.Add(("/.editorconfig", OptInConfig));
        }

        await test.RunAsync(CancellationToken.None);
    }
}
