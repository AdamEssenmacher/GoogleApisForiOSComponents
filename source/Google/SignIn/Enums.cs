using System;
using ObjCRuntime;

namespace Google.SignIn
{

	[Native]
	public enum ErrorCode : long
	{
		Unknown = -1,
		Keychain = -2,
		HasNoAuthInKeychain = -4,
		Canceled = -5,
		ScopesAlreadyGranted = -8,
		MismatchWithCurrentUser = -9
	}

	[Native]
	public enum ButtonStyle : long
	{
		Standard = 0,
		Wide = 1,
		IconOnly = 2
	}

	[Native]
	public enum ButtonColorScheme : long
	{
		Dark = 0,
		Light = 1
	}

}

