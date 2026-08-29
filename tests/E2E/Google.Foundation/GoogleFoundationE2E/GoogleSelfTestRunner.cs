using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Foundation;

namespace GoogleFoundationE2E;

public static class GoogleSelfTestRunner
{
    const string ResultFileName = "google-foundation-e2e-result.json";

    public static async Task RunAsync(StatusViewController statusViewController)
    {
        var target = ReadAssemblyMetadata("GoogleE2ETarget") ?? "Places";

        var result = new GoogleE2ERunResult
        {
            BundleId = NSBundle.MainBundle.BundleIdentifier ?? string.Empty,
            Target = target,
            PackageVersion = ReadAssemblyMetadata("TargetPackageVersion"),
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        await statusViewController.AppendLineAsync("Target: " + target);
        await statusViewController.AppendLineAsync("Package version: " + (result.PackageVersion ?? "unknown"));

        try
        {
            // The trimmer warns (IL2045) that it may strip AssemblyMetadataAttribute. It currently
            // does not, but if that ever changes the version would silently read as null and an A/B
            // comparison between two package versions would look valid while comparing one package
            // against itself. Refuse to run rather than produce a result that cannot be trusted.
            if (string.IsNullOrWhiteSpace(result.PackageVersion))
            {
                throw new InvalidOperationException(
                    "TargetPackageVersion assembly metadata is missing, so this run cannot report " +
                    "which package version it exercised. Results would not be trustworthy.");
            }

            switch (target)
            {
#if ENABLE_TARGET_PLACES
                case "Places":
                    await PlacesCases.RunAsync(result, statusViewController, ExecuteCaseAsync);
                    break;
#endif
#if ENABLE_TARGET_MAPS
                case "Maps":
                    await MapsCases.RunAsync(result, statusViewController, ExecuteCaseAsync);
                    break;
#endif
                default:
                    throw new NotSupportedException(
                        $"No E2E cases are compiled in for target '{target}'. " +
                        "Build with -p:GoogleE2ETarget=<target>.");
            }
        }
        catch (Exception ex)
        {
            result.FatalError = ex.ToString();
            E2ELogger.WriteLine($"Unhandled E2E failure: {ex}");
            await statusViewController.AppendLineAsync("Unhandled failure: " + ex.Message);
        }

        result.CompletedAtUtc = DateTimeOffset.UtcNow;
        result.Success = string.IsNullOrWhiteSpace(result.FatalError)
            && result.Cases.Count > 0
            && result.Cases.All(c => c.Success);

        var indentedJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        var compactJson = JsonSerializer.Serialize(result);
        var resultFilePath = GetResultFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(resultFilePath)!);
        File.WriteAllText(resultFilePath, indentedJson);

        await statusViewController.AppendLineAsync(result.Success ? "All Google E2E checks passed." : "Google E2E checks failed.");
        await statusViewController.AppendLineAsync("Result file: " + resultFilePath);

        E2ELogger.WriteLine("E2E_RESULT:" + compactJson);
        E2ELogger.WriteLine("E2E_STATUS:" + (result.Success ? "PASS" : "FAIL"));
    }

    public static async Task ExecuteCaseAsync(
        GoogleE2ERunResult result,
        StatusViewController statusViewController,
        string name,
        Func<Task<string>> testCase)
    {
        await statusViewController.AppendLineAsync(string.Empty);
        await statusViewController.AppendLineAsync("Running " + name + "...");

        var caseResult = new GoogleE2ETestCaseResult
        {
            Name = name,
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            caseResult.Detail = await testCase();
            caseResult.Success = true;
            caseResult.Message = "OK";
            await statusViewController.AppendLineAsync("PASS " + name + ": " + caseResult.Detail);
        }
        catch (Exception ex)
        {
            caseResult.Success = false;
            caseResult.Message = ex.Message;
            caseResult.ExceptionType = ex.GetType().FullName;
            caseResult.Detail = ex.ToString();
            E2ELogger.WriteLine($"FAIL {name}: {ex}");
            await statusViewController.AppendLineAsync("FAIL " + name + ": " + ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            caseResult.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Cases.Add(caseResult);
        }
    }

    static string? ReadAssemblyMetadata(string key) =>
        typeof(GoogleSelfTestRunner).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;

    static string GetResultFilePath()
    {
        var cacheDirectory = NSSearchPath.GetDirectories(NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            cacheDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        return Path.Combine(cacheDirectory, ResultFileName);
    }
}
