﻿using System;
using System.Collections.Generic;

using Foundation;
using ObjCRuntime;
using UserNotifications;

namespace Firebase.CloudMessaging
{
	// typedef void(^FIRMessagingFCMTokenFetchCompletion)(NSString * _Nullable FCMToken, NSError* _Nullable error) FIR_SWIFT_NAME(MessagingFCMTokenFetchCompletion);
	delegate void MessagingFcmTokenFetchCompletionHandler ([NullAllowed] string fcmToken, [NullAllowed] NSError error);

	// typedef void(^FIRMessagingDeleteFCMTokenCompletion)(NSError * _Nullable error) FIR_SWIFT_NAME(MessagingDeleteFCMTokenCompletion);
	delegate void MessagingDeleteFcmTokenCompletionHandler ([NullAllowed] NSError error);

	// typedef void (^FIRMessagingTopicOperationCompletion)(NSError *_Nullable error);
	delegate void MessagingTopicOperationCompletionHandler ([NullAllowed] NSError error);

	delegate void DeleteDataCompletionHandler ([NullAllowed] NSError error);

	// @interface FIRMessagingMessageInfo : NSObject
	[BaseType (typeof (NSObject), Name = "FIRMessagingMessageInfo")]
	interface MessageInfo
	{
		// @property (readonly, assign, nonatomic) FIRMessagingMessageStatus status;
		[Export ("status", ArgumentSemantic.Assign)]
		MessageStatus Status { get; }
	}

	interface IMessagingDelegate
	{
	}

	// @protocol FIRMessagingDelegate <NSObject>
	[Model]
	[Protocol]
	[BaseType (typeof (NSObject), Name = "FIRMessagingDelegate")]
	interface MessagingDelegate
	{
		// @optional -(void)messaging:(FIRMessaging * _Nonnull)messaging didReceiveRegistrationToken:(NSString * _Nonnull)fcmToken;
		[Export ("messaging:didReceiveRegistrationToken:")]
		void DidReceiveRegistrationToken (Messaging messaging, string fcmToken);
	}

	// @interface FIRMessaging : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "FIRMessaging")]
	interface Messaging
	{
		// @property(nonatomic, weak, nullable) id<FIRMessagingDelegate> delegate;
		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		IMessagingDelegate Delegate { get; set; }

		// +(instancetype _Nonnull)messaging;
		[Static]
		[Export ("messaging")]
		Messaging SharedInstance { get; }

		// +(FIRMessagingExtensionHelper * _Nonnull)extensionHelper __attribute__((swift_name("serviceExtension()"))) __attribute__((availability(ios, introduced=10.0)));
		[Introduced (PlatformName.iOS, 10, 0, 0)]
		[Static]
		[Export ("extensionHelper")]
		MessagingExtensionHelper ExtensionHelper { get; }

		// @property(nonatomic, copy, nullable) NSData *APNSToken FIR_SWIFT_NAME(apnsToken);
		[NullAllowed]
		[Export ("APNSToken", ArgumentSemantic.Copy)]
		NSData ApnsToken { get; set; }

		// - (void)setAPNSToken:(nonnull NSData *)apnsToken type:(FIRMessagingAPNSTokenType)type;
		[Export ("setAPNSToken:type:")]
		void SetApnsToken (NSData apnsToken, ApnsTokenType type);

		// @property(nonatomic, assign, getter=isAutoInitEnabled) BOOL autoInitEnabled;
		[Export ("autoInitEnabled")]
		bool AutoInitEnabled { [Bind ("isAutoInitEnabled")] get; set; }

		// @property(nonatomic, readonly, nullable) NSString *FCMToken FIR_SWIFT_NAME(fcmToken);
		[NullAllowed]
		[Export ("FCMToken")]
		string FcmToken { get; }

		// -(void)tokenWithCompletion:(void (^ _Nonnull)(NSString * _Nullable, NSError * _Nullable))completion;
		[Export ("tokenWithCompletion:")]
		[Async]
		void FetchToken (MessagingFcmTokenFetchCompletionHandler completion);

		// -(void)deleteTokenWithCompletion:(void (^ _Nonnull)(NSError * _Nullable))completion;
		[Export ("deleteTokenWithCompletion:")]
		[Async]
		void DeleteToken (MessagingDeleteFcmTokenCompletionHandler completion);

		// - (void)retrieveFCMTokenForSenderID:(nonnull NSString *)senderID completion:(nonnull FIRMessagingFCMTokenFetchCompletion) completion FIR_SWIFT_NAME(retrieveFCMToken(forSenderID:completion:));
		[Async]
		[Export ("retrieveFCMTokenForSenderID:completion:")]
		void RetrieveFcmToken (string senderId, MessagingFcmTokenFetchCompletionHandler completion);

		// - (void)deleteFCMTokenForSenderID:(nonnull NSString *)senderID completion:(nonnull FIRMessagingDeleteFCMTokenCompletion) completion FIR_SWIFT_NAME(deleteFCMToken(forSenderID:completion:));
		[Async]
		[Export ("deleteFCMTokenForSenderID:completion:")]
		void DeleteFcmToken (string senderId, MessagingDeleteFcmTokenCompletionHandler completion);

		// -(void)subscribeToTopic:(NSString * _Nonnull)topic;
		[Export ("subscribeToTopic:")]
		void Subscribe (string topic);

		// -(void)subscribeToTopic:(NSString * _Nonnull)topic completion:(nullable FIRMessagingTopicOperationCompletion)completion;
		[Async]
		[Export ("subscribeToTopic:completion:")]
		void Subscribe (string topic, MessagingTopicOperationCompletionHandler completion);

		// -(void)unsubscribeFromTopic:(NSString * _Nonnull)topic;
		[Export ("unsubscribeFromTopic:")]
		void Unsubscribe (string topic);

		//-(void)unsubscribeFromTopic:(NSString * _Nonnull)topic completion:(nullable FIRMessagingTopicOperationCompletion)completion;
		[Async]
		[Export ("unsubscribeFromTopic:completion:")]
		void Unsubscribe (string topic, MessagingTopicOperationCompletionHandler completion);

		// -(FIRMessagingMessageInfo * _Nonnull)appDidReceiveMessage:(NSDictionary * _Nonnull)message;
		[Export ("appDidReceiveMessage:")]
		MessageInfo AppDidReceiveMessage (NSDictionary message);

		// - (void)deleteDataWithCompletion:(void (^)(NSError *__nullable error))completion;
		[Export ("deleteDataWithCompletion:")]
		void DeleteData (DeleteDataCompletionHandler completion);
	}

	// @interface FIRMessagingExtensionHelper : NSObject
	[Introduced (PlatformName.iOS, 10, 0, 0)]
	[BaseType (typeof (NSObject), Name = "FIRMessagingExtensionHelper")]
	interface MessagingExtensionHelper {
		// -(void)populateNotificationContent:(UNMutableNotificationContent * _Nonnull)content withContentHandler:(void (^ _Nonnull)(UNNotificationContent * _Nonnull))contentHandler;
		[Export ("populateNotificationContent:withContentHandler:")]
		void PopulateNotificationContent (UNMutableNotificationContent content, Action<UNNotificationContent> contentHandler);

		// - (void)exportDeliveryMetricsToBigQueryWithMessageInfo:(NSDictionary *)info;
		[Export ("exportDeliveryMetricsToBigQueryWithMessageInfo:")]
		void ExportDeliveryMetricsToBigQueryWithMessageInfo (NSDictionary info);
	}
}
