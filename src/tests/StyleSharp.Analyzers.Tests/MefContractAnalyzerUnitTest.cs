// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Testing;
using VerifyMef = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.MefContractAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>
/// Unit tests for <see cref="MefContractAnalyzer"/>, which reports the three MEF-contract defects
/// SST2472 (an exported contract the type does not implement), SST2473 (a shared export part
/// constructed with <c>new</c>) and SST2474 (a creation policy on a type with no export), across both
/// the <c>System.ComponentModel.Composition</c> and <c>System.Composition</c> flavors.
/// </summary>
public class MefContractAnalyzerUnitTest
{
    /// <summary>The document containing the cached MEF attribute declarations.</summary>
    private const string MefStubsFileName = "MefStubs.cs";

    /// <summary>
    /// In-source stubs of both MEF flavors' marker attributes, added as a second document so the
    /// analyzer's marker types resolve without a package restore. Omitting this document is what the
    /// "MEF not referenced" test relies on to prove the gate.
    /// </summary>
    private const string MefStubs = """
                                    #nullable disable
                                    using System;

                                    namespace System.ComponentModel.Composition
                                    {
                                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                                        public class ExportAttribute : Attribute
                                        {
                                            public ExportAttribute() { }
                                            public ExportAttribute(Type contractType) => ContractType = contractType;
                                            public ExportAttribute(string contractName) { }
                                            public ExportAttribute(string contractName, Type contractType) => ContractType = contractType;
                                            public Type ContractType { get; }
                                        }

                                        public enum CreationPolicy { Any = 0, Shared = 1, NonShared = 2 }

                                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                                        public sealed class PartCreationPolicyAttribute : Attribute
                                        {
                                            public PartCreationPolicyAttribute(CreationPolicy creationPolicy) => CreationPolicy = creationPolicy;
                                            public CreationPolicy CreationPolicy { get; }
                                        }
                                    }

                                    namespace System.Composition
                                    {
                                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
                                        public class ExportAttribute : Attribute
                                        {
                                            public ExportAttribute() { }
                                            public ExportAttribute(Type contractType) => ContractType = contractType;
                                            public ExportAttribute(string contractName) { }
                                            public ExportAttribute(string contractName, Type contractType) => ContractType = contractType;
                                            public Type ContractType { get; }
                                        }

                                        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                                        public sealed class SharedAttribute : Attribute
                                        {
                                            public SharedAttribute() { }
                                            public SharedAttribute(string sharingBoundaryName) { }
                                        }
                                    }
                                    """;

    /// <summary>Verifies an export whose contract type the class does not implement is reported (MEF1).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportedContractNotImplementedIsReportedAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [{|SST2472:Export(typeof(IService))|}]
                              public class Widget { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies the <c>contractType:</c> named-argument form is reported the same way.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportedContractNamedArgumentIsReportedAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [{|SST2472:Export(contractType: typeof(IService))|}]
                              public class Widget { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an export whose contract the class implements is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportedContractImplementedIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [Export(typeof(IService))]
                              public class Widget : IService { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a contract satisfied through a base type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportedContractSatisfiedByBaseIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              public class ServiceBase : IService { }

