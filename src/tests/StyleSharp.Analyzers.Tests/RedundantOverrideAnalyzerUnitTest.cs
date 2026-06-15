// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyOverride = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.RedundantOverrideCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1181 (redundant overriding members) and its fix.</summary>
public class RedundantOverrideAnalyzerUnitTest
{
    /// <summary>Verifies an expression-bodied method override that only forwards to the base is reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardingMethodRemovedAsync()
    {
        const string Source = """
                              public class B
                              {
                                  public virtual int Add(int x, int y) => x + y;
                              }

                              public class C : B
                              {
                                  public override int {|SST1181:Add|}(int x, int y) => base.Add(x, y);
                              }
                              """;
        const string FixedSource = """
                                   public class B
                                   {
                                       public virtual int Add(int x, int y) => x + y;
                                   }

                                   public class C : B
                                   {
                                   }
                                   """;
        await VerifyOverride.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a property override that only forwards each accessor is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ForwardingPropertyReportedAsync()
        => await VerifyOverride.VerifyAnalyzerAsync(
            """
            public class B
            {
                public virtual int Value { get; set; }
            }

            public class C : B
            {
                public override int {|SST1181:Value|}
                {
                    get => base.Value;
                    set => base.Value = value;
                }
            }
            """);

    /// <summary>Verifies Fix All removes every redundant forwarding override across a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class B
                              {
                                  public virtual int Add(int x, int y) => x + y;

                                  public virtual int Twice(int x) => x * 2;
                              }

                              public class C : B
                              {
                                  public override int {|SST1181:Add|}(int x, int y) => base.Add(x, y);

                                  public override int {|SST1181:Twice|}(int x) => base.Twice(x);
                              }
                              """;
        const string FixedSource = """
                                   public class B
                                   {
                                       public virtual int Add(int x, int y) => x + y;

                                       public virtual int Twice(int x) => x * 2;
                                   }

                                   public class C : B
                                   {
                                   }
                                   """;
        await VerifyOverride.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a sealed override, an override that adds work, and one that reorders arguments are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MeaningfulOverridesAreCleanAsync()
        => await VerifyOverride.VerifyAnalyzerAsync(
            """
            public class B
            {
                public virtual int Add(int x, int y) => x + y;

                public virtual int Twice(int x) => x * 2;
            }

            public class C : B
            {
                public sealed override int Twice(int x) => base.Twice(x);

                public override int Add(int x, int y) => base.Add(y, x);
            }
            """);
}
