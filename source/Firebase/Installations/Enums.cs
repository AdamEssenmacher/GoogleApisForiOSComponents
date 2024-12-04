using System;
using ObjCRuntime;

namespace Firebase.Installations
{
	[Native]
	public enum InstallationsErrorCode : ulong
	{
		// Unknown error. See userInfo for details.
		Unknown = 0,
		// Keychain error. See userInfo for details.
		Keychain = 1,
		// Server unreachable. A network error or server is unavailable. See userInfo for details.
		ServerUnreachable = 2,
		// FirebaseApp configuration issues e.g. invalid GMP-App-ID, etc. See userInfo for details.
		InvalidConfiguration = 3
	}
}
