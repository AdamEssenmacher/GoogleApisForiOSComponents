using System;
using UIKit;
using Foundation;
using ObjCRuntime;

#if !NET
using NativeHandle = System.IntPtr;
#endif

namespace Google.SignIn
{
	// @interface GIDConfiguration : NSObject <NSCopying, NSSecureCoding>
	[BaseType (typeof (NSObject), Name = "GIDConfiguration")]
	[DisableDefaultCtor]
	interface Configuration : INSCopying, INSSecureCoding
	{
		// @property (readonly, nonatomic) NSString * _Nonnull clientID;
		[Export ("clientID")]
		string ClientId { get; }

		// @property (readonly, nonatomic) NSString * _Nullable serverClientID;
		[NullAllowed, Export ("serverClientID")]
		string ServerClientId { get; }

		// @property (readonly, nonatomic) NSString * _Nullable hostedDomain;
		[NullAllowed, Export ("hostedDomain")]
		string HostedDomain { get; }

		// @property (readonly, nonatomic) NSString * _Nullable openIDRealm;
		[NullAllowed, Export ("openIDRealm")]
		string OpenIDRealm { get; }

		// -(instancetype _Nonnull)initWithClientID:(NSString * _Nonnull)clientID;
		[Export ("initWithClientID:")]
		NativeHandle Constructor (string clientId);

		// -(instancetype _Nonnull)initWithClientID:(NSString * _Nonnull)clientID serverClientID:(NSString * _Nullable)serverClientID;
		[Export ("initWithClientID:serverClientID:")]
		NativeHandle Constructor (string clientId, [NullAllowed] string serverClientId);

		// -(instancetype _Nonnull)initWithClientID:(NSString * _Nonnull)clientID serverClientID:(NSString * _Nullable)serverClientID hostedDomain:(NSString * _Nullable)hostedDomain openIDRealm:(NSString * _Nullable)openIDRealm __attribute__((objc_designated_initializer));
		[Export ("initWithClientID:serverClientID:hostedDomain:openIDRealm:")]
		[DesignatedInitializer] 
		NativeHandle Constructor (string clientId, [NullAllowed] string serverClientId, [NullAllowed] string hostedDomain, [NullAllowed] string openIDRealm);
	}

	//@interface GIDGoogleUser : NSObject <NSSecureCoding>
	[BaseType (typeof (NSObject), Name = "GIDGoogleUser")]
	interface GoogleUser : INSSecureCoding
	{
		// @property (readonly, nonatomic) NSString * _Nullable userID;
		[NullAllowed, Export ("userID")]
		string UserId { get; }

		// @property (readonly, nonatomic) GIDProfileData * _Nullable profile;
		[NullAllowed, Export ("profile")]
		ProfileData Profile { get; }

		// @property (readonly, nonatomic) NSArray<NSString *> * _Nullable grantedScopes;
		[NullAllowed, Export ("grantedScopes")]
		string[] GrantedScopes { get; }

		// @property (readonly, nonatomic) GIDConfiguration * _Nonnull configuration;
		[Export ("configuration")]
		Configuration Configuration { get; }

		// @property (readonly, nonatomic) GIDToken * _Nonnull accessToken;
		[Export ("accessToken")]
		Token AccessToken { get; }

		// @property (readonly, nonatomic) GIDToken * _Nonnull refreshToken;
		[Export ("refreshToken")]
		Token RefreshToken { get; }

		 // @property (readonly, nonatomic) GIDToken * _Nullable idToken;
		[NullAllowed, Export ("idToken")]
		Token IdToken { get; }

		// @property (readonly, nonatomic) id<GTMFetcherAuthorizationProtocol> _Nonnull fetcherAuthorizer;
		[Export ("fetcherAuthorizer")]
		//FetcherAuthorizationProtocol FetcherAuthorizer { get; }]
		// TODO: This opens a can of worms where GTMSessionFetcher needs to be bound at least partially.
		NSObject FetcherAuthorizer { get; }

