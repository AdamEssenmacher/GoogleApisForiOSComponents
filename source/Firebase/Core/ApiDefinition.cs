﻿using System;

using UIKit;
using Foundation;
using ObjCRuntime;
using CoreGraphics;

namespace Firebase.Core
{
	// typedef void (^FIRAppVoidBoolCallback)(BOOL);
	delegate void AppVoidBoolHandler (bool success);

	// @interface FIRApp : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRApp")]
	interface App : INativeObject
	{
		// +(void)configure;
		[Static]
		[Export ("configure")]
		void Configure ();

		// +(void)configureWithOptions:(FIROptions * _Nonnull)options;
		[Static]
		[Export ("configureWithOptions:")]
		void Configure (Options options);

		// +(void)configureWithName:(NSString * _Nonnull)name options:(FIROptions * _Nonnull)options;
		[Static]
		[Export ("configureWithName:options:")]
		void Configure (string name, Options options);

		// +(FIRApp * _Nullable)defaultApp;
		[Static]
		[Export ("defaultApp")]
		App DefaultInstance { get; }

		// +(FIRApp * _Nullable)appNamed:(NSString * _Nonnull)name;
		[Static]
		[return: NullAllowed]
		[Export ("appNamed:")]
		App From (string name);

		// +(NSDictionary * _Nullable)allApps;
		[Static]
		[return: NullAllowed]
		[Export ("allApps")]
		NSDictionary<NSString, App> GetAll ();

		// -(void)deleteApp:(FIRAppVoidBoolCallback _Nonnull)completion;
		[Export ("deleteApp:")]
		void Delete (AppVoidBoolHandler completion);

		// @property (readonly, copy, nonatomic) NSString * _Nonnull name;
		[Export ("name")]
		string Name { get; }

		// @property (readonly, nonatomic) FIROptions * _Nonnull options;
		[Export ("options", ArgumentSemantic.Copy)]
		Options Options { get; }

		// @property(nonatomic, readwrite, getter=isDataCollectionDefaultEnabled) BOOL dataCollectionDefaultEnabled;
		[Export ("dataCollectionDefaultEnabled", ArgumentSemantic.Assign)]
		bool DataCollectionDefaultEnabled { [Bind ("isDataCollectionDefaultEnabled")] get; set; }
	}

	// @interface FIRConfiguration : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRConfiguration")]
	interface Configuration
	{
		// +(FIRConfiguration *)sharedInstance;
		[Static]
		[Export ("sharedInstance")]
		Configuration SharedInstance { get; }

		// - (void)setLoggerLevel:(FIRLoggerLevel)loggerLevel;
		[Export ("setLoggerLevel:")]
		void SetLoggerLevel (LoggerLevel loggerLevel);
	}

	// @interface FIROptions : NSObject <NSCopying>
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIROptions")]
	interface Options : INSCopying
	{
		// +(FIROptions *)defaultOptions;
		[Static]
		[NullAllowed]
		[Export ("defaultOptions")]
		Options DefaultInstance { get; }

		// @property (readonly, copy, nonatomic) NSString * APIKey;
		[NullAllowed]
		[Export ("APIKey")]
		string ApiKey { get; set; }

		// @property(nonatomic, copy) NSString *bundleID;
		[NullAllowed]
		[Export ("bundleID")]
		string BundleId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * clientID;
		[NullAllowed]
		[Export ("clientID")]
		string ClientId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * trackingID;
		[NullAllowed]
		[Export ("trackingID")]
		string TrackingId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * GCMSenderID;
		[Export ("GCMSenderID")]
		string GcmSenderId { get; set; }

		// @property(nonatomic, readonly, copy) NSString *projectID;
		[NullAllowed]
		[Export ("projectID")]
		string ProjectId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * androidClientID;
		[NullAllowed]
		[Export ("androidClientID")]
		string AndroidClientId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * googleAppID;
		[Export ("googleAppID")]
		string GoogleAppId { get; set; }

		// @property (readonly, copy, nonatomic) NSString * databaseURL;
		[NullAllowed]
		[Export ("databaseURL")]
		string DatabaseUrl { get; set; }

		// @property (readwrite, copy, nonatomic) NSString * deepLinkURLScheme;
		[NullAllowed]
		[Export ("deepLinkURLScheme")]
		string DeepLinkUrlScheme { get; set; }

		// @property (readonly, copy, nonatomic) NSString * storageBucket;
		[NullAllowed]
		[Export ("storageBucket")]
		string StorageBucket { get; set; }

		// @property(nonatomic, copy, nullable) NSString *appGroupID;
		[NullAllowed]
		[Export ("appGroupID")]
		string AppGroupId { get; set; }

		// - (instancetype)initWithContentsOfFile:(NSString *)plistPath;
		[Export ("initWithContentsOfFile:")]
		NativeHandle Constructor (string plistPath);

		// - (instancetype)initWithGoogleAppID:(NSString *)googleAppID GCMSenderID:(NSString*) GCMSenderID;
		[Export ("initWithGoogleAppID:GCMSenderID:")]
		NativeHandle Constructor (string googleAppId, string gcmSenderId);
	}

	// @interface FIRTimestamp : NSObject <NSCopying>
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRTimestamp")]
	interface Timestamp : INSCopying {
		// -(instancetype _Nonnull)initWithSeconds:(int64_t)seconds nanoseconds:(int32_t)nanoseconds __attribute__((objc_designated_initializer));
		[Export ("initWithSeconds:nanoseconds:")]
		[DesignatedInitializer]
		NativeHandle Constructor (long seconds, int nanoseconds);

		// +(instancetype _Nonnull)timestampWithSeconds:(int64_t)seconds nanoseconds:(int32_t)nanoseconds;
		[Static]
		[Export ("timestampWithSeconds:nanoseconds:")]
		Timestamp Create (long seconds, int nanoseconds);

		// +(instancetype _Nonnull)timestampWithDate:(NSDate * _Nonnull)date;
		[Static]
		[Export ("timestampWithDate:")]
		Timestamp Create (NSDate date);

		// +(instancetype _Nonnull)timestamp;
		[Static]
		[Export ("timestamp")]
		Timestamp Create ();

		// -(NSDate * _Nonnull)dateValue;
		[Export ("dateValue")]
		NSDate DateValue { get; }

		// -(NSComparisonResult)compare:(FIRTimestamp * _Nonnull)other;
		[Export ("compare:")]
		NSComparisonResult Compare (Timestamp other);

		// @property (readonly, assign, nonatomic) int64_t seconds;
		[Export ("seconds")]
		long Seconds { get; }

		// @property (readonly, assign, nonatomic) int32_t nanoseconds;
		[Export ("nanoseconds")]
		int Nanoseconds { get; }
	}
}
