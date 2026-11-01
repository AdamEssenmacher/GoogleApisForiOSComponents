#if IOS
using System;
using Firebase.Core;

namespace Firebase.AppCheck
{
    public partial class RecaptchaProvider
    {
        /// <summary>Creates a provider, preserving a native nil result for incomplete options.</summary>
        /// <remarks>Native configuration exceptions, including a missing site key, still propagate.</remarks>
        public static RecaptchaProvider? Create(App app)
        {
            ArgumentNullException.ThrowIfNull(app);
            using var factory = new RecaptchaProviderFactory();
            var provider = factory.CreateProviderWithApp(app);
            return provider switch {
                null => null,
                RecaptchaProvider recaptcha => recaptcha,
                _ => throw new InvalidOperationException("The native reCAPTCHA factory returned an unexpected provider type.")
            };
        }
    }
}
#endif
