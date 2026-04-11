using System;
using ObjCRuntime;

namespace Firebase.RemoteConfig
{
	[Native]
	public enum RemoteConfigFetchStatus : long
	{
		NoFetchYet,
		Success,
		Failure,
		Throttled
	}

	[Native]
	public enum RemoteConfigFetchAndActivateStatus : long
	{
		SuccessFetchedFromRemote,
		SuccessUsingPreFetchedData,
		Error
	}

	[Native]
	public enum RemoteConfigError : long
	{
		Unknown = 8001,
		Throttled = 8002,
		InternalError = 8003
	}

	[Native]
	public enum RemoteConfigUpdateError : long
	{
		StreamError = 8001,
		NotFetched = 8002,
		MessageInvalid = 8003,
		Unavailable = 8004
	}

	[Native]
	public enum RemoteConfigCustomSignalsError : long
	{
		Unknown = 8101,
		InvalidValueType = 8102,
		LimitExceeded = 8103
	}

	[Native]
	public enum RemoteConfigSource : long
	{
		Remote,
		Default,
		Static
	}
}
