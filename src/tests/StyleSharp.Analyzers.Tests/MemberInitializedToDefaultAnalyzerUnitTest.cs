// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyDefaultInit = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.RedundantCodeAnalyzer,
    StyleSharp.Analyzers.MemberInitializedToDefaultCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1176 (members initialized to default) and its fix.</summary>
public class MemberInitializedToDefaultAnalyzerUnitTest
{
    /// <summary>Verifies each numeric literal representation and character default is recognized.</summary>
    /// <param name="type">The member type.</param>
    /// <param name="zero">The default literal.</param>
    /// <param name="other">The nondefault literal.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("long", "0L", "1L")]
    [Arguments("uint", "0U", "1U")]
    [Arguments("ulong", "0UL", "1UL")]
    [Arguments("float", "0F", "1F")]
    [Arguments("double", "0D", "1D")]
    [Arguments("decimal", "0M", "1M")]
    [Arguments("char", "'\\0'", "'x'")]
    [Arguments("int", "default(int)", "1")]
    [Arguments("int", "default", "1")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task LiteralDefaultsAreRecognizedAsync(string type, string zero, string other) =>
        VerifyDefaultInit.VerifyAnalyzerAsync($$"""
            class C
            {
                public {{type}} Field = {|SST1176:{{zero}}|};
                public {{type}} Property { get; set; } = {|SST1176:{{zero}}|};
                public {{type}} Other = {{other}};
                public {{type}} OtherProperty { get; set; } = {{other}};
            }
            """);

    /// <summary>Verifies event defaults are diagnosed while absent and computed initializers are retained.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task EventAndComputedInitializersAsync() =>
        VerifyDefaultInit.VerifyAnalyzerAsync("""
            class C
            {
                public event System.Action Changed = {|SST1176:null|};
                public int Empty;
                public int Computed = Next();
                public string Text = "";
                static int Next() => 0;
            }
            """);

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task NonDefaultAndConstAreCleanAsync() =>
        VerifyDefaultInit.VerifyAnalyzerAsync(
            """
            public class C
            {
                private int _count = 5;

                private const int Limit = 0;

                public int Value { get; set; } = 1;
            }
            """);
}
