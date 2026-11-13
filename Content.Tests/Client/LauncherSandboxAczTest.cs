#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Content.Packaging;
using NUnit.Framework;
using Robust.Packaging;
using Robust.Packaging.AssetProcessing.Passes;
using Robust.Shared.Analyzers;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;

namespace Content.Tests.Client;

/// <summary>
/// The official launcher type-checks the client zip, not the whole client output directory.
/// This runs that same checker against only the assemblies <see cref="ClientPackaging"/> puts in the zip.
/// </summary>
[TestFixture]
public sealed class LauncherSandboxAczTest
{
    [Test]
    public async Task ServedClientAssembliesPassSandbox()
    {
        var root = FindRepoRoot();
        var clientBin = Path.Combine(root, "bin", "Content.Client");

        // A leftover from the old separate-project install must not survive the client build.
        Assert.That(Directory.GetFiles(clientBin, "Content.VoiceChat.*.dll"), Is.Empty,
            "bin/Content.Client still has a Content.VoiceChat DLL. The client build should delete it.");

        // Even if one appears after the build, the zip writer only copies the named content assemblies.
        var staleName = "Content.VoiceChat.Shared.dll";
        var stalePath = Path.Combine(clientBin, staleName);
        await File.WriteAllBytesAsync(stalePath, new byte[] { 0x4D, 0x5A });
        try
        {
            var zipPath = Path.Combine(Path.GetTempPath(), "frontier-acz-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                await WriteAczZip(root, zipPath);
                using var zip = ZipFile.OpenRead(zipPath);
                var names = zip.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
                Assert.That(names, Does.Not.Contain("Assemblies/" + staleName));
                Assert.That(names.Any(name => name.Contains("Content.VoiceChat.", StringComparison.Ordinal)), Is.False,
                    "The client zip must not contain a Content.VoiceChat assembly.");

                var served = names
                    .Where(name => name.StartsWith("Assemblies/", StringComparison.Ordinal) && name.EndsWith(".dll", StringComparison.Ordinal))
                    .Select(name => name["Assemblies/".Length..])
                    .ToList();
                Assert.That(served, Does.Contain("Content.Client.dll"));
                Assert.That(served, Does.Contain("Content.Shared.dll"));

                using var temp = new TempAssemblies();
                foreach (var dll in served)
                {
                    var entry = zip.GetEntry("Assemblies/" + dll) ?? zip.GetEntry("Assemblies\\" + dll);
                    Assert.That(entry, Is.Not.Null, dll);
                    entry!.ExtractToFile(Path.Combine(temp.Path, dll));
                }

                var engineDirs = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Select(assembly => Path.GetDirectoryName(assembly.Location)!)
                    .Distinct(StringComparer.Ordinal)
                    .Where(dir => !File.Exists(Path.Combine(dir, staleName)))
                    .ToArray();

                var sawmill = new CollectingSawmill();
                var checker = CreateChecker(sawmill, temp.Path, engineDirs);
                var check = checker.GetType().GetMethod("CheckAssembly", new[] { typeof(string) })!;
                foreach (var dll in served.Where(name => name.StartsWith("Content.", StringComparison.Ordinal)))
                {
                    sawmill.Errors.Clear();
                    var passed = (bool) check.Invoke(checker, new object[] { Path.Combine(temp.Path, dll) })!;
                    Assert.That(passed, Is.True, dll + " failed the launcher sandbox:\n" + string.Join('\n', sawmill.Errors));
                }
            }
            finally
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
        }
        finally
        {
            if (File.Exists(stalePath))
                File.Delete(stalePath);
        }
    }

    private static async Task WriteAczZip(string root, string zipPath)
    {
        await using var stream = File.Create(zipPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        var writer = new AssetPassZipWriter(zip) { Compression = CompressionLevel.NoCompression };
        await ClientPackaging.WriteResources(root, writer, new QuietPackageLogger(), default);
        await writer.FinishedTask;
    }

    private static object CreateChecker(ISawmill sawmill, string servedDir, string[] engineDirs)
    {
        var shared = typeof(IResourceManager).Assembly;
        var checkerType = shared.GetType("Robust.Shared.ContentPack.AssemblyTypeChecker")
            ?? throw new InvalidOperationException("AssemblyTypeChecker was not found.");
        var constructor = checkerType.GetConstructor(new[] { typeof(IResourceManager), typeof(ISawmill) })
            ?? throw new InvalidOperationException("AssemblyTypeChecker constructor was not found.");

        var resources = DispatchProxy.Create<IResourceManager, RejectingResourceManager>();
        var checker = constructor.Invoke(new object[] { resources, sawmill });

        checkerType.GetField("EngineModuleDirectories")!.SetValue(checker, engineDirs);
        checkerType.GetProperty("ExtraRobustLoader")!.SetValue(checker, new Func<string, Stream?>(name =>
        {
            var path = Path.Combine(servedDir, name);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }));

        return checker;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SpaceStation14.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory);
    }

    private sealed class QuietPackageLogger : IPackageLogger
    {
        public void Log(LogLevel level, string msg)
        {
        }
    }

    private sealed class TempAssemblies : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("launcher-acz-").FullName;

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Virtual]
    public class RejectingResourceManager : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            throw new InvalidOperationException(
                "The sandbox resolver tried to read a content file from the VFS (" + targetMethod?.Name +
                "). Served assemblies must be loaded through the client-zip set.");
        }
    }

    [Virtual]
    public class CollectingSawmill : ISawmill
    {
        public List<string> Errors { get; } = new();

        public string Name => "test.typecheck";
        public LogLevel? Level { get; set; }

        public void AddHandler(ILogHandler handler)
        {
        }

        public void RemoveHandler(ILogHandler handler)
        {
        }

        public void Log(LogLevel level, string message, params object?[] args)
        {
            Log(level, string.Format(message, args));
        }

        public void Log(LogLevel level, Exception? exception, string message, params object?[] args)
        {
            Log(level, message, args);
        }

        public void Log(LogLevel level, string message)
        {
            if (level >= LogLevel.Error)
                Errors.Add(message);
        }

        public void Debug(string message, params object?[] args)
        {
        }

        public void Debug(string message)
        {
        }

        public void Info(string message, params object?[] args)
        {
        }

        public void Info(string message)
        {
        }

        public void Warning(string message, params object?[] args)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message, params object?[] args)
        {
            Log(LogLevel.Error, message, args);
        }

        public void Error(string message)
        {
            Log(LogLevel.Error, message);
        }

        public void Fatal(string message, params object?[] args)
        {
            Log(LogLevel.Fatal, message, args);
        }

        public void Fatal(string message)
        {
            Log(LogLevel.Fatal, message);
        }
    }
}
