// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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

    /// <summary>Runs the analyzer against the source plus the MEF marker stubs.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new VerifyMef.Test { TestCode = source };
        test.TestState.Sources.Add(("MefStubs.cs", MefStubs));
        await test.RunAsync(CancellationToken.None);
    }
}
