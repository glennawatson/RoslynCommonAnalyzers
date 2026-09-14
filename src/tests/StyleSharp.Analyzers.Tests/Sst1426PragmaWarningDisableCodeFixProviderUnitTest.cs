// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Composition.Hosting;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Testing;

using VerifyPragma = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1426PragmaWarningDisableAnalyzer,
    StyleSharp.Analyzers.Sst1426PragmaWarningDisableCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1426 #pragma-to-[SuppressMessage] code fix.</summary>
public class Sst1426PragmaWarningDisableCodeFixProviderUnitTest
{
    /// <summary>Verifies unmatched restores and directives outside members cannot become member attributes.</summary>
    /// <param name="source">The directive region or stale diagnostic source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("#pragma warning disable SST1309\nclass C { }")]
    [Arguments("#pragma warning disable SST1309, SST1400\nclass C { }\n#pragma warning restore SST1309\n")]
    [Arguments("#pragma warning disable SST1309\nusing System;\n#pragma warning restore SST1309\n")]
    [Arguments("// stale directive\nclass C { }")]
    public async Task UnsafeDirectiveRegionsAreNotReplacedAsync(string source)
    {
        using var workspace = new AdhocWorkspace();
        var document = workspace.AddProject(nameof(Test), LanguageNames.CSharp).AddDocument("Test.cs", source);
        var root = (await document.GetSyntaxRootAsync())!;
        var diagnostic = Diagnostic.Create(MaintainabilityRules.PreferSuppressMessageOverPragma, Location.Create(root.SyntaxTree, new(0, 1)));
        using var container = new ContainerConfiguration().WithPart<Sst1426PragmaWarningDisableCodeFixProvider>().CreateContainer();
        var provider = container.GetExport<CodeFixProvider>();
        var actions = new List<CodeAction>();
        await provider.RegisterCodeFixesAsync(new(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        await Assert.That(actions).IsEmpty();
    }

    /// <summary>Verifies unindented directives and intervening unrelated restores preserve the remaining trivia.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ColumnZeroDirectivePairIsReplacedAsync() =>
        VerifyAsync(
            """
            {|SST1426:#pragma warning disable SST1309|}
            class C { }
            #region Keep
            #pragma warning restore SST1400
            #endregion
            #pragma warning restore SST1309
            """,
            """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
            class C { }
            #region Keep
            #pragma warning restore SST1400
            #endregion
            """);

    /// <summary>Verifies a pair that brackets more than one member is reported but not replaced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The fix erases both halves and writes one attribute, so the members it no longer covers would
    /// silently start warning again.
    /// </remarks>
    [Test]
    public async Task PairCoveringSeveralMembersIsNotReplacedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1426:#pragma warning disable SST1309|}
                                  private int first;
                                  private int second;
                                  #pragma warning restore SST1309
                              }
                              """;
        await VerifyAsync(Source, Source);
    }

    /// <summary>Verifies a member-level disable becomes a [SuppressMessage] on the member, with the restore removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MemberLevelDisableMovesToMemberAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1426:#pragma warning disable SST1309|}
                                  private int field;
                                  #pragma warning restore SST1309
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
                                       private int field;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disable inside a method body moves to a [SuppressMessage] on the enclosing method.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task StatementLevelDisableMovesToEnclosingMethodAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private void M()
                                  {
                                      {|SST1426:#pragma warning disable SST1309|}
                                      var x = 1;
                                      #pragma warning restore SST1309
                                  }
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
                                       private void M()
                                       {
                                           var x = 1;
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disable before a type moves to a [SuppressMessage] on that type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeLevelDisableMovesToTypeAsync()
    {
        const string Source = """
                              namespace N
                              {
                                  {|SST1426:#pragma warning disable SST1309|}
                                  internal class C
                                  {
                                  }
                                  #pragma warning restore SST1309
                              }
                              """;
        const string FixedSource = """
                                   namespace N
                                   {
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
                                       internal class C
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a disable of several analyzer codes produces one attribute per code.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleAnalyzerCodesProduceMultipleAttributesAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1426:#pragma warning disable SST1309, SST1400|}
                                  private int field;
                                  #pragma warning restore SST1309, SST1400
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "SST1400", Justification = "<Pending>")]
                                       private int field;
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies a directive that also carries a compiler (CS) code is left unchanged (no fix offered).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MixedCompilerAndAnalyzerCodesAreNotFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1426:#pragma warning disable CS0169, SST1309|}
                                  private int field;
                                  #pragma warning restore CS0169, SST1309
                              }
                              """;
        await VerifyAsync(Source, Source);
    }

    /// <summary>Verifies an existing attribute on the member is preserved when the suppression is added.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExistingAttributeIsPreservedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  {|SST1426:#pragma warning disable SST1309|}
                                  [System.Obsolete]
                                  private void M()
                                  {
                                  }
                                  #pragma warning restore SST1309
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "SST1309", Justification = "<Pending>")]
                                       [System.Obsolete]
                                       private void M()
                                       {
                                       }
                                   }
                                   """;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>
    /// Runs the code fix, skipping the verifier's suppression check. SST1426 flags every analyzer-code
    /// <c>#pragma warning disable</c>, including the one the check injects to suppress SST1426 itself, so
    /// that generic check does not apply to this rule.
    /// </summary>
    /// <param name="source">The markup source to analyze and fix.</param>
    /// <param name="fixedSource">The expected source after the fix is applied.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new VerifyPragma.Test { TestCode = source, FixedCode = fixedSource, TestBehaviors = TestBehaviors.SkipSuppressionCheck, };

        await test.RunAsync(CancellationToken.None);
    }
}