		// -(void)refreshTokensIfNeededWithCompletion:(void (^ _Nonnull)(GIDGoogleUser * _Nullable, NSError * _Nullable))completion;
		[Export ("refreshTokensIfNeededWithCompletion:")]
		void RefreshTokensIfNeededWithCompletion (Action<GoogleUser, NSError> completion);

		// -(void)addScopes:(NSArray<NSString *> * _Nonnull)scopes presentingViewController:(UIViewController * _Nonnull)presentingViewController completion:(void (^ _Nullable)(GIDSignInResult * _Nullable, NSError * _Nullable))completion __attribute__((availability(macos_app_extension, unavailable))) __attribute__((availability(ios_app_extension, unavailable)));
		//[Unavailable (PlatformName.MacOSXAppExtension)]
		//[Unavailable (PlatformName.iOSAppExtension)]
		[Export ("addScopes:presentingViewController:completion:")]
		void AddScopes (string[] scopes, UIViewController presentingViewController, [NullAllowed] Action<SignInResult, NSError> completion);
	}

	// @interface GIDProfileData : NSObject <NSCopying, NSSecureCoding>
	[BaseType (typeof (NSObject), Name = "GIDProfileData")]
	interface ProfileData : INSCopying, INSSecureCoding
	{
		// @property (readonly, nonatomic) NSString * _Nonnull email;
		[Export ("email")]
		string Email { get; }

		// @property (readonly, nonatomic) NSString * _Nonnull name;
		[Export ("name")]
		string Name { get; }

		// @property (readonly, nonatomic) NSString * _Nullable givenName;
		[NullAllowed, Export ("givenName")]
		string GivenName { get; }

		// @property (readonly, nonatomic) NSString * _Nullable familyName;
		[NullAllowed, Export ("familyName")]
		string FamilyName { get; }

		// @property (readonly, nonatomic) BOOL hasImage;
		[Export ("hasImage")]
		bool HasImage { get; }

		// -(NSURL * _Nullable)imageURLWithDimension:(NSUInteger)dimension;
		[Export ("imageURLWithDimension:")]
		[return: NullAllowed]
		NSUrl GetImageUrl (nuint dimension);
	}

	// @interface GIDSignIn : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GIDSignIn")]
	interface SignIn
	{
		// @property (readonly, nonatomic, class) GIDSignIn * _Nonnull sharedInstance;
		[Static]
		[Export ("sharedInstance")]
		SignIn SharedInstance { get; }

		// @property (readonly, nonatomic) GIDGoogleUser * _Nullable currentUser;
		[NullAllowed, Export ("currentUser")]
		GoogleUser CurrentUser { get; }

		// @property (nonatomic) GIDConfiguration * _Nullable configuration;
		[NullAllowed, Export ("configuration", ArgumentSemantic.Assign)]
		Configuration Configuration { get; set; }

		// -(BOOL)handleURL:(NSURL * _Nonnull)url;
		[Export ("handleURL:")]
		bool HandleUrl (NSUrl url);
		
		// -(BOOL)hasPreviousSignIn;
		[Export ("hasPreviousSignIn")]
		bool HasPreviousSignIn { get; }

		// -(void)restorePreviousSignInWithCompletion:(void (^ _Nullable)(GIDGoogleUser * _Nullable, NSError * _Nullable))completion;
		[Export ("restorePreviousSignInWithCompletion:")]
		void RestorePreviousSignInWithCompletion ([NullAllowed] Action<GoogleUser, NSError> completion);

		// -(void)signOut;
		[Export ("signOut")]
		void SignOutUser ();

		// -(void)disconnectWithCompletion:(void (^ _Nullable)(NSError * _Nullable))completion;
		[Export ("disconnectWithCompletion:")]
		void DisconnectWithCompletion ([NullAllowed] Action<NSError> completion);

