// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyPragma = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst1426PragmaWarningDisableAnalyzer,
    StyleSharp.Analyzers.Sst1426PragmaWarningDisableCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the SST1426 #pragma-to-[SuppressMessage] code fix.</summary>
public class Sst1426PragmaWarningDisableCodeFixProviderUnitTest
{
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
        var test = new VerifyPragma.Test
        {
            TestCode = source,
            FixedCode = fixedSource,
            TestBehaviors = TestBehaviors.SkipSuppressionCheck,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
