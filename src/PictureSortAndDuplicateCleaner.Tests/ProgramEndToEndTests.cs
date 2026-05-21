using System.Reflection;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// In-process tests that invoke the Cmd Program's compiler-generated entry point via
/// reflection so the top-level statements participate in coverage. Each test sets the
/// required PICSORT environment variables, calls Main, and inspects the returned exit code.
/// </summary>
[Collection("ProgramEntryPoint")] // serialize because Program mutates global state (Log.Logger, env vars, culture)
public sealed class ProgramEndToEndTests
{
    [Fact]
    public async Task Program_WithMissingPictureSource_ReturnsInvalidConfigurationExitCode()
    {
        using var scope = new EnvironmentVariableScope();
        scope.Set("PICTURE_SOURCE", null);
        scope.Set("PICTURE_TARGET", null);

        var exitCode = await InvokeProgramMainAsync();

        Assert.Equal(1, exitCode); // ExitCodes.InvalidConfiguration
    }

    [Fact]
    public async Task Program_WithMissingDirectories_ReturnsInvalidConfigurationExitCode()
    {
        using var scope = new EnvironmentVariableScope();
        scope.Set("PICTURE_SOURCE", Path.Combine(Path.GetTempPath(), "picsort-missing-" + Guid.NewGuid().ToString("N")));
        scope.Set("PICTURE_TARGET", Path.Combine(Path.GetTempPath(), "picsort-missing-" + Guid.NewGuid().ToString("N")));
        scope.Set("LOGGING_TARGET", Path.Combine(Path.GetTempPath(), "picsort-test-" + Guid.NewGuid().ToString("N") + ".log"));

        var exitCode = await InvokeProgramMainAsync();

        Assert.Equal(1, exitCode); // ExitCodes.InvalidConfiguration
    }

    [Fact]
    public async Task Program_WithValidEmptyDirectories_ReturnsSuccessExitCode()
    {
        var source = Path.Combine(Path.GetTempPath(), "picsort-e2e-src-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(Path.GetTempPath(), "picsort-e2e-tgt-" + Guid.NewGuid().ToString("N"));
        var loggingTarget = Path.Combine(Path.GetTempPath(), "picsort-e2e-log-" + Guid.NewGuid().ToString("N") + ".log");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(target);
        try
        {
            using var scope = new EnvironmentVariableScope();
            scope.Set("PICTURE_SOURCE", source);
            scope.Set("PICTURE_TARGET", target);
            scope.Set("LOGGING_TARGET", loggingTarget);
            scope.Set("INVENTOR_OF_THE_TARGET_DIRECTORY", "false");
            scope.Set("MAX_CONCURRENCY", "1");

            var exitCode = await InvokeProgramMainAsync();

            Assert.Equal(0, exitCode); // ExitCodes.Success
        }
        finally
        {
            TryDelete(source);
            TryDelete(target);
            try
            {
                var dir = Path.GetDirectoryName(loggingTarget)!;
                var prefix = Path.GetFileNameWithoutExtension(loggingTarget);
                foreach (var f in Directory.EnumerateFiles(dir, prefix + "*"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }

    private static async Task<int> InvokeProgramMainAsync()
    {
        var cmdAssembly = typeof(PictureSortAndDuplicateCleaner.Cmd.PictureSortEnvironmentOptions).Assembly;
        var programType = cmdAssembly.GetType("Program", throwOnError: true)!;
        var mainMethod = programType.GetMethod("<Main>$", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Top-level Main entry point not found on Program type.");
        var resultObj = mainMethod.Invoke(null, new object[] { Array.Empty<string>() })!;
        if (resultObj is Task<int> taskInt) return await taskInt.ConfigureAwait(false);
        if (resultObj is Task task) { await task.ConfigureAwait(false); return 0; }
        if (resultObj is int code) return code;
        throw new InvalidOperationException($"Unexpected return type from Main: {resultObj.GetType()}");
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private static readonly string[] ManagedVariables =
        {
            "PICTURE_SOURCE", "PICTURE_TARGET", "MAX_CONCURRENCY", "DUPLICATE_FOLDER_NAME",
            "ALREADY_EXISTING_FOLDER_NAME", "CULTURE_NAME", "INVENTOR_OF_THE_TARGET_DIRECTORY",
            "LOGGING_TARGET", "SIDECAR_EXTENSIONS", "JOURNAL_FILE", "FOLDER_TEMPLATE",
            "UNKNOWN_DATE_POLICY", "DRY_RUN", "OPERATION_MODE"
        };

        private readonly Dictionary<string, string?> _originals = new();

        public EnvironmentVariableScope()
        {
            foreach (var name in ManagedVariables)
            {
                _originals[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        public void Set(string name, string? value) => Environment.SetEnvironmentVariable(name, value);

        public void Dispose()
        {
            foreach (var kvp in _originals)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }
}

[CollectionDefinition("ProgramEntryPoint", DisableParallelization = true)]
public sealed class ProgramEntryPointCollection { }
