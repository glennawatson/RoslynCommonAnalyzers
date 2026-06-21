// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLanguageStyle = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.LanguageStyleAnalyzer,
    StyleSharp.Analyzers.LanguageStyleCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for the grouped language-style readability analyzer (SST1193-SST1199).</summary>
public class LanguageStyleAnalyzerUnitTest
{
    /// <summary>Verifies object initializer opportunities are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ObjectInitializerCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public string Name { get; set; } = "";
                              }

                              public sealed class C
                              {
                                  public Person M()
                                  {
                                      var person = {|SST1193:new Person()|};
                                      person.Name = "Ada";
                                      return person;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                       public string Name { get; set; } = "";
                                   }

                                   public sealed class C
                                   {
                                       public Person M()
                                       {
                                           var person = new Person() { Name = "Ada" };
                                           return person;
                                       }
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies collection initializer opportunities are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CollectionInitializerCandidateIsFixedAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class C
                              {
                                  public List<int> M()
                                  {
                                      var values = {|SST1194:new List<int>()|};
                                      values.Add(1);
                                      return values;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System.Collections.Generic;

                                   public sealed class C
                                   {
                                       public List<int> M()
                                       {
                                           var values = new List<int>() { 1 };
                                           return values;
                                       }
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies null-coalescing conditional expressions are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullCoalescingCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public string M(string value, string fallback) => {|SST1195:value == null ? fallback : value|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public string M(string value, string fallback) => value ?? fallback;
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies null-propagation conditional expressions are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullPropagationCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                                  public string Name { get; set; } = "";
                              }

                              public sealed class C
                              {
                                  public string M(Person person) => {|SST1196:person == null ? null : person.Name|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                       public string Name { get; set; } = "";
                                   }

                                   public sealed class C
                                   {
                                       public string M(Person person) => person?.Name;
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies adjacent return statements are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalReturnCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool flag)
                                  {
                                      {|SST1197:if|} (flag)
                                      {
                                          return 1;
                                      }

                                      return 2;
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public int M(bool flag)
                                       {
                                           return flag ? 1 : 2;
                                       }
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies adjacent returns are not collapsed when the result would nest conditional expressions.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalReturnWithFollowingConditionalExpressionIsCleanAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public int M(bool first, bool second)
                                  {
                                      if (first)
                                      {
                                          return 1;
                                      }

                                      return second ? 2 : 3;
                                  }
                              }
                              """;
        await VerifyLanguageStyle.VerifyAnalyzerAsync(Source);
    }

    /// <summary>Verifies matching if/else assignments are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAssignmentCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class C
                              {
                                  public void M(bool flag)
                                  {
                                      var value = 0;
                                      {|SST1198:if|} (flag)
                                      {
                                          value = 1;
                                      }
                                      else
                                      {
                                          value = 2;
                                      }
                                  }
                              }
                              """;
        const string FixedSource = """
                                   public sealed class C
                                   {
                                       public void M(bool flag)
                                       {
                                           var value = 0;
                                           value = flag ? 1 : 2;
                                       }
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies <c>typeof(T).Name</c> is reported when <c>nameof(T)</c> is equivalent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TypeofNameCandidateIsFixedAsync()
    {
        const string Source = """
                              public sealed class Person
                              {
                              }

                              public sealed class C
                              {
                                  public string M() => {|SST1199:typeof(Person).Name|};
                              }
                              """;
        const string FixedSource = """
                                   public sealed class Person
                                   {
                                   }

                                   public sealed class C
                                   {
                                       public string M() => nameof(Person);
                                   }
                                   """;
        await VerifyLanguageStyle.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies risky or already-modern shapes are clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonCandidatesAreCleanAsync()
    {
        const string Source = """
                              using System.Collections.Generic;

                              public sealed class Person
                              {
                                  public Person(string name)
                                  {
                                      Name = name;
                                  }

                                  public string Name { get; set; }
                              }

                              public sealed class C
                              {
                                  private readonly string _text = "";
                                  private readonly Person _field = new Person("Ada");

                                  public Person Property { get; } = new Person("Ada");

                                  public void ObjectWithConstructorArgument()
                                  {
                                      var person = new Person("Ada");
                                  }

                                  public string PropertyReceiver(Person fallback) => Property == null ? fallback.Name : Property.Name;

                                  public string FieldCoalesce(string fallback) => _text == null ? fallback : _text;

                                  public string FieldReceiver(Person fallback) => _field == null ? fallback.Name : _field.Name;

                                  public string GenericName() => typeof(List<int>).Name;
                              }
                              """;
        await VerifyLanguageStyle.VerifyAnalyzerAsync(Source);
    }
}