		// -(void)signInWithPresentingViewController:(UIViewController * _Nonnull)presentingViewController completion:(void (^ _Nullable)(GIDSignInResult * _Nullable, NSError * _Nullable))completion __attribute__((availability(macos_app_extension, unavailable))) __attribute__((availability(ios_app_extension, unavailable)));
		//[Unavailable (PlatformName.MacOSXAppExtension)]
		//[Unavailable (PlatformName.iOSAppExtension)]
		[Export ("signInWithPresentingViewController:completion:")]
		void SignInWithPresentingViewController (UIViewController presentingViewController, [NullAllowed] Action<SignInResult, NSError> completion);

		// -(void)signInWithPresentingViewController:(UIViewController * _Nonnull)presentingViewController hint:(NSString * _Nullable)hint completion:(void (^ _Nullable)(GIDSignInResult * _Nullable, NSError * _Nullable))completion __attribute__((availability(macos_app_extension, unavailable))) __attribute__((availability(ios_app_extension, unavailable)));
		//[Unavailable (PlatformName.MacOSXAppExtension)]
		//[Unavailable (PlatformName.iOSAppExtension)]
		[Export ("signInWithPresentingViewController:hint:completion:")]
		void SignInWithPresentingViewController (UIViewController presentingViewController, [NullAllowed] string hint, [NullAllowed] Action<SignInResult, NSError> completion);

		// -(void)signInWithPresentingViewController:(UIViewController * _Nonnull)presentingViewController hint:(NSString * _Nullable)hint additionalScopes:(NSArray<NSString *> * _Nullable)additionalScopes completion:(void (^ _Nullable)(GIDSignInResult * _Nullable, NSError * _Nullable))completion __attribute__((availability(macos_app_extension, unavailable))) __attribute__((availability(ios_app_extension, unavailable)));
		//[Unavailable (PlatformName.MacOSXAppExtension)]
		//[Unavailable (PlatformName.iOSAppExtension)]
		[Export ("signInWithPresentingViewController:hint:additionalScopes:completion:")]
		void SignInWithPresentingViewController (UIViewController presentingViewController, [NullAllowed] string hint, [NullAllowed] string[] additionalScopes, [NullAllowed] Action<SignInResult, NSError> completion);
	}

	// @interface GIDSignInButton : UIControl
	[BaseType (typeof (UIControl), Name = "GIDSignInButton")]
	interface SignInButton
	{
		// @property (assign, nonatomic) GIDSignInButtonStyle style;
		[Export ("style", ArgumentSemantic.Assign)]
		ButtonStyle Style { get; set; }

		// @property (assign, nonatomic) GIDSignInButtonColorScheme colorScheme;
		[Export ("colorScheme", ArgumentSemantic.Assign)]
		ButtonColorScheme ColorScheme { get; set; }
	}
	
	// @interface GIDSignInResult : NSObject
	[BaseType (typeof(NSObject), Name = "GIDSignInResult")]
	[DisableDefaultCtor]
	interface SignInResult
	{
		// @property (readonly, nonatomic) GIDGoogleUser * _Nonnull user;
		[Export ("user")]
		GoogleUser User { get; }
	
		// @property (readonly, nonatomic) NSString * _Nullable serverAuthCode;
		[NullAllowed, Export ("serverAuthCode")]
		string ServerAuthCode { get; }
	}
	
	// @interface GIDToken : NSObject <NSSecureCoding>
	[BaseType (typeof(NSObject), Name = "GIDToken")]
	[DisableDefaultCtor]
	interface Token : INSSecureCoding
	{
		// @property (readonly, copy, nonatomic) NSString * _Nonnull tokenString;
		[Export ("tokenString")]
		string TokenString { get; }
	
		// @property (readonly, nonatomic) NSDate * _Nullable expirationDate;
		[NullAllowed, Export ("expirationDate")]
		NSDate ExpirationDate { get; }
	
		// -(BOOL)isEqualToToken:(GIDToken * _Nonnull)otherToken;
		[Export ("isEqualToToken:")]
		bool IsEqualToToken (Token otherToken);
	}
}

