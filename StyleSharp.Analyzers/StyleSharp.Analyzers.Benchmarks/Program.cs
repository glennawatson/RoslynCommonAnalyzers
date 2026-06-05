// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// Run in-process (InProcessEmitToolchain) so the harness does not have to spin up
// a child build against a preview SDK; ShortRun keeps the sweep quick while
// MemoryDiagnoser gives the allocation columns we care about.
var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
    .AddDiagnoser(MemoryDiagnoser.Default);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
