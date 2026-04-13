using System.Diagnostics;
using System.Text.Json;
using Firebase.Analytics;
using Firebase.Core;
using Firebase.Installations;
using Foundation;

namespace FirebaseFoundationE2E;

public static class FirebaseSelfTestRunner
{
    const string ExpectedBundleId = "com.googleapisforioscomponents.tests.firebase.e2e";
    const string ResultFileName = "firebase-foundation-e2e-result.json";
    static readonly TimeSpan InstallationsTimeout = TimeSpan.FromSeconds(45);

    public static async Task RunAsync(StatusViewController statusViewController)
    {
        var result = new FirebaseE2ERunResult
        {
            BundleId = NSBundle.MainBundle.BundleIdentifier ?? ExpectedBundleId,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        await statusViewController.AppendLineAsync($"Bundle id: {result.BundleId}");
        await statusViewController.AppendLineAsync("Running Firebase binding smoke tests...");
#if ENABLE_NULLABILITY_VALIDATION
        await statusViewController.AppendLineAsync("Firebase nullability validation mode enabled.");
#endif
#if ENABLE_BINDING_SURFACE_COVERAGE
        await statusViewController.AppendLineAsync("Firebase binding surface coverage mode enabled.");
#endif
        var runtimeDriftCase = FirebaseRuntimeDriftCases.GetConfiguredCaseId();
        if (!string.IsNullOrWhiteSpace(runtimeDriftCase))
        {
            await statusViewController.AppendLineAsync($"Runtime drift case mode enabled: {runtimeDriftCase}");
        }

        try
        {
            await ExecuteCaseAsync(result, statusViewController, "ConfigureApp", async () =>
            {
                EnsureConfigFileIsBundled();
                App.Configure();

                var defaultApp = App.DefaultInstance ?? throw new InvalidOperationException("Firebase.Core.App.DefaultInstance returned null after App.Configure().");
                var options = Options.DefaultInstance;

                result.DefaultAppName = defaultApp.Name;
                result.GoogleAppId = options?.GoogleAppId;
                result.ProjectId = options?.ProjectId;

                return $"Configured app '{defaultApp.Name}'.";
            });

            if (!string.IsNullOrWhiteSpace(runtimeDriftCase))
            {
                await ExecuteCaseAsync(result, statusViewController, $"RuntimeDrift:{runtimeDriftCase}", () =>
                    FirebaseRuntimeDriftCases.ExecuteConfiguredCaseAsync());
            }
#if ENABLE_BINDING_SURFACE_COVERAGE
            else if (!string.IsNullOrWhiteSpace(FirebaseBindingSurfaceCoverage.GetConfiguredTarget()))
            {
                await ExecuteCaseAsync(result, statusViewController, "BindingSurfaceCoverage", () =>
                    FirebaseBindingSurfaceCoverage.VerifyConfiguredAsync(result));
            }
#endif
            else
            {
                await ExecuteCaseAsync(result, statusViewController, "CoreSurface", async () =>
                {
                    var defaultApp = App.DefaultInstance ?? throw new InvalidOperationException("Firebase.Core.App.DefaultInstance returned null.");
                    var firebaseVersion = App.FirebaseVersion;

                    if (string.IsNullOrWhiteSpace(firebaseVersion))
                    {
                        throw new InvalidOperationException("Firebase.Core.App.FirebaseVersion returned an empty value.");
                    }

                    result.FirebaseVersion = firebaseVersion;
                    return $"Firebase version: {firebaseVersion}; app name: {defaultApp.Name}.";
                });

#if ENABLE_NULLABILITY_VALIDATION
                await ExecuteCaseAsync(result, statusViewController, "CoreNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyCoreNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "AnalyticsNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyAnalyticsNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "AppCheckNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyAppCheckNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "CloudMessagingNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyCloudMessagingNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "CloudFirestoreNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyCloudFirestoreNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "CrashlyticsNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyCrashlyticsNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "DatabaseNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyDatabaseNullabilityAsync());

                await ExecuteCaseAsync(result, statusViewController, "PerformanceNullabilitySurface", () =>
                    FirebaseNullabilityValidation.VerifyPerformanceNullabilityAsync());
#endif

                await ExecuteCaseAsync(result, statusViewController, "InstallationsSurface", async () =>
                {
                    var installations = Installations.DefaultInstance ?? throw new InvalidOperationException("Firebase.Installations.Installations.DefaultInstance returned null.");
                    var installationId = await GetInstallationIdAsync(installations);
                    result.InstallationsIdPreview = installationId.Length > 16
                        ? installationId[..8] + "..." + installationId[^8..]
                        : installationId;
                    return $"Installation id: {result.InstallationsIdPreview}.";
                });

                await ExecuteCaseAsync(result, statusViewController, "AnalyticsSurface", async () =>
                {
                    Analytics.SetAnalyticsCollectionEnabled(true);
                    Analytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
                    {
                        [ConsentType.AnalyticsStorage] = ConsentStatus.Granted,
                        [ConsentType.AdStorage] = ConsentStatus.Denied,
                    });

                    Analytics.LogEvent(EventNamesConstants.ScreenView.ToString(), new Dictionary<object, object>
                    {
                        [ParameterNamesConstants.ScreenName] = new NSString("firebase_nuget_e2e"),
                        [ParameterNamesConstants.ScreenClass] = new NSString(nameof(FirebaseSelfTestRunner)),
                        [ParameterNamesConstants.Success] = NSNumber.FromBoolean(true),
                    });

                    return "Analytics collection and event APIs completed without throwing.";
                });
            }
        }
        catch (Exception ex)
        {
            result.FatalError = ex.ToString();
            E2ELogger.WriteLine($"Unhandled E2E failure: {ex}");
            await statusViewController.AppendLineAsync("Unhandled failure: " + ex.Message);
        }

        result.CompletedAtUtc = DateTimeOffset.UtcNow;
        result.Success = string.IsNullOrWhiteSpace(result.FatalError) && result.Cases.All(c => c.Success);

        var indentedJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        var compactJson = JsonSerializer.Serialize(result);
        var resultFilePath = GetResultFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(resultFilePath)!);
        File.WriteAllText(resultFilePath, indentedJson);

        await statusViewController.AppendLineAsync(result.Success ? "All Firebase E2E checks passed." : "Firebase E2E checks failed.");
        await statusViewController.AppendLineAsync("Result file: " + resultFilePath);

        E2ELogger.WriteLine("E2E_RESULT:" + compactJson);
        E2ELogger.WriteLine("E2E_STATUS:" + (result.Success ? "PASS" : "FAIL"));
    }