                              [Export(typeof(IService))]
                              public class Widget : ServiceBase { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a bare export with no contract type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportWithoutContractTypeIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              [Export]
                              public class Widget { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies an export whose contract the class does not implement is reported (MEF2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExportedContractNotImplementedMef2IsReportedAsync()
    {
        const string Source = """
                              using System.Composition;

                              public interface IService { }

                              [{|SST2472:Export(typeof(IService))|}]
                              public class Widget { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies constructing a shared export part with <c>new</c> is reported (MEF1).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedPartConstructedDirectlyIsReportedAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [Export(typeof(IService))]
                              [PartCreationPolicy(CreationPolicy.Shared)]
                              public class Service : IService { }

                              public class Consumer
                              {
                                  public IService Create() => {|SST2473:new Service()|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies constructing a MEF2 shared export part with <c>new</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedPartConstructedDirectlyMef2IsReportedAsync()
    {
        const string Source = """
                              using System.Composition;

                              public interface IService { }

                              [Export(typeof(IService))]
                              [Shared]
                              public class Service : IService { }

                              public class Consumer
                              {
                                  public IService Create() => {|SST2473:new Service()|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies constructing a non-shared export part with <c>new</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonSharedPartConstructedDirectlyIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [Export(typeof(IService))]
                              [PartCreationPolicy(CreationPolicy.NonShared)]
                              public class Service : IService { }

                              public class Consumer
                              {
                                  public IService Create() => new Service();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies constructing a plain non-part type with <c>new</c> is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task PlainTypeConstructedDirectlyIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public class Plain { }

                              public class Consumer
                              {
                                  public Plain Create() => new Plain();
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a creation-policy attribute on a type with no export is reported (MEF1).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreationPolicyWithoutExportIsReportedAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              [{|SST2474:PartCreationPolicy(CreationPolicy.Shared)|}]
                              public class Orphan { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a MEF2 shared attribute on a type with no export is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedWithoutExportMef2IsReportedAsync()
    {
        const string Source = """
                              using System.Composition;

                              [{|SST2474:Shared|}]
                              public class Orphan { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a creation-policy attribute alongside an export is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreationPolicyWithExportIsSilentAsync()
    {
        const string Source = """
                              using System.ComponentModel.Composition;

                              public interface IService { }

                              [Export(typeof(IService))]
                              [PartCreationPolicy(CreationPolicy.Shared)]
                              public class Service : IService { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a creation policy alongside a derived export attribute is not reported (MEF2).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// Deriving a custom attribute from <c>ExportAttribute</c> is how MEF is meant to be extended, and
    /// the container honours the derived attribute exactly like its base. Roslyn's own
    /// <c>ExportCodeFixProvider</c> is the canonical example, so treating a derived attribute as "not
    /// an export" would fire on every code-fix provider ever written.
    /// </remarks>
    [Test]
    public async Task CreationPolicyWithDerivedExportMef2IsSilentAsync()
    {
        const string Source = """
                              using System;
                              using System.Composition;

                              [AttributeUsage(AttributeTargets.Class)]
                              public sealed class ExportPluginAttribute : ExportAttribute
                              {
                                  public ExportPluginAttribute(string name) { }
                              }

                              [ExportPlugin("thing")]
                              [Shared]
                              public class Plugin { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a creation policy alongside a derived export attribute is not reported (MEF1).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CreationPolicyWithDerivedExportIsSilentAsync()
    {
        const string Source = """
                              using System;
                              using System.ComponentModel.Composition;

                              [AttributeUsage(AttributeTargets.Class)]
                              public sealed class ExportPluginAttribute : ExportAttribute
                              {
                                  public ExportPluginAttribute() { }
                              }

                              [ExportPlugin]
                              [PartCreationPolicy(CreationPolicy.Shared)]
                              public class Plugin { }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies a shared part exported by a derived attribute is still caught when constructed directly.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SharedPartExportedByDerivedAttributeConstructedDirectlyIsReportedAsync()
    {
        const string Source = """
                              using System;
                              using System.Composition;

                              [AttributeUsage(AttributeTargets.Class)]
                              public sealed class ExportPluginAttribute : ExportAttribute
                              {
                                  public ExportPluginAttribute() { }
                              }

                              [ExportPlugin]
                              [Shared]
                              public class Plugin { }

                              public class Consumer
                              {
                                  public Plugin Make() => {|SST2473:new Plugin()|};
                              }
                              """;
        await VerifyAsync(Source);
    }

    /// <summary>Verifies all three shapes stay silent when no MEF assembly is referenced.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>
    /// The MEF stub document is deliberately not added, so the marker types do not resolve and the
    /// analyzer registers nothing. The attributes here bind to a look-alike set in a non-MEF namespace,
    /// proving the gate rejects the shapes on the marker types, not on the written names.
    /// </remarks>
    [Test]
    public async Task SilentWhenMefNotReferencedAsync()
    {
        const string Source = """
                              using System;

                              namespace Look
                              {
                                  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                                  public sealed class ExportAttribute : Attribute
                                  {
                                      public ExportAttribute(Type contractType) { }
                                  }

                                  public enum CreationPolicy { Any, Shared, NonShared }

                                  [AttributeUsage(AttributeTargets.Class)]
                                  public sealed class PartCreationPolicyAttribute : Attribute
                                  {
                                      public PartCreationPolicyAttribute(CreationPolicy policy) { }
                                  }

                                  public interface IService { }

                                  [Export(typeof(IService))]
                                  [PartCreationPolicy(CreationPolicy.Shared)]
                                  public class Service { }

                                  [PartCreationPolicy(CreationPolicy.Shared)]
                                  public class Orphan { }

                                  public class Consumer
                                  {
                                      public Service Create() => new Service();
                                  }
                              }
                              """;

        var test = new VerifyMef.Test { TestCode = Source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies contract matching accepts self, base, and open generic contracts and skips name-only exports.</summary>
    /// <param name="source">The source with its export attributes.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[System.Composition.Export(typeof(C))] class C { }")]
    [Arguments("class B { } [System.Composition.Export(typeof(B))] class C : B { }")]
    [Arguments("interface I<T> { } interface J { } [System.Composition.Export(typeof(I<>))] class C : J, I<int> { }")]
    [Arguments("[System.Composition.Export(\"contract\")] class C { }")]
    [Arguments("[System.Composition.Export(\"contract\", typeof(object))] class C { }")]
    [Arguments("[System.Composition.Export] class C { } class Consumer { C M() => new(); }")]
    [Arguments("[System.Composition.Export, System.Composition.Shared] class C { } class Consumer { C M() => {|SST2473:new()|}; }")]
    [Arguments("class ExportAttribute : System.Attribute { public ExportAttribute(System.Type type) { } } [global::Export(typeof(int))] class C { }")]
    [Arguments("class SharedAttribute : System.Attribute { } [global::Shared] class C { }")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task ContractAndMarkerIdentityAreRespectedAsync(string source) => VerifyAsync(source);

    /// <summary>Verifies incomplete attributes and non-type targets cannot establish an invalid export contract.</summary>
    /// <param name="source">The incomplete source.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class C { [System.Composition.Export] void M() { } }")]
    [Arguments("[method: System.Composition.Export] class C { }")]
    [Arguments("[return: System.Composition.Shared] class C { }")]
    [Arguments("static class CExtensions { [System.Composition.Export] extension(int value) { public int M() => 1; } }")]
    [Arguments("static class CExtensions { [System.Composition.Shared] extension(int value) { public int M() => 1; } }")]
    [Arguments("[System.Composition.Export(typeof(Missing))] class C { }")]
    [Arguments("[System.Composition.Export((System.Type)null)] class C { }")]
    [Arguments("[System.Composition.Export] class C { } class D { object M() => new Missing(); }")]
    [Arguments("[Missing, System.Composition.Export] class C { } class D { C M() => new C(); }")]
    public async Task IncompleteMefSourceIsSilentAsync(string source)
    {
        var test = new VerifyMef.Test { TestCode = source, CompilerDiagnostics = CompilerDiagnostics.None };
        test.TestState.Sources.Add((MefStubsFileName, MefStubs));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies attributes at identical offsets in different partial declarations retain their own identity.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PartialAttributesAreMatchedToTheirSyntaxTreeAsync()
    {
        const string Source = "[System.Composition.Export] partial class C { }";
        var test = new VerifyMef.Test { TestCode = Source };
        test.TestState.Sources.Add(("Other.cs", Source));
        test.TestState.Sources.Add((MefStubsFileName, MefStubs));
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies absent sharing metadata does not classify an exported type as shared.</summary>
    /// <param name="policyType">The incomplete creation-policy definition.</param>
    /// <param name="argument">The policy argument.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("", "0")]
    [Arguments("public enum CreationPolicy { Any }", "0")]
    [Arguments("public class CreationPolicy { public static int Shared => 1; }", "0")]
    [Arguments("public enum CreationPolicy { Shared }", "")]
    public async Task IncompleteSharingMetadataIsSilentAsync(string policyType, string argument)
    {
        var source = $$"""
            namespace System.ComponentModel.Composition
            {
                public class ExportAttribute : System.Attribute { }
                public class PartCreationPolicyAttribute : System.Attribute
                {
                    public PartCreationPolicyAttribute() { }
                    public PartCreationPolicyAttribute(int value) { }
                }
                {{policyType}}
            }
            [System.ComponentModel.Composition.Export, System.ComponentModel.Composition.PartCreationPolicy({{argument}})]
            class C { }
            class D { C M() => new C(); }
            """;
        var test = new VerifyMef.Test { TestCode = source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies sharing requires both export and policy marker types.</summary>
    /// <param name="declaration">The available attribute definition.</param>
    /// <param name="attribute">The attribute on the constructed type.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("class ExportAttribute : System.Attribute { }", "Export")]
    [Arguments("class SharedAttribute : System.Attribute { }", "Shared")]
    public async Task SingleMarkerFamilyDoesNotEstablishSharedExportAsync(string declaration, string attribute)
    {
        var source = $"namespace System.Composition {{ {declaration} }} [System.Composition.{attribute}] class C {{ }} class D {{ C M() => new C(); }}";
        var test = new VerifyMef.Test { TestCode = source };
        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs the analyzer against the source plus the MEF marker stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyMef.Test { TestCode = source };
        test.TestState.Sources.Add((MefStubsFileName, MefStubs));
        await test.RunAsync(CancellationToken.None);
    }
}
