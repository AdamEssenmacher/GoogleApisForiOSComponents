namespace Firebase.AppCheck;

public partial class AppCheck
{
	public static void SetAppCheckProviderFactory (AppCheckDebugProviderFactory factory)
	{
		SetAppCheckProviderFactory ((IAppCheckProviderFactory) factory);
	}

	public static void SetAppCheckProviderFactory (DeviceCheckProviderFactory factory)
	{
		SetAppCheckProviderFactory ((IAppCheckProviderFactory) factory);
	}
}
