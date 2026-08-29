#if ENABLE_TARGET_PLACES
using System.Runtime.InteropServices;
using CoreLocation;
using Foundation;
using Google.Places;
using ObjCRuntime;

namespace GoogleFoundationE2E;

/// <summary>
/// Delivery-focused checks for AdamE.Google.iOS.Places.
///
/// These validate that the native SDK reached the app bundle and linked, not that the Places
/// backend works. Every check is keyless on purpose: CI must not need an API key. Native or
/// configuration errors are acceptable; binding-layer failures are not.
/// </summary>
public static class PlacesCases
{
    const string BundleName = "GooglePlaces";
    const int ExpectedBundleFileCount = 59;

    public static async Task RunAsync(
        GoogleE2ERunResult result,
        StatusViewController status,
        Func<GoogleE2ERunResult, StatusViewController, string, Func<Task<string>>, Task> execute)
    {
        await execute(result, status, "places-managed-binding-loads", VerifyManagedBindingLoadsAsync);
        await execute(result, status, "places-pinvoke-linkage", VerifyPInvokeLinkageAsync);
        await execute(result, status, "places-objc-class-lookup", VerifyObjCClassLookupAsync);
        await execute(result, status, "places-resource-bundle-present", VerifyResourceBundlePresentAsync);
        await execute(result, status, "places-resource-bundle-contents", VerifyResourceBundleContentsAsync);
    }

    /// <summary>
    /// Loads a type from the managed binding assembly. Beyond checking the assembly shipped, this
    /// keeps a hard reference to it: an app that never touches the binding lets the trimmer drop the
    /// assembly, and with it the native payload, which would make every check below fail for a
    /// reason that has nothing to do with packaging.
    /// </summary>
    static Task<string> VerifyManagedBindingLoadsAsync()
    {
        var type = typeof(AutocompleteFilter);
        var name = type.Assembly.GetName();

        return Task.FromResult($"{type.FullName} loaded from {name.Name} {name.Version}.");
    }

    /// <summary>
    /// Resolves a plain C symbol out of the GooglePlaces static framework. The symbol only exists in
    /// the app binary if the xcframework actually linked, so EntryPointNotFoundException is the
    /// precise signature of a delivery failure.
    ///
    /// This declares its own P/Invoke rather than calling the binding's
    /// AutocompleteFilter.PlaceRectangularLocationOption, because that binding declares an NSObject
    /// return type, which the marshaller rejects with MarshalDirectiveException while generating the
    /// IL stub -- before the native symbol is ever looked up. Routing through it would make this case
    /// pass without proving anything. (That binding bug predates the packaging change and reproduces
    /// on the XBD-delivered package; it is tracked separately.)
    /// </summary>
    [DllImport("__Internal", EntryPoint = "GMSPlaceRectangularLocationOption")]
    static extern IntPtr GMSPlaceRectangularLocationOption(
        CLLocationCoordinate2D northEastBounds,
        CLLocationCoordinate2D southWestBounds);

    static Task<string> VerifyPInvokeLinkageAsync()
    {
        var northEast = new CLLocationCoordinate2D(37.785, -122.395);
        var southWest = new CLLocationCoordinate2D(37.775, -122.415);

        try
        {
            var handle = GMSPlaceRectangularLocationOption(northEast, southWest);
            return Task.FromResult(
                $"GMSPlaceRectangularLocationOption resolved and returned handle 0x{handle:x}.");
        }
        catch (EntryPointNotFoundException ex)
        {
            throw new InvalidOperationException(
                "GMSPlaceRectangularLocationOption did not resolve, so the GooglePlaces native library " +
                "was not linked into the app. This is a package delivery failure.", ex);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "The __Internal native library was not found, so the GooglePlaces static framework " +
                "was not linked into the app. This is a package delivery failure.", ex);
        }
    }

    /// <summary>
    /// Proves the Objective-C classes are present in the loaded image, which is what ForceLoad and
    /// the -ObjC linker flag exist to guarantee. Pure runtime lookup: no SDK initialization, no key.
    /// </summary>
    static Task<string> VerifyObjCClassLookupAsync()
    {
        string[] expectedClasses = ["GMSPlacesClient", "GMSPlace", "GMSAutocompleteFilter"];

        var missing = expectedClasses
            .Where(name => (IntPtr)Class.GetHandle(name) == IntPtr.Zero)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Objective-C classes missing from the loaded image: " + string.Join(", ", missing) +
                ". ForceLoad or the -ObjC linker flag did not survive packaging.");
        }

        return Task.FromResult($"Resolved {expectedClasses.Length} Objective-C classes: {string.Join(", ", expectedClasses)}.");
    }

    /// <summary>
    /// GooglePlaces.xcframework is static, so its internal Resources are not copied automatically.
    /// The package has to place GooglePlaces.bundle at the app root itself. This is the check with
    /// no build-time failure mode: get it wrong and the SDK silently loses its assets and strings.
    /// </summary>
    static Task<string> VerifyResourceBundlePresentAsync()
    {
        var bundlePath = NSBundle.MainBundle.PathForResource(BundleName, "bundle");

        if (string.IsNullOrWhiteSpace(bundlePath) || !Directory.Exists(bundlePath))
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle was not found in the app bundle. The package did not deliver its " +
                "BundleResource items.");
        }

        return Task.FromResult($"{BundleName}.bundle resolved at {bundlePath}.");
    }

    static Task<string> VerifyResourceBundleContentsAsync()
    {
        var bundlePath = NSBundle.MainBundle.PathForResource(BundleName, "bundle");

        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            throw new InvalidOperationException($"{BundleName}.bundle was not found in the app bundle.");
        }

        // A spot check across the three content shapes in the bundle: a loose data file, a
        // localized strings file, and an image asset.
        string[] expectedFiles =
        [
            "oss_licenses_places.txt.gz",
            Path.Combine("en.lproj", "GooglePlaces.strings"),
            "sad_cloud@2x.png",
        ];

        var missing = expectedFiles
            .Where(relative => !File.Exists(Path.Combine(bundlePath, relative)))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle is present but missing expected files: " + string.Join(", ", missing));
        }

        var fileCount = Directory.GetFiles(bundlePath, "*", SearchOption.AllDirectories).Length;

        if (fileCount != ExpectedBundleFileCount)
        {
            throw new InvalidOperationException(
                $"{BundleName}.bundle contains {fileCount} files; expected the " +
                $"{ExpectedBundleFileCount}-file Places 7.4.0 delivery baseline.");
        }

        return Task.FromResult($"{BundleName}.bundle contains {fileCount} files including all {expectedFiles.Length} spot-checked entries.");
    }
}
#endif
