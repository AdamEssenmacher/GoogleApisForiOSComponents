namespace Google.SignIn
{
	public class Loader
	{
		public static void ForceLoad () {}
	}
}

namespace ApiDefinition
{
	partial class Messaging
	{
		static Messaging ()
		{
			Google.SignIn.Loader.ForceLoad ();
		}
	}
}

