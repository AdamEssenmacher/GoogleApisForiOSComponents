#if ENABLE_TARGET_MAPS
using System.Runtime.InteropServices;
using CoreLocation;
using Foundation;
using ObjCRuntime;
using MapsMapView = Google.Maps.MapView;

namespace GoogleFoundationE2E;

/// <summary>
/// Delivery-focused checks for AdamE.Google.iOS.Maps.
///
/// These checks require neither an API key nor network access. They prove that the managed binding,
/// native symbols, Objective-C classes, and packaged resources reached the app without initializing
/// the Maps service or contacting its backend.
/// </summary>
public static class MapsCases
{
    const string BundleName = "GoogleMaps";
    const int ExpectedBundleFileCount = 190;

    public static async Task RunAsync(
        GoogleE2ERunResult result,
        StatusViewController status,
        Func<GoogleE2ERunResult, StatusViewController, string, Func<Task<string>>, Task> execute)
    {
        await execute(result, status, "maps-managed-binding-loads", VerifyManagedBindingLoadsAsync);
        await execute(result, status, "maps-pinvoke-linkage", VerifyPInvokeLinkageAsync);
        await execute(result, status, "maps-objc-class-lookup", VerifyObjCClassLookupAsync);
        await execute(result, status, "maps-resource-bundle-present", VerifyResourceBundlePresentAsync);
        await execute(result, status, "maps-resource-bundle-contents", VerifyResourceBundleContentsAsync);
    }

    static Task<string> VerifyManagedBindingLoadsAsync()
    {
        var type = typeof(MapsMapView);
        var name = type.Assembly.GetName();

        return Task.FromResult($"{type.FullName} loaded from {name.Name} {name.Version}.");
    }

    [DllImport("__Internal", EntryPoint = "GMSGeometryDistance")]
    static extern double GMSGeometryDistance(
        CLLocationCoordinate2D fromCoordinate,
        CLLocationCoordinate2D toCoordinate);

    static Task<string> VerifyPInvokeLinkageAsync()
    {
        var fromCoordinate = new CLLocationCoordinate2D(37.7749, -122.4194);
        var toCoordinate = new CLLocationCoordinate2D(37.7849, -122.4094);

        try
        {
            var distance = GMSGeometryDistance(fromCoordinate, toCoordinate);
            if (!double.IsFinite(distance) || distance <= 0)
            {
                throw new InvalidOperationException(
                    $"GMSGeometryDistance resolved but returned invalid distance {distance}.");
            }

            return Task.FromResult($"GMSGeometryDistance resolved and returned {distance:F2} meters.");
        }
        catch (EntryPointNotFoundException ex)
        {
            throw new InvalidOperationException(
                "GMSGeometryDistance did not resolve, so the GoogleMaps native library was not " +
                "linked into the app. This is a package delivery failure.", ex);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "The __Internal native library was not found, so the GoogleMaps native framework " +
                "was not linked into the app. This is a package delivery failure.", ex);
        }
    }

    static Task<string> VerifyObjCClassLookupAsync()
    {
        string[] expectedClasses = ["GMSMapView", "GMSCameraPosition", "GMSMarker"];

        var missing = expectedClasses
            .Where(name => (IntPtr)Class.GetHandle(name) == IntPtr.Zero)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Objective-C classes missing from the loaded image: " + string.Join(", ", missing) +
                ". ForceLoad or the -ObjC linker flag did not survive packaging.");
        }

        return Task.FromResult(
            $"Resolved {expectedClasses.Length} Objective-C classes: {string.Join(", ", expectedClasses)}.");
    }

    static Task<string> VerifyResourceBundlePresentAsync()
    {
        var appBundlePath = NSBundle.MainBundle.BundlePath;
        var expectedBundlePath = Path.Combine(appBundlePath, BundleName + ".bundle");
        var matches = Directory
            .GetDirectories(appBundlePath, BundleName + ".bundle", SearchOption.AllDirectories)
            .Select(Path.GetFullPath)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {BundleName}.bundle in the app, found {matches.Length}: " +
                string.Join(", ", matches));
        }

        if (!string.Equals(matches[0], Path.GetFullPath(expectedBundlePath), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle was delivered at {matches[0]}, expected the app root at " +
                $"{expectedBundlePath}.");
        }

        return Task.FromResult($"Exactly one root {BundleName}.bundle resolved at {matches[0]}.");
    }

    static Task<string> VerifyResourceBundleContentsAsync()
    {
        var bundlePath = Path.Combine(NSBundle.MainBundle.BundlePath, BundleName + ".bundle");
        if (!Directory.Exists(bundlePath))
        {
            throw new InvalidOperationException($"{BundleName}.bundle was not found at the app root.");
        }

        string[] expectedFiles =
        [
            "Info.plist",
            "PrivacyInfo.xcprivacy",
            "Assets.car",
            Path.Combine("GMSCacheStorage.momd", "Storage.mom"),
            Path.Combine("GMSCoreResources.bundle", "en.lproj", "GMSCore.strings"),
        ];

        var missingOrEmpty = expectedFiles
            .Where(relative =>
            {
                var path = Path.Combine(bundlePath, relative);
                return !File.Exists(path) || new FileInfo(path).Length == 0;
            })
            .ToArray();

        if (missingOrEmpty.Length > 0)
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle is missing expected non-empty files: " +
                string.Join(", ", missingOrEmpty));
        }

        var fileCount = Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories).Length;
        if (fileCount != ExpectedBundleFileCount)
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle contains {fileCount} files; expected the " +
                $"{ExpectedBundleFileCount}-file Maps 9.2.0 delivery baseline.");
        }

        return Task.FromResult(
            $"{BundleName}.bundle contains {fileCount} files including all " +
            $"{expectedFiles.Length} spot-checked entries.");
    }
}
#endif
