using System;
using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
namespace Firebase.CloudFunctions {
	public partial class CloudFunctions {
		static string? currentVersion;
		// The current native SDK only exposes useEmulatorWithHost:port: to ObjC, so preserve the
		// managed emulatorOrigin/useFunctionsEmulatorOrigin API by caching the last requested origin
		// on the native FIRFunctions instance itself.
		static readonly NSString emulatorOriginAssociationKey = new ("Firebase.CloudFunctions.EmulatorOrigin");
		const nuint AssociationPolicyRetainNonAtomic = 1;

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern IntPtr objc_getAssociatedObject (IntPtr obj, IntPtr key);

		[DllImport (Constants.ObjectiveCLibrary)]
		static extern void objc_setAssociatedObject (IntPtr obj, IntPtr key, IntPtr value, nuint policy);

		public static string CurrentVersion {
			get {
				if (currentVersion == null) {
					IntPtr RTLD_MAIN_ONLY = Dlfcn.dlopen (null, 0);
					IntPtr ptr = Dlfcn.dlsym (RTLD_MAIN_ONLY, "FirebaseCloudFunctionsVersionStr");
						currentVersion = Marshal.PtrToStringAnsi (ptr) ?? string.Empty;
						Dlfcn.dlclose (RTLD_MAIN_ONLY);
					}

					return currentVersion ?? string.Empty;
				}
			}

		public const string CloudFunctionsErrorDomain = "com.firebase.functions";
		public const string CloudFunctionsErrorDetailsKey = "details";

		public string? EmulatorOrigin {
			get {
				var handle = objc_getAssociatedObject (Handle, emulatorOriginAssociationKey.Handle);
				if (handle == IntPtr.Zero)
					return null;

				var value = Runtime.GetNSObject<NSString> (handle, false);
				return value?.ToString ();
			}
		}

		public void UseFunctionsEmulatorOrigin (string origin)
		{
			var (host, port) = ParseEmulatorOrigin (origin);
			UseEmulatorOriginWithHost (host, port);
			SetCachedEmulatorOrigin (origin);
		}

		public void UseEmulatorOriginWithHost (string host, uint port)
		{
			if (string.IsNullOrWhiteSpace (host))
				throw new ArgumentException ("Host must not be null or empty.", nameof (host));

			_UseEmulatorOriginWithHost (host, checked ((nint) port));
			SetCachedEmulatorOrigin ($"{host}:{port}");
		}

		void SetCachedEmulatorOrigin (string origin)
		{
			using var value = new NSString (origin);
			objc_setAssociatedObject (Handle, emulatorOriginAssociationKey.Handle, value.Handle, AssociationPolicyRetainNonAtomic);
		}

		static (string Host, uint Port) ParseEmulatorOrigin (string origin)
		{
			if (string.IsNullOrWhiteSpace (origin))
				throw new ArgumentException ("Origin must not be null or empty.", nameof (origin));

			if (TryParseOrigin (origin, out var host, out var port))
				return (host, port);

			if (TryParseOrigin ($"http://{origin}", out host, out port))
				return (host, port);

			throw new ArgumentException ("Expected a Cloud Functions emulator origin in 'host:port' or absolute URL form.", nameof (origin));
		}

		static bool TryParseOrigin (string origin, out string host, out uint port)
		{
			host = string.Empty;
			port = 0;

			if (!Uri.TryCreate (origin, UriKind.Absolute, out var uri))
				return false;

			if (string.IsNullOrWhiteSpace (uri.Host) || uri.Port <= 0)
				return false;

			host = uri.Host;
			port = checked ((uint) uri.Port);
			return true;
		}
	}
}
