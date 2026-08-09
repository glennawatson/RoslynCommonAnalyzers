// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyFoldNullCheck = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2285FoldNullCheckIntoConditionalAccessAnalyzer,
    StyleSharp.Analyzers.Sst2285FoldNullCheckIntoConditionalAccessCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for <see cref="Sst2285FoldNullCheckIntoConditionalAccessAnalyzer"/> and its code fix (SST2285).</summary>
public class FoldNullCheckIntoConditionalAccessAnalyzerUnitTest
{
    /// <summary>Verifies a guarded relational comparison folds into a conditional access.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedRelationalComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string text) => {|SST2285:text != null && text.Length > 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string text) => text?.Length > 0;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guarded equality comparison against a non-null constant folds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedEqualityComparisonIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              internal class C
                              {
                                  public bool M(List<int> items) => {|SST2285:items != null && items.Count == 3|};
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   internal class C
                                   {
                                       public bool M(List<int> items) => items?.Count == 3;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guarded bool-valued member read folds to a comparison against true.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedBooleanMemberIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class Owner
                              {
                                  public bool Ready { get; set; }
                              }

                              internal class C
                              {
                                  public bool M(Owner owner) => {|SST2285:owner != null && owner.Ready|};
                              }
                              """;
        const string FixedSource = """
                                   internal class Owner
                                   {
                                       public bool Ready { get; set; }
                                   }

                                   internal class C
                                   {
                                       public bool M(Owner owner) => owner?.Ready == true;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guarded method call folds into a conditional access.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedMethodCallIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string text) => {|SST2285:text != null && text.StartsWith("a")|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string text) => text?.StartsWith("a") == true;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a longer member chain keeps everything after the guarded receiver.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedMemberChainIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class Inner
                              {
                                  public string Name { get; set; } = "";
                              }

                              internal class Owner
                              {
                                  public Inner Child { get; set; } = new Inner();
                              }

                              internal class C
                              {
                                  public bool M(Owner owner) => {|SST2285:owner != null && owner.Child.Name.Length > 2|};
                              }
                              """;
        const string FixedSource = """
                                   internal class Inner
                                   {
                                       public string Name { get; set; } = "";
                                   }

                                   internal class Owner
                                   {
                                       public Inner Child { get; set; } = new Inner();
                                   }

                                   internal class C
                                   {
                                       public bool M(Owner owner) => owner?.Child.Name.Length > 2;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guarded nullable bool read folds to a comparison against true.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedNullableBooleanValueIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(bool? ready) => {|SST2285:ready != null && ready.Value|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(bool? ready) => ready == true;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a null guard written with the literal first is folded the same way.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardWithLiteralFirstIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  public bool M(string text) => {|SST2285:null != text && text.Length > 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       public bool M(string text) => text?.Length > 0;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a guarded field on a this-qualified receiver folds.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedThisQualifiedReceiverIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              internal class C
                              {
                                  private string _text = "";

                                  public bool M() => {|SST2285:this._text != null && this._text.Length > 0|};
                              }
                              """;
        const string FixedSource = """
                                   internal class C
                                   {
                                       private string _text = "";

                                       public bool M() => this._text?.Length > 0;
                                   }
                                   """;
        await VerifyFoldNullCheck.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies an inequality comparison is left alone; the folded form answers differently for null.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedInequalityComparisonIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string text) => text != null && text.Length != 0;
            }
            """);

    /// <summary>Verifies a comparison against null on the right is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedComparisonAgainstNullIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class Owner
            {
                public string Name { get; set; } = "";
            }

            internal class C
            {
                public bool M(Owner owner) => owner != null && owner.Name == null;
            }
            """);

    /// <summary>Verifies a right operand that also reads the guarded value is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RightOperandReadingGuardedValueIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string text) => text != null && text.Length > text.GetHashCode();
            }
            """);

    /// <summary>Verifies a conjunction whose second half is about something else is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedConjunctionIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string text, int count) => text != null && count > 0;
            }
            """);

    /// <summary>Verifies a guard on a side-effecting receiver is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SideEffectingReceiverIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class C
            {
                private static string Get() => "a";

                public bool M() => Get() != null && Get().Length > 0;
            }
            """);

    /// <summary>Verifies a null-equality guard is not a null-exclusion guard and is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EqualityGuardIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class C
            {
                public bool M(string text, bool other) => text == null && other;
            }
            """);

    /// <summary>Verifies a guarded non-bool member read is left alone; there is nothing to compare it to.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardedNonBooleanMemberIsCleanAsync()
        => await VerifyFoldNullCheck.VerifyAnalyzerAsync(
            """
            internal class Owner
            {
                public bool Ready { get; set; }
            }

            internal class C
            {
                public bool M(Owner owner, bool flag) => owner != null && flag;
            }
            """);
}
