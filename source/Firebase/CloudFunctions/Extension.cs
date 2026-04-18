using System;
using System.Runtime.InteropServices;
using ObjCRuntime;
namespace Firebase.CloudFunctions {
	public partial class CloudFunctions {
		static string? currentVersion;
		public static string CurrentVersion {
			get {
				if (currentVersion == null) {
					IntPtr RTLD_MAIN_ONLY = Dlfcn.dlopen (null, 0);
					if (RTLD_MAIN_ONLY == IntPtr.Zero)
						throw new InvalidOperationException ("Unable to open the main program handle.");

					try {
						IntPtr ptr = Dlfcn.dlsym (RTLD_MAIN_ONLY, "FirebaseFunctionsVersionString");
						if (ptr == IntPtr.Zero)
							throw new InvalidOperationException ("Unable to resolve FirebaseFunctionsVersionString.");

						currentVersion = Marshal.PtrToStringAnsi (ptr)
							?? throw new InvalidOperationException ("Unable to read FirebaseFunctionsVersionString.");
					} finally {
						Dlfcn.dlclose (RTLD_MAIN_ONLY);
					}
				}

				return currentVersion;
			}
		}

		public const string CloudFunctionsErrorDomain = "com.firebase.functions";
		public const string CloudFunctionsErrorDetailsKey = "details";
	}
}
