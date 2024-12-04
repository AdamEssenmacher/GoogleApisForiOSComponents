using System;
using ObjCRuntime;

namespace MLKit.Core {
	[Native]
	public enum ImageSourceType : long {
		Image = 0,
		PixelBuffer = 1,
		SampleBuffer = 2
	}

	[Native]
	public enum ModelDownloadUserInfoKey : long
	{
		RemoteModel,
		Error
	}
}
