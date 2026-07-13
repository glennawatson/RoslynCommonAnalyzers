// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyGuid = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2012UseGuidEmptyAnalyzer,
    StyleSharp.Analyzers.Sst2012UseGuidEmptyCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2012 (use Guid.Empty for the empty GUID) and its fix.</summary>
public class UseGuidEmptyAnalyzerUnitTest
{
    /// <summary>Verifies the parameterless construction is reported and replaced by the value it produces.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessConstructionIsReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private Guid _id = {|SST2012:new Guid()|};

                                  public Guid Blank() => {|SST2012:new Guid()|};

                                  public bool IsBlank(Guid id) => id == {|SST2012:new Guid()|};
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private Guid _id = Guid.Empty;

                                       public Guid Blank() => Guid.Empty;

                                       public bool IsBlank(Guid id) => id == Guid.Empty;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the replacement is spelled the way the construction was.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task QualifiedConstructionKeepsItsQualificationAsync()
    {
        const string Source = """
                              public class C
                              {
                                  private System.Guid _id = {|SST2012:new System.Guid()|};

                                  public System.Guid Read() => _id;
                              }
                              """;
        const string FixedSource = """
                                   public class C
                                   {
                                       private System.Guid _id = System.Guid.Empty;

                                       public System.Guid Read() => _id;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a target-typed construction is reported and given a name for the value.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TargetTypedConstructionIsReplacedAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private Guid _id = {|SST2012:new()|};

                                  public Guid Read() => _id;
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private Guid _id = Guid.Empty;

                                       public Guid Read() => _id;
                                   }
                                   """;

        await VerifyGuid.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a generated GUID and a seeded one are left alone: they mean what they say.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GeneratedAndSeededGuidsAreCleanAsync()
        => await VerifyGuid.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private Guid _fresh = Guid.NewGuid();

                private Guid _seeded = new Guid("00000000-0000-0000-0000-000000000001");

                private Guid _named = Guid.Empty;

                public Guid Fresh() => _fresh;

                public Guid Seeded() => _seeded;

                public Guid Named() => _named;
            }
            """);

    /// <summary>Verifies a parameterless construction of another type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructionOfAnotherTypeIsCleanAsync()
        => await VerifyGuid.VerifyAnalyzerAsync(
            """
            namespace Fakes
            {
                public struct Guid
                {
                }
            }

            public class C
            {
                private Fakes.Guid _id = new Fakes.Guid();

                public Fakes.Guid Read() => _id;
            }
            """);
}
