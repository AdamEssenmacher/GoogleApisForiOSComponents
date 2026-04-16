using System;

using UIKit;
using Foundation;
using ObjCRuntime;
using CoreGraphics;
using Firebase.Core;

namespace Firebase.CloudFunctions
{
	// @interface FIRFunctions : NSObject
	[DisableDefaultCtor]
	[BaseType(typeof(NSObject), Name = "FIRFunctions")]
	interface CloudFunctions
	{
		// + (FIRFunctions *)functions;
		[Static]
		[Export("functions")]
		CloudFunctions DefaultInstance { get; }

		// + (FIRFunctions *)functionsForApp:(FIRApp *)app;
		[Static]
		[Export("functionsForApp:")]
		CloudFunctions From(App app);

		// + (instancetype)functionsForApp:(FIRApp *)app customDomain:(NSString *)customDomain
		[Static]
		[Export("functionsForApp:customDomain:")]
		CloudFunctions FromCustomDomain(App app, string customDomain);
				
		//+ (FIRFunctions *)functionsForCustomDomain:(NSString*) customDomain
		[Static]
		[Export ("functionsForCustomDomain:")]
		CloudFunctions FromCustomDomain (string customDomain);

		//+ (FIRFunctions *)functionsForApp:(FIRApp *)app region:(NSString*) region
		[Static]
		[Export("functionsForApp:region:")]
		CloudFunctions FromRegion(App app, string region);

		//+ (FIRFunctions *) functionsForRegion:(NSString*) region;
		[Static]
		[Export ("functionsForRegion:")]
		CloudFunctions FromRegion (string region);

		//- (FIRHTTPSCallable *)HTTPSCallableWithName:(NSString *)name;
		[Export("HTTPSCallableWithName:")]
		HttpsCallable HttpsCallable(string name);

		//- (FIRHTTPSCallable *)HTTPSCallableWithName:(NSString *)name options:(FIRHTTPSCallableOptions *)options;
		[Export("HTTPSCallableWithName:options:")]
		HttpsCallable HttpsCallable(string name, HttpsCallableOptions options);

		//- (FIRHTTPSCallable *)HTTPSCallableWithURL:(NSURL *)url;
		[Export("HTTPSCallableWithURL:")]
		HttpsCallable HttpsCallable(NSUrl url);

		//- (FIRHTTPSCallable *)HTTPSCallableWithURL:(NSURL *)url options:(FIRHTTPSCallableOptions *)options;
		[Export("HTTPSCallableWithURL:options:")]
		HttpsCallable HttpsCallable(NSUrl url, HttpsCallableOptions options);

		//- (void)useEmulatorWithHost:(NSString *)host port:(NSInteger) port;
		[Export ("useEmulatorWithHost:port:")]
		void UseEmulatorWithHost (string host, nint port);
	}

	// void (^)(FIRHTTPSCallableResult *_Nullable result, NSError *_Nullable error);
	delegate void HttpsCallableResultHandler([NullAllowed] HttpsCallableResult result, [NullAllowed] NSError error);

	[DisableDefaultCtor]
	[BaseType(typeof(NSObject), Name = "FIRHTTPSCallableOptions")]
	interface HttpsCallableOptions
	{
		//- (instancetype)initWithRequireLimitedUseAppCheckTokens:(BOOL)requireLimitedUseAppCheckTokens;
		[DesignatedInitializer]
		[Export("initWithRequireLimitedUseAppCheckTokens:")]
		NativeHandle Constructor(bool requireLimitedUseAppCheckTokens);

		//@property(nonatomic, readonly) BOOL requireLimitedUseAppCheckTokens;
		[Export("requireLimitedUseAppCheckTokens")]
		bool RequireLimitedUseAppCheckTokens { get; }
	}

	[DisableDefaultCtor]
	[BaseType(typeof(NSObject), Name = "FIRHTTPSCallable")]
	interface HttpsCallable
	{
		//- (void)callWithCompletion: (void (^)(FIRHTTPSCallableResult *_Nullable result, NSError *_Nullable error))completion;
		[Export("callWithCompletion:")]
		[Async]
		void Call(HttpsCallableResultHandler completion);

		//- (void)callWithObject:(nullable id)data completion:(void (^)(FIRHTTPSCallableResult* _Nullable result, NSError *_Nullable error))completion
		[Export("callWithObject:completion:")]
		[Async]
		void Call([NullAllowed] NSObject data, HttpsCallableResultHandler completion);

		//@property(nonatomic, assign) NSTimeInterval timeoutInterval;
		[Export("timeoutInterval")]
		double TimeoutInterval { get; set; }
	}

	[DisableDefaultCtor]
	[BaseType(typeof(NSObject), Name = "FIRHTTPSCallableResult")]
	interface HttpsCallableResult
	{
		//@property(nonatomic, strong, readonly) id data;
		[Export("data")]
		NSObject Data { get; }
	}
}