    static async Task ExecuteCaseAsync(
        FirebaseE2ERunResult result,
        StatusViewController statusViewController,
        string name,
        Func<Task<string>> testCase)
    {
        await statusViewController.AppendLineAsync(string.Empty);
        await statusViewController.AppendLineAsync("Running " + name + "...");

        var caseResult = new FirebaseE2ETestCaseResult
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
            await statusViewController.AppendLineAsync("FAIL " + name + ": " + ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            caseResult.DurationMs = stopwatch.ElapsedMilliseconds;
            result.Cases.Add(caseResult);
        }

        if (!caseResult.Success)
        {
            throw new InvalidOperationException($"{name} failed: {caseResult.Message}");
        }
    }

    static void EnsureConfigFileIsBundled()
    {
        var configPath = NSBundle.MainBundle.PathForResource("GoogleService-Info", "plist");
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            throw new InvalidOperationException("GoogleService-Info.plist was not found in the app bundle.");
        }

        if (!string.Equals(NSBundle.MainBundle.BundleIdentifier, ExpectedBundleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unexpected bundle id '{NSBundle.MainBundle.BundleIdentifier}'. Expected '{ExpectedBundleId}'.");
        }
    }

    static async Task<string> GetInstallationIdAsync(Installations installations)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        installations.GetInstallationId((identifier, error) =>
        {
            if (error is not null)
            {
                tcs.TrySetException(new InvalidOperationException("Firebase Installations returned an error: " + error.LocalizedDescription));
                return;
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                tcs.TrySetException(new InvalidOperationException("Firebase Installations returned an empty installation id."));
                return;
            }

            tcs.TrySetResult(identifier);
        });

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(InstallationsTimeout));
        if (completedTask != tcs.Task)
        {
            throw new TimeoutException($"Firebase Installations did not complete within {InstallationsTimeout.TotalSeconds} seconds.");
        }

        return await tcs.Task;
    }

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
