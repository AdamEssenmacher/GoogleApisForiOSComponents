using System;
using System.Collections.Generic;

using Foundation;
using ObjCRuntime;

namespace Firebase.Analytics
{
	// @interface FIRAnalytics : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRAnalytics")]
	interface Analytics
	{
		// +(void)logEventWithName:(NSString * _Nonnull)name parameters:(NSDictionary<NSString *,NSObject *> * _Nullable)parameters;
		[Static]
		[Export ("logEventWithName:parameters:")]
		void LogEvent (string name, [NullAllowed] NSDictionary<NSString, NSObject> nsParameters);

		[Static]
		[Wrap ("LogEvent (name, parameters == null ? null : parameters.Keys.Count == 0 ? new NSDictionary<NSString, NSObject> () : NSDictionary<NSString, NSObject>.FromObjectsAndKeys (System.Linq.Enumerable.ToArray (parameters.Values), System.Linq.Enumerable.ToArray (parameters.Keys), parameters.Keys.Count))")]
		void LogEvent (string name, [NullAllowed] Dictionary<object, object> parameters);

		// +(void)setUserPropertyString:(NSString * _Nullable)value forName:(NSString * _Nonnull)name;
		[Static]
		[Export ("setUserPropertyString:forName:")]
		void SetUserProperty ([NullAllowed] string value, string name);

		// +(void)setUserID:(NSString * _Nullable)userID;
		[Static]
		[Export ("setUserID:")]
		void SetUserId ([NullAllowed] string userId);

		// +(void)setAnalyticsCollectionEnabled:(BOOL)analyticsCollectionEnabled;
		[Static]
		[Export ("setAnalyticsCollectionEnabled:")]
		void SetAnalyticsCollectionEnabled (bool analyticsCollectionEnabled);

		// +(void)setSessionTimeoutInterval:(NSTimeInterval)sessionTimeoutInterval;
		[Static]
		[Export ("setSessionTimeoutInterval:")]
		void SetSessionTimeoutInterval (double sessionTimeoutInterval);

		// + (void)sessionIDWithCompletion:(void (^)(int64_t sessionID, NSError *_Nullable error))completion;
		[Static]
		[Export ("sessionIDWithCompletion:")]
		void SessionIdWithCompletion (Action<long, NSError> completion);

		// +(void)initiateOnDeviceConversionMeasurementWithEmailAddress:(NSString * _Nonnull)emailAddress;
		[Static]
		[Export ("initiateOnDeviceConversionMeasurementWithEmailAddress:")]
		void InitiateOnDeviceConversionMeasurementWithEmailAddress (string emailAddress);

		// +(void)initiateOnDeviceConversionMeasurementWithPhoneNumber:(NSString * _Nonnull)phoneNumber;
		[Static]
		[Export ("initiateOnDeviceConversionMeasurementWithPhoneNumber:")]
		void InitiateOnDeviceConversionMeasurementWithPhoneNumber (string phoneNumber);

		// +(void)initiateOnDeviceConversionMeasurementWithHashedEmailAddress:(NSData * _Nonnull)hashedEmailAddress;
		[Static]
		[Export ("initiateOnDeviceConversionMeasurementWithHashedEmailAddress:")]
		void InitiateOnDeviceConversionMeasurementWithHashedEmailAddress (NSData hashedEmailAddress);

		// +(void)initiateOnDeviceConversionMeasurementWithHashedPhoneNumber:(NSData * _Nonnull)hashedPhoneNumber;
		[Static]
		[Export ("initiateOnDeviceConversionMeasurementWithHashedPhoneNumber:")]
		void InitiateOnDeviceConversionMeasurementWithHashedPhoneNumber (NSData hashedPhoneNumber);

		// + (nullable NSString *)appInstanceID;
		[Static]
		[NullAllowed]
		[Export ("appInstanceID")]
		string AppInstanceId { get; }

		// + (void)resetAnalyticsData;
		[Static]
		[Export ("resetAnalyticsData")]
		void ResetAnalyticsData ();

		// +(void)setDefaultEventParameters:(NSDictionary<NSString *,id> * _Nullable)parameters;
		[Static]
		[Export ("setDefaultEventParameters:")]
		void SetDefaultEventParameters ([NullAllowed] NSDictionary<NSString, NSObject> nsParameters);

		[Static]
		[Wrap ("SetDefaultEventParameters (parameters == null ? null : parameters.Keys.Count == 0 ? new NSDictionary<NSString, NSObject> () : NSDictionary<NSString, NSObject>.FromObjectsAndKeys (System.Linq.Enumerable.ToArray (parameters.Values), System.Linq.Enumerable.ToArray (parameters.Keys), parameters.Keys.Count))")]
		void SetDefaultEventParameters ([NullAllowed] Dictionary<object, object> parameters);

		///
		/// This method comes from a category (FIRAnalytics+AppDelegate.h)
		///

		// +(void)handleEventsForBackgroundURLSession:(NSString *)identifier completionHandler:(void (^)(void))completionHandler;
		[Static]
		[Export ("handleEventsForBackgroundURLSession:completionHandler:")]
		void HandleEventsForBackgroundUrlSession (string identifier, [NullAllowed] Action completionHandler);

		// +(void)handleOpenURL:(NSURL *)url;
		[Static]
		[Export ("handleOpenURL:")]
		void HandleOpenUrl (NSUrl url);

		// +(void)handleUserActivity:(id)userActivity;
		[Static]
		[Export ("handleUserActivity:")]
		void HandleUserActivity (NSObject userActivity);

		///
		/// This method comes from a category (FIRAnalytics+Constent.h)
		///

		// + (void)setConsent:(NSDictionary<FIRConsentType, FIRConsentStatus> *)consentSettings;
		[Static]
		[Internal]
		[Export ("setConsent:")]
		void _SetConsent (NSDictionary<NSString, NSString> consentSettings);
	}
}
