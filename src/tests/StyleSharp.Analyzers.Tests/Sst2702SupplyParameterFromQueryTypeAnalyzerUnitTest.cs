// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using VerifyQuery = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2702SupplyParameterFromQueryTypeAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="Sst2702SupplyParameterFromQueryTypeAnalyzer"/> (SST2702), which reports a
/// <c>[SupplyParameterFromQuery]</c> property whose type the framework cannot bind from a query string.
/// </summary>
public class Sst2702SupplyParameterFromQueryTypeAnalyzerUnitTest
{
    /// <summary>
    /// An in-source stub of the query-supply attribute, added as a second document so the marker resolves without a
    /// package restore. Omitting it is what the "not referenced" gate test relies on.
    /// </summary>
    private const string ComponentsStub = """
                                          #nullable disable
                                          namespace Microsoft.AspNetCore.Components
                                          {
                                              [System.AttributeUsage(System.AttributeTargets.Property)]
                                              public sealed class SupplyParameterFromQueryAttribute : System.Attribute
                                              {
                                                  public string Name { get; set; }
                                              }
                                          }
                                          """;

    /// <summary>Verifies every supported scalar type binds without a diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SupportedScalarTypesAreSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public bool Flag { get; set; }
                                  [SupplyParameterFromQuery] public byte B { get; set; }
                                  [SupplyParameterFromQuery] public sbyte Sb { get; set; }
                                  [SupplyParameterFromQuery] public short S { get; set; }
                                  [SupplyParameterFromQuery] public ushort Us { get; set; }
                                  [SupplyParameterFromQuery] public int I { get; set; }
                                  [SupplyParameterFromQuery] public uint Ui { get; set; }
                                  [SupplyParameterFromQuery] public long L { get; set; }
                                  [SupplyParameterFromQuery] public ulong Ul { get; set; }
                                  [SupplyParameterFromQuery] public float F { get; set; }
                                  [SupplyParameterFromQuery] public double D { get; set; }
                                  [SupplyParameterFromQuery] public decimal M { get; set; }
                                  [SupplyParameterFromQuery] public string Text { get; set; }
                                  [SupplyParameterFromQuery] public Guid Id { get; set; }
                                  [SupplyParameterFromQuery] public DateTime When { get; set; }
                                  [SupplyParameterFromQuery] public DateTimeOffset Moment { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the nullable and single-dimension array forms of supported types bind without a diagnostic.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableAndArrayFormsAreSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public int? MaybeInt { get; set; }
                                  [SupplyParameterFromQuery] public double? MaybeDouble { get; set; }
                                  [SupplyParameterFromQuery] public Guid? MaybeId { get; set; }
                                  [SupplyParameterFromQuery] public int[] Ints { get; set; }
                                  [SupplyParameterFromQuery] public string[] Words { get; set; }
                                  [SupplyParameterFromQuery] public int?[] MaybeInts { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an enum property is reported, as the framework cannot bind an enum from the query string.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EnumPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public enum Tone { Light, Dark }

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public Tone {|SST2702:Theme|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a nullable enum property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullableEnumPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public enum Tone { Light, Dark }

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public Tone? {|SST2702:Theme|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a custom-type property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CustomTypePropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Payload { }

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public Payload {|SST2702:Data|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a char property is reported, as it is outside the supported set.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CharPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public char {|SST2702:Grade|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a TimeSpan property is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TimeSpanPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public TimeSpan {|SST2702:Window|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a multi-dimensional array property is reported, as only single-dimension arrays bind.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultiDimensionalArrayPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public int[,] {|SST2702:Grid|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a jagged array property is reported, as its element is itself an array.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task JaggedArrayPropertyIsReportedAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public int[][] {|SST2702:Rows|} { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a property whose type does not resolve is not reported — a broken type is the compiler's to flag.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnresolvedTypePropertyIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public {|CS0246:Missing|} Value { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a property without the attribute is not reported, whatever its type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PropertyWithoutAttributeIsSilentAsync()
    {
        const string Source = """
                              #nullable disable
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  public System.TimeSpan Window { get; set; }
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies DateOnly and TimeOnly bind when the target framework defines them.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task DateOnlyAndTimeOnlyAreSilentWhenPresentAsync()
    {
        const string Source = """
                              #nullable disable
                              using System;
                              using Microsoft.AspNetCore.Components;

                              public class Page
                              {
                                  [SupplyParameterFromQuery] public DateOnly Day { get; set; }
                                  [SupplyParameterFromQuery] public TimeOnly Time { get; set; }
                              }
                              """;

        var test = new VerifyQuery.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = Source,
        };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
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
                                  [AttributeUsage(AttributeTargets.Property)]
                                  public sealed class SupplyParameterFromQueryAttribute : Attribute { }

                                  public class Page
                                  {
                                      [SupplyParameterFromQuery] public TimeSpan Window { get; set; }
                                  }
                              }
                              """;

        var test = new VerifyQuery.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
        };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the components marker stub, on the netstandard2.0 reference set.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The netstandard2.0 reference set has no DateOnly or TimeOnly, so these runs also exercise the resolver's
    /// path where an optional supported type is absent and simply never matched.
    /// </remarks>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyQuery.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = source,
        };
        test.TestState.Sources.Add(("ComponentsStub.cs", ComponentsStub));
        await test.RunAsync(CancellationToken.None);
    }
}
