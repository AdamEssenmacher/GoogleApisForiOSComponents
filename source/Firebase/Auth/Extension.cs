using System;
using System.Runtime.InteropServices;
using ObjCRuntime;
namespace Firebase.Auth
{
	public partial class Auth {
		static string? currentVersion;
		public static string CurrentVersion { 
			get {
				if (currentVersion == null) {
					IntPtr RTLD_MAIN_ONLY = Dlfcn.dlopen (null, 0);
					if (RTLD_MAIN_ONLY == IntPtr.Zero)
						throw new InvalidOperationException ("Unable to open the main program handle.");

					try {
						IntPtr ptr = Dlfcn.dlsym (RTLD_MAIN_ONLY, "FirebaseAuthVersionStr");
						if (ptr == IntPtr.Zero)
							throw new InvalidOperationException ("Unable to resolve FirebaseAuthVersionStr.");

						currentVersion = Marshal.PtrToStringAnsi (ptr)
							?? throw new InvalidOperationException ("Unable to read FirebaseAuthVersionStr.");
					} finally {
						Dlfcn.dlclose (RTLD_MAIN_ONLY);
					}
				}

				return currentVersion;
			}
		}
	}
}
