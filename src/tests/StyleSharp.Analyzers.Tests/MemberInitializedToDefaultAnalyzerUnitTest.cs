// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyDefaultInit = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.MemberInitializedToDefaultCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1176 (members initialized to default) and its fix.</summary>
public class MemberInitializedToDefaultAnalyzerUnitTest
{
    /// <summary>Verifies field and auto-property default initializers are reported and removed.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DefaultInitializersRemovedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count = {|SST1176:0|};

                                  private string _name = {|SST1176:null|};

                                  public bool Ready { get; set; } = {|SST1176:false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;

                                       private string _name;

                                       public bool Ready { get; set; }
                                   }
                                   """;
        await VerifyDefaultInit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies Fix All removes every default initializer in a document in one pass.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task FixAllRewritesEveryOccurrenceAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private int _count = {|SST1176:0|};

                                  private string _name = {|SST1176:null|};

                                  private bool _ready = {|SST1176:false|};
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private int _count;

                                       private string _name;

                                       private bool _ready;
                                   }
                                   """;
        await VerifyDefaultInit.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies non-default initializers and const fields are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonDefaultAndConstAreCleanAsync()
        => await VerifyDefaultInit.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _count = 5;

                private const int Limit = 0;

                public int Value { get; set; } = 1;
            }
            """);
}
