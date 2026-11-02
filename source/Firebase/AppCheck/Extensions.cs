#if IOS
using System;
using Firebase.Core;

namespace Firebase.AppCheck
{
    public partial class RecaptchaProvider
    {
        /// <summary>Creates a provider, preserving a native nil result for incomplete options.</summary>
        /// <remarks>
        /// Firebase 12.17 moved the site key out of <see cref="Firebase.Core.Options"/> and into the
        /// factory initializer, so it is supplied here. Native configuration exceptions, including an
        /// empty site key, still propagate.
        /// </remarks>
        public static RecaptchaProvider? Create(App app, string siteKey)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentNullException.ThrowIfNull(siteKey);
            using var factory = new RecaptchaProviderFactory(siteKey);
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
