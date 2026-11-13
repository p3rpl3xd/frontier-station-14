#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Content.Packaging;
using NUnit.Framework;
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
    public void ServedClientAssembliesPassSandbox()
    {
        var root = FindRepoRoot();
        var bin = Path.Combine(root, "bin", "Content.Client");
        var served = ClientPackaging.ClientContentAssemblies;
        Assert.That(served, Does.Contain("Content.VoiceChat.Client"));
        Assert.That(served, Does.Contain("Content.VoiceChat.Shared"));
        Assert.That(served, Does.Not.Contain("NWaves"));

        using var temp = new TempAssemblies();
        foreach (var name in served)
        {
            var source = Path.Combine(bin, name + ".dll");
            Assert.That(File.Exists(source), Is.True, $"{name}.dll is missing from bin/Content.Client, so the client zip cannot serve it.");
            File.Copy(source, Path.Combine(temp.Path, name + ".dll"));
        }

        // Engine directories only. bin/Content.Client also contains the voice DLLs, and adding it
        // would hide a zip that forgot to ship them.
        var engineDirs = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Select(assembly => Path.GetDirectoryName(assembly.Location)!)
            .Distinct(StringComparer.Ordinal)
            .Where(dir => !File.Exists(Path.Combine(dir, "Content.VoiceChat.Shared.dll")))
            .ToArray();

        var sawmill = new CollectingSawmill();
        var checker = CreateChecker(sawmill, temp.Path, engineDirs);
        var check = checker.GetType().GetMethod("CheckAssembly", new[] { typeof(string) })!;

        foreach (var name in served)
        {
            sawmill.Errors.Clear();
            var path = Path.Combine(temp.Path, name + ".dll");
            var passed = (bool) check.Invoke(checker, new object[] { path })!;
            Assert.That(passed, Is.True, name + " failed the launcher sandbox:\n" + string.Join('\n', sawmill.Errors));
        }
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
