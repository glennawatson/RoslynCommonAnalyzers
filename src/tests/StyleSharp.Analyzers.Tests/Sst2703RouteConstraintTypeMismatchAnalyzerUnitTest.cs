// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyRoute = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2703RouteConstraintTypeMismatchAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2703RouteConstraintTypeMismatchAnalyzer"/> (SST2703), which reports a routable
/// component whose route template constrains a segment to one type while the same-named component parameter is
/// another.
/// </summary>
public class Sst2703RouteConstraintTypeMismatchAnalyzerUnitTest
{
    /// <summary>
    /// In-source stubs of the route and parameter marker attributes, added as a second document so the markers
    /// resolve without a package restore. Omitting them is what the "not referenced" gate test relies on.
    /// </summary>
    private const string ComponentsStub = """
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
                                              public sealed class RouteAttribute : System.Attribute
                                              {
                                                  public RouteAttribute(string template) { }
                                              }

                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class ParameterAttribute : System.Attribute
                                              {
                                              }
                                          }
                                          """;

    /// <summary>Verifies an int constraint feeding a string parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IntConstraintFeedingStringParameterIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int}")]
                              public class UserPage
                              {
                                  [Parameter] public string {|SST2703:Id|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a guid constraint feeding an int parameter is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuidConstraintFeedingIntParameterIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/item/{key:guid}")]
                              public class ItemPage
                              {
                                  [Parameter] public int {|SST2703:Key|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a matching constraint and parameter type are not reported, and a non-class type is skipped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MatchingConstraintIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public enum Kind { A, B }

                              [Route("/user/{id:int}")]
                              public class UserPage
                              {
                                  [Parameter] public int Id { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies every typed constraint keyword matches its CLR type without a diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AllTypedConstraintsMatchAreSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;
                              using Microsoft.AspNetCore.Components;

                              [Route("/x/{a:long}/{b:bool}/{c:datetime}/{d:decimal}/{e:double}/{f:float}")]
                              public class Page
                              {
                                  [Parameter] public long A { get; set; }
                                  [Parameter] public bool B { get; set; }
                                  [Parameter] public DateTime C { get; set; }
                                  [Parameter] public decimal D { get; set; }
                                  [Parameter] public double E { get; set; }
                                  [Parameter] public float F { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a nullable parameter matches the underlying constrained type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableParameterMatchesConstraintAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int?}")]
                              public class UserPage
                              {
                                  [Parameter] public int? Id { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an untyped segment carries no constraint and is not checked.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UntypedSegmentIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/blog/{slug}")]
                              public class BlogPage
                              {
                                  [Parameter] public int Slug { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a constraint the rule does not map to a type is not checked.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnmappedConstraintIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/blog/{slug:alpha}")]
                              public class BlogPage
                              {
                                  [Parameter] public int Slug { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a constraint with a parenthesised argument is read as its leading keyword, and an unmapped one is skipped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintWithArgumentsUnmappedIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/code/{value:regex(^abc$)}")]
                              public class CodePage
                              {
                                  [Parameter] public int Value { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a typed constraint with no same-named parameter is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstraintWithoutMatchingParameterIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int}")]
                              public class UserPage
                              {
                                  [Parameter] public string Name { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a same-named property that is not a component parameter is not treated as the receiver.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SameNamedNonParameterIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int}")]
                              public class UserPage
                              {
                                  public string Id { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the name match is case-insensitive.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CaseInsensitiveNameMismatchIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{userid:int}")]
                              public class UserPage
                              {
                                  [Parameter] public string {|SST2703:UserId|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a parameter inherited from a base component is found and checked.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InheritedParameterMismatchIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class PageBase
                              {
                                  [Parameter] public string {|SST2703:Slug|} { get; set; }
                              }

                              [Route("/post/{slug:int}")]
                              public class PostPage : PageBase
                              {
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a catch-all segment's constraint is checked after the leading marker is dropped.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CatchAllConstraintMismatchIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/files/{*path:guid}")]
                              public class FilePage
                              {
                                  [Parameter] public int {|SST2703:Path|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies only the first constraint keyword of a multi-constraint segment fixes the type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleConstraintsUseFirstAsTypeAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int:min(1)}")]
                              public class UserPage
                              {
                                  [Parameter] public string {|SST2703:Id|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies literal doubled braces are skipped and a following typed segment is still checked.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EscapedBracesAreSkippedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/lit/{{literal}}/{id:int}")]
                              public class LiteralPage
                              {
                                  [Parameter] public string {|SST2703:Id|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a template with an unclosed brace is parsed without a diagnostic and without throwing.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnclosedBraceIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/user/{id:int")]
                              public class UserPage
                              {
                                  [Parameter] public string Id { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an empty route template is ignored.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyTemplateIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("")]
                              public class UserPage
                              {
                                  [Parameter] public string Id { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the same mismatched parameter is reported once even when two templates constrain it.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DuplicateAcrossTemplatesIsReportedOnceAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              [Route("/a/{id:int}")]
                              [Route("/b/{id:int}")]
                              public class UserPage
                              {
                                  [Parameter] public string {|SST2703:Id|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the rule stays silent when the components assembly is not referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenComponentsNotReferencedAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;

                              namespace Look
                              {
                                  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                                  public sealed class RouteAttribute : Attribute
                                  {
                                      public RouteAttribute(string template) { }
                                  }

                                  [AttributeUsage(AttributeTargets.Property)]
                                  public sealed class ParameterAttribute : Attribute { }

                                  [Route("/user/{id:int}")]
                                  public class UserPage
                                  {
                                      [Parameter] public string Id { get; set; }
                                  }
                              }
                              """;

        var test = new VerifyRoute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the route and parameter marker stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyRoute.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = source,
        };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
