// Firebase artifacts available to be built. These artifacts generate NuGets.
Artifact FIREBASE_AB_TESTING_ARTIFACT              = new Artifact ("Firebase.ABTesting",              "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "ABTesting");
Artifact FIREBASE_ANALYTICS_ARTIFACT               = new Artifact ("Firebase.Analytics",              "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Analytics");
Artifact FIREBASE_AUTH_ARTIFACT                    = new Artifact ("Firebase.Auth",                   "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Auth");
Artifact FIREBASE_CLOUD_FIRESTORE_ARTIFACT         = new Artifact ("Firebase.CloudFirestore",         "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "CloudFirestore");
Artifact FIREBASE_CLOUD_FUNCTIONS_ARTIFACT         = new Artifact ("Firebase.CloudFunctions",         "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "CloudFunctions");
Artifact FIREBASE_CLOUD_MESSAGING_ARTIFACT         = new Artifact ("Firebase.CloudMessaging",         "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "CloudMessaging");
Artifact FIREBASE_CORE_ARTIFACT                    = new Artifact ("Firebase.Core",                   "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Core");
Artifact FIREBASE_CRASHLYTICS_ARTIFACT             = new Artifact ("Firebase.Crashlytics",            "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Crashlytics");
Artifact FIREBASE_DATABASE_ARTIFACT                = new Artifact ("Firebase.Database",               "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Database");
Artifact FIREBASE_DYNAMIC_LINKS_ARTIFACT           = new Artifact ("Firebase.DynamicLinks",           "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "DynamicLinks");
//Artifact FIREBASE_IN_APP_MESSAGING_ARTIFACT      = new Artifact ("Firebase.InAppMessaging",         "8.10.0.3",  "11.0", ComponentGroup.Firebase, csprojName: "InAppMessaging");
Artifact FIREBASE_INSTALLATIONS_ARTIFACT           = new Artifact ("Firebase.Installations",          "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Installations");
//Artifact FIREBASE_PERFORMANCE_MONITORING_ARTIFACT=new Artifact ("Firebase.PerformanceMonitoring",   "8.18.0.3",  "11.0", ComponentGroup.Firebase, csprojName: "PerformanceMonitoring");
Artifact FIREBASE_REMOTE_CONFIG_ARTIFACT           = new Artifact ("Firebase.RemoteConfig",           "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "RemoteConfig");
Artifact FIREBASE_STORAGE_ARTIFACT                 = new Artifact ("Firebase.Storage",                "11.10.0.0",  "13.0", ComponentGroup.Firebase, csprojName: "Storage");
//Artifact FIREBASE_APP_DISTRIBUTION_ARTIFACT      = new Artifact ("Firebase.AppDistribution",        "8.10.0.1",  "10.0", ComponentGroup.Firebase, csprojName: "AppDistribution");
//Artifact FIREBASE_APP_CHECK_ARTIFACT             = new Artifact ("Firebase.AppCheck",               "8.10.0.1",  "11.0", ComponentGroup.Firebase, csprojName: "AppCheck");

// Google artifacts available to be built. These artifacts generate NuGets.
Artifact GOOGLE_ANALYTICS_ARTIFACT                 = new Artifact ("Google.Analytics",                "3.20.0.2",  "11.0", ComponentGroup.Google, csprojName: "Analytics");
Artifact GOOGLE_CAST_ARTIFACT                      = new Artifact ("Google.Cast",                     "4.7.0.1",   "12.0", ComponentGroup.Google, csprojName: "Cast");
Artifact GOOGLE_MAPS_ARTIFACT                      = new Artifact ("Google.Maps",                     "9.2.0.5",   "15.0", ComponentGroup.Google, csprojName: "Maps");
Artifact GOOGLE_MOBILE_ADS_ARTIFACT                = new Artifact ("Google.MobileAds",                "8.13.0.3",  "11.0", ComponentGroup.Google, csprojName: "MobileAds");
Artifact GOOGLE_UMP_ARTIFACT                       = new Artifact ("Google.UserMessagingPlatform",    "1.1.0.1",   "11.0", ComponentGroup.Google, csprojName: "UserMessagingPlatform");
Artifact GOOGLE_PLACES_ARTIFACT                    = new Artifact ("Google.Places",                   "6.0.0.3",   "12.0", ComponentGroup.Google, csprojName: "Places");
Artifact GOOGLE_SIGN_IN_ARTIFACT                   = new Artifact ("Google.SignIn",                   "8.0.0.0",   "12.0", ComponentGroup.Google, csprojName: "SignIn");
Artifact GOOGLE_TAG_MANAGER_ARTIFACT               = new Artifact ("Google.TagManager",               "7.4.0.2",   "11.0", ComponentGroup.Google, csprojName: "TagManager");

Artifact GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT    = new Artifact ("Google.AppMeasurement",           "11.10.0",   "12.0", ComponentGroup.Google, csprojName: "GoogleAppMeasurement");
Artifact GOOGLE_PROMISES_OBJC_ARTIFACT             = new Artifact ("Google.PromisesObjC",             "2.4.0",     "10.0", ComponentGroup.Google, csprojName: "PromisesObjC");
Artifact GOOGLE_GTM_SESSION_FETCHER_ARTIFACT       = new Artifact ("Google.GTMSessionFetcher",        "4.3.0",     "13.0", ComponentGroup.Google, csprojName: "GTMSessionFetcher");
Artifact GOOGLE_NANOPB_ARTIFACT                    = new Artifact ("Google.Nanopb",                   "3.30910.0", "12.0", ComponentGroup.Google, csprojName: "Nanopb");
Artifact GOOGLE_GOOGLE_UTILITIES_ARTIFACT          = new Artifact ("Google.GoogleUtilities",          "8.0.2",     "12.0", ComponentGroup.Google, csprojName: "GoogleUtilities");
Artifact GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT     = new Artifact ("Google.GoogleDataTransport",      "10.1.0",    "12.0", ComponentGroup.Google, csprojName: "GoogleDataTransport");

// MLKit artifacts available to be built. These artifacts generate NuGets.
Artifact MLKIT_CORE_ARTIFACT                     = new Artifact ("MLKit.Core",                        "12.0.0.0",  "16.0", ComponentGroup.MLKit, csprojName: "Core");
Artifact MLKIT_TEXT_RECOGNITION                  = new Artifact ("MLKit.TextRecognition",             "1.0.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognition");
Artifact MLKIT_VISION                            = new Artifact ("MLKit.Vision",                      "3.0.0",     "10.0", ComponentGroup.MLKit, csprojName: "Vision");
Artifact MLKIT_TEXT_RECOGNITION_LATIN            = new Artifact ("MLKit.TextRecognition.Latin",       "1.4.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognitionLatin");
Artifact MLKIT_TEXT_RECOGNITION_CHINESE          = new Artifact ("MLKit.TextRecognition.Chinese",     "1.0.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognitionChinese");
Artifact MLKIT_TEXT_RECOGNITION_DEVANAGARI       = new Artifact ("MLKit.TextRecognition.Devanagari",  "1.0.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognitionDevanagari");
Artifact MLKIT_TEXT_RECOGNITION_JAPANESE         = new Artifact ("MLKit.TextRecognition.Japanese",    "1.0.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognitionJapanese");
Artifact MLKIT_TEXT_RECOGNITION_KOREAN           = new Artifact ("MLKit.TextRecognition.Korean",      "1.0.0.3",   "10.0", ComponentGroup.MLKit, csprojName: "TextRecognitionKorean");
Artifact MLKIT_FACE_DETECTION                    = new Artifact ("MLKit.FaceDetection",               "1.5.0",     "10.0", ComponentGroup.MLKit, csprojName: "FaceDetection");
Artifact MLKIT_BARCODE_SCANNING                  = new Artifact ("MLKit.BarcodeScanning",             "6.0.0.0",   "16.0", ComponentGroup.MLKit, csprojName: "BarcodeScanning");
Artifact MLKIT_DIGITAL_INK_RECOGNITION           = new Artifact ("MLKit.DigitalInkRecognition",       "1.5.0",     "10.0", ComponentGroup.MLKit, csprojName: "DigitalInkRecognition");
Artifact MLKIT_IMAGE_LABELING                    = new Artifact ("MLKit.ImageLabeling",               "1.5.0",     "10.0", ComponentGroup.MLKit, csprojName: "ImageLabeling");
Artifact MLKIT_OBJECT_DETECTION                  = new Artifact ("MLKit.ObjectDetection",             "1.5.0",     "10.0", ComponentGroup.MLKit, csprojName: "ObjectDetection");

var ARTIFACTS = new Dictionary<string, Artifact> {
	{ "Firebase.ABTesting",              FIREBASE_AB_TESTING_ARTIFACT },
	{ "Firebase.Analytics",              FIREBASE_ANALYTICS_ARTIFACT },
	{ "Firebase.Auth",                   FIREBASE_AUTH_ARTIFACT },
	{ "Firebase.CloudFirestore",         FIREBASE_CLOUD_FIRESTORE_ARTIFACT },
	{ "Firebase.CloudFunctions",         FIREBASE_CLOUD_FUNCTIONS_ARTIFACT },
	{ "Firebase.CloudMessaging",         FIREBASE_CLOUD_MESSAGING_ARTIFACT },
	{ "Firebase.Core",                   FIREBASE_CORE_ARTIFACT },
	{ "Firebase.Crashlytics",            FIREBASE_CRASHLYTICS_ARTIFACT },
	{ "Firebase.Database",               FIREBASE_DATABASE_ARTIFACT },
	{ "Firebase.DynamicLinks",           FIREBASE_DYNAMIC_LINKS_ARTIFACT },
	//{ "Firebase.InAppMessaging",         FIREBASE_IN_APP_MESSAGING_ARTIFACT },
	{ "Firebase.Installations",          FIREBASE_INSTALLATIONS_ARTIFACT },
	//{ "Firebase.PerformanceMonitoring",  FIREBASE_PERFORMANCE_MONITORING_ARTIFACT },
	{ "Firebase.RemoteConfig",           FIREBASE_REMOTE_CONFIG_ARTIFACT },
	{ "Firebase.Storage",                FIREBASE_STORAGE_ARTIFACT },
	// { "Firebase.AppDistribution",        FIREBASE_APP_DISTRIBUTION_ARTIFACT },
	// { "Firebase.AppCheck",               FIREBASE_APP_CHECK_ARTIFACT },

    { "Google.GoogleAppMeasurement",  GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT },
	{ "Google.Analytics",             GOOGLE_ANALYTICS_ARTIFACT },
	{ "Google.Cast",                  GOOGLE_CAST_ARTIFACT },	
	{ "Google.Maps",                  GOOGLE_MAPS_ARTIFACT },
	{ "Google.MobileAds",             GOOGLE_MOBILE_ADS_ARTIFACT },
	{ "Google.UserMessagingPlatform", GOOGLE_UMP_ARTIFACT },
	{ "Google.Places",                GOOGLE_PLACES_ARTIFACT },
	{ "Google.SignIn",                GOOGLE_SIGN_IN_ARTIFACT },
	{ "Google.TagManager",            GOOGLE_TAG_MANAGER_ARTIFACT },
	{ "Google.GTMSessionFetcher",     GOOGLE_GTM_SESSION_FETCHER_ARTIFACT },
	{ "Google.PromisesObjC",          GOOGLE_PROMISES_OBJC_ARTIFACT },
	{ "Google.Nanopb",                GOOGLE_NANOPB_ARTIFACT },
	{ "Google.GoogleUtilities",       GOOGLE_GOOGLE_UTILITIES_ARTIFACT },
	{ "Google.GoogleDataTransport",   GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT },

	{ "MLKit.Core",                       MLKIT_CORE_ARTIFACT },
	// { "MLKit.TextRecognition",            MLKIT_TEXT_RECOGNITION },
	// { "MLKit.Vision",                     MLKIT_VISION },
	// { "MLKit.TextRecognition.Latin",      MLKIT_TEXT_RECOGNITION_LATIN },
	// { "MLKit.TextRecognition.Chinese",    MLKIT_TEXT_RECOGNITION_CHINESE },
	// { "MLKit.TextRecognition.Devanagari", MLKIT_TEXT_RECOGNITION_DEVANAGARI },
	// { "MLKit.TextRecognition.Japanese",   MLKIT_TEXT_RECOGNITION_JAPANESE },
	// { "MLKit.TextRecognition.Korean",     MLKIT_TEXT_RECOGNITION_KOREAN },
	// { "MLKit.FaceDetection",              MLKIT_FACE_DETECTION },
	{ "MLKit.BarcodeScanning",            MLKIT_BARCODE_SCANNING },
	// { "MLKit.ImageLabeling",              MLKIT_IMAGE_LABELING },
	// { "MLKit.ObjectDetection",            MLKIT_OBJECT_DETECTION },
	// { "MLKit.DigitalInkRecognition",      MLKIT_DIGITAL_INK_RECOGNITION },
};

void SetArtifactsDependencies ()
{
	FIREBASE_AB_TESTING_ARTIFACT.Dependencies              = new [] { FIREBASE_CORE_ARTIFACT };
	FIREBASE_ANALYTICS_ARTIFACT.Dependencies               = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT, GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT };
	FIREBASE_AUTH_ARTIFACT.Dependencies                    = new [] { FIREBASE_CORE_ARTIFACT, GOOGLE_GTM_SESSION_FETCHER_ARTIFACT /* Needed for sample GOOGLE_SIGN_IN_ARTIFACT */ };
	FIREBASE_CLOUD_FIRESTORE_ARTIFACT.Dependencies         = new [] { FIREBASE_CORE_ARTIFACT, /* Needed for sample */ FIREBASE_AUTH_ARTIFACT };
	FIREBASE_CLOUD_FUNCTIONS_ARTIFACT.Dependencies         = new [] { FIREBASE_CORE_ARTIFACT, GOOGLE_GTM_SESSION_FETCHER_ARTIFACT };
	FIREBASE_CLOUD_MESSAGING_ARTIFACT.Dependencies         = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT };
	FIREBASE_CORE_ARTIFACT.Dependencies                    = new [] { GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT, GOOGLE_GOOGLE_UTILITIES_ARTIFACT, GOOGLE_NANOPB_ARTIFACT, GOOGLE_PROMISES_OBJC_ARTIFACT};
	FIREBASE_CRASHLYTICS_ARTIFACT.Dependencies             = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT };
	FIREBASE_DATABASE_ARTIFACT.Dependencies                = new [] { FIREBASE_CORE_ARTIFACT, /* Needed for sample FIREBASE_AUTH_ARTIFACT */ };
	FIREBASE_DYNAMIC_LINKS_ARTIFACT.Dependencies           = new [] { FIREBASE_CORE_ARTIFACT };
	//FIREBASE_IN_APP_MESSAGING_ARTIFACT.Dependencies        = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT, FIREBASE_AB_TESTING_ARTIFACT };
	FIREBASE_INSTALLATIONS_ARTIFACT.Dependencies           = new [] { FIREBASE_CORE_ARTIFACT };
	//FIREBASE_PERFORMANCE_MONITORING_ARTIFACT.Dependencies  = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT, FIREBASE_AB_TESTING_ARTIFACT, FIREBASE_REMOTE_CONFIG_ARTIFACT };
	FIREBASE_REMOTE_CONFIG_ARTIFACT.Dependencies           = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT, FIREBASE_AB_TESTING_ARTIFACT };
	FIREBASE_STORAGE_ARTIFACT.Dependencies                 = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_DATABASE_ARTIFACT, GOOGLE_GTM_SESSION_FETCHER_ARTIFACT /* Needed for sample FIREBASE_AUTH_ARTIFACT */ };
	// FIREBASE_APP_DISTRIBUTION_ARTIFACT.Dependencies        = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT };
	// FIREBASE_APP_CHECK_ARTIFACT.Dependencies               = new [] { FIREBASE_CORE_ARTIFACT };

	GOOGLE_ANALYTICS_ARTIFACT.Dependencies             = null;
	GOOGLE_CAST_ARTIFACT.Dependencies                  = new [] { FIREBASE_CORE_ARTIFACT };
	GOOGLE_MAPS_ARTIFACT.Dependencies                  = null;
	GOOGLE_MOBILE_ADS_ARTIFACT.Dependencies            = new [] { FIREBASE_CORE_ARTIFACT };
	GOOGLE_UMP_ARTIFACT.Dependencies                   = null;
	GOOGLE_PLACES_ARTIFACT.Dependencies                = null;
	GOOGLE_SIGN_IN_ARTIFACT.Dependencies               = new [] { GOOGLE_GTM_SESSION_FETCHER_ARTIFACT, GOOGLE_PROMISES_OBJC_ARTIFACT, GOOGLE_GOOGLE_UTILITIES_ARTIFACT };
	GOOGLE_TAG_MANAGER_ARTIFACT.Dependencies           = new [] { FIREBASE_CORE_ARTIFACT, FIREBASE_INSTALLATIONS_ARTIFACT, FIREBASE_ANALYTICS_ARTIFACT };
	GOOGLE_PROMISES_OBJC_ARTIFACT.Dependencies         = null;
	GOOGLE_GTM_SESSION_FETCHER_ARTIFACT.Dependencies   = null;
	GOOGLE_NANOPB_ARTIFACT.Dependencies                = null;
	GOOGLE_GOOGLE_UTILITIES_ARTIFACT.Dependencies      = null;
	GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT.Dependencies = new [] { GOOGLE_NANOPB_ARTIFACT, GOOGLE_PROMISES_OBJC_ARTIFACT };

	MLKIT_CORE_ARTIFACT.Dependencies                = new [] { FIREBASE_CORE_ARTIFACT };
	MLKIT_TEXT_RECOGNITION.Dependencies             = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT };
	MLKIT_VISION.Dependencies                       = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT };
	MLKIT_TEXT_RECOGNITION_LATIN.Dependencies       = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_TEXT_RECOGNITION };
	MLKIT_TEXT_RECOGNITION_CHINESE.Dependencies     = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_TEXT_RECOGNITION };
	MLKIT_TEXT_RECOGNITION_DEVANAGARI.Dependencies  = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_TEXT_RECOGNITION };
	MLKIT_TEXT_RECOGNITION_JAPANESE.Dependencies    = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_TEXT_RECOGNITION };
	MLKIT_TEXT_RECOGNITION_KOREAN.Dependencies      = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_TEXT_RECOGNITION };
	MLKIT_FACE_DETECTION.Dependencies               = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT };
	MLKIT_BARCODE_SCANNING.Dependencies             = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT };
	MLKIT_DIGITAL_INK_RECOGNITION.Dependencies      = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT };
	MLKIT_IMAGE_LABELING.Dependencies               = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_VISION };
	MLKIT_OBJECT_DETECTION.Dependencies             = new [] { FIREBASE_CORE_ARTIFACT, MLKIT_CORE_ARTIFACT, MLKIT_VISION };
}

void SetArtifactsPodSpecs ()
{
	// Firebase components
	FIREBASE_AB_TESTING_ARTIFACT.PodSpecs = new [] { 
		PodSpec.Create ("FirebaseABTesting", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	FIREBASE_ANALYTICS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseAnalytics", "11.10.0")
	};
	FIREBASE_AUTH_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseAuth",     "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("RecaptchaInterop", "101.0.0", frameworkSource: FrameworkSource.Pods)
	};
	FIREBASE_CLOUD_FIRESTORE_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseFirestore",         "11.10.0",       frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseFirestoreInternal", "11.10.0",       frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("BoringSSL-GRPC",            "0.0.37",       frameworkSource: FrameworkSource.Pods, frameworkName: "openssl_grpc"),
		PodSpec.Create ("gRPC-Core",                 "1.69.0",       frameworkSource: FrameworkSource.Pods, frameworkName: "grpc"),
		PodSpec.Create ("gRPC-C++",                  "1.69.0",       frameworkSource: FrameworkSource.Pods, frameworkName: "grpcpp"),
		PodSpec.Create ("abseil",                    "1.20240722.0", frameworkSource: FrameworkSource.Pods, frameworkName: "absl", subSpecs: new [] { "algorithm", "base", "memory", "meta", "strings", "time", "types" }),
	};
	FIREBASE_CLOUD_FUNCTIONS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseFunctions", "11.10.0", frameworkSource: FrameworkSource.Pods)		
	};
	FIREBASE_CLOUD_MESSAGING_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseMessaging", "11.10.0", frameworkSource: FrameworkSource.Pods)		
	};
	FIREBASE_CORE_ARTIFACT.PodSpecs = new [] {
	    PodSpec.Create ("FirebaseAppCheckInterop",     "11.10.0", frameworkSource: FrameworkSource.Pods),
	    PodSpec.Create ("FirebaseAuthInterop",         "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseCore",                "11.10.0", frameworkSource: FrameworkSource.Pods),		
		PodSpec.Create ("FirebaseCoreExtension",       "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseCoreInternal",        "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseMessagingInterop",    "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseRemoteConfigInterop", "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseSessions",            "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("FirebaseSharedSwift",         "11.10.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("GoogleAppMeasurement",        "11.10.0"),
		PodSpec.Create ("PromisesSwift",               "2.4.0",  frameworkSource: FrameworkSource.Pods, frameworkName: "Promises", targetName: "PromisesSwift"),
		PodSpec.Create ("leveldb-library",             "1.22.6", frameworkSource: FrameworkSource.Pods, frameworkName: "leveldb"),
	};
	FIREBASE_CRASHLYTICS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseCrashlytics", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	FIREBASE_DATABASE_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseDatabase", "11.10.0", frameworkSource: FrameworkSource.Pods)		
	};
	FIREBASE_DYNAMIC_LINKS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseDynamicLinks", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	//FIREBASE_IN_APP_MESSAGING_ARTIFACT.PodSpecs = new [] {
	//	PodSpec.Create ("Firebase", "8.10.0", frameworkSource: FrameworkSource.Pods, frameworkName: "FirebaseInAppMessaging", targetName: "FirebaseInAppMessaging", subSpecs: new [] { "InAppMessaging" })
	//};
	FIREBASE_INSTALLATIONS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseInstallations", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	//FIREBASE_PERFORMANCE_MONITORING_ARTIFACT.PodSpecs = new [] {
	//	PodSpec.Create ("Firebase", "8.10.0", frameworkSource: FrameworkSource.Pods, frameworkName: "FirebasePerformance", targetName: "FirebasePerformance",  subSpecs: new [] { "Performance" })
	//};
	FIREBASE_REMOTE_CONFIG_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseRemoteConfig", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	FIREBASE_STORAGE_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("FirebaseStorage", "11.10.0", frameworkSource: FrameworkSource.Pods)
	};
	// FIREBASE_APP_DISTRIBUTION_ARTIFACT.PodSpecs = new [] {
	// 	PodSpec.Create ("Firebase", "8.10.0", frameworkSource: FrameworkSource.Pods, frameworkName: "FirebaseAppDistribution", targetName: "FirebaseAppDistribution", subSpecs: new [] { "AppDistribution" })		
	// };
	// FIREBASE_APP_CHECK_ARTIFACT.PodSpecs = new [] {
	// 	PodSpec.Create ("Firebase", "8.10.0", frameworkSource: FrameworkSource.Pods, frameworkName: "FirebaseAppCheck", targetName: "FirebaseAppCheck", subSpecs: new [] { "AppCheck" })		
	// };

	// Google components
	GOOGLE_ANALYTICS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GoogleAnalytics", "3.20.0")
	};
	GOOGLE_CAST_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("google-cast-sdk", "4.7.0")
	};
	GOOGLE_MAPS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GoogleMaps", "9.2.0")
	};
	GOOGLE_MOBILE_ADS_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("Google-Mobile-Ads-SDK", "8.13.0")
	};
	GOOGLE_UMP_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GoogleUserMessagingPlatform", "1.1.0")
	};
	GOOGLE_PLACES_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GooglePlaces", "6.0.0")
	};
	GOOGLE_SIGN_IN_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GoogleSignIn", "8.0.0",  frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("AppAuth",      "1.7.6",  frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("AppCheckCore", "11.2.0", frameworkSource: FrameworkSource.Pods),
		PodSpec.Create ("GTMAppAuth",   "4.1.1",  frameworkSource: FrameworkSource.Pods),
	};
	GOOGLE_TAG_MANAGER_ARTIFACT.PodSpecs = new [] {
		PodSpec.Create ("GoogleTagManager", "7.4.0")
	};
    GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT.PodSpecs = new [] {
        PodSpec.Create ("GoogleAppMeasurement", "11.10.0")
    };
	GOOGLE_PROMISES_OBJC_ARTIFACT.PodSpecs = new [] {
	    PodSpec.Create ("PromisesObjC", "2.4.0", frameworkSource: FrameworkSource.Pods, frameworkName: "FBLPromises", targetName: "PromisesObjC"),
    };
	GOOGLE_GTM_SESSION_FETCHER_ARTIFACT.PodSpecs = new [] {
        PodSpec.Create ("GTMSessionFetcher", "4.3.0", frameworkSource: FrameworkSource.Pods, subSpecs: new [] { "Full" }),
    };
    GOOGLE_NANOPB_ARTIFACT.PodSpecs = new [] {
        PodSpec.Create ("nanopb", "3.30910.0", frameworkSource: FrameworkSource.Pods),
    };
    GOOGLE_GOOGLE_UTILITIES_ARTIFACT.PodSpecs = new [] {
        PodSpec.Create ("GoogleUtilities", "8.0.2", frameworkSource: FrameworkSource.Pods, subSpecs: new [] { "AppDelegateSwizzler", "Environment", "Logger", "MethodSwizzler", "Network", "NSData+zlib", "Privacy", "Reachability", "UserDefaults", }),
    };
	GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT.PodSpecs = new [] {
        PodSpec.Create ("GoogleDataTransport", "10.1.0", frameworkSource: FrameworkSource.Pods),
    };

	// MLKit components
	MLKIT_CORE_ARTIFACT.PodSpecs = new [] { 
		PodSpec.Create ("MLKitCommon",                     "12.0.0"),
		PodSpec.Create ("MLKitVision",                     "8.0.0"),
		PodSpec.Create ("MLImage",                         "1.0.0-beta6"),
		//PodSpec.Create ("MLKitImageLabelingCommon",        "7.0.0"),
		//PodSpec.Create ("MLKitObjectDetectionCommon",      "7.0.0"),
		PodSpec.Create ("GoogleToolboxForMac",             "4.2.1", frameworkSource: FrameworkSource.Pods, subSpecs: new [] { "Core", "Defines", "Logger", "NSData+zlib"}),
		//PodSpec.Create ("SSZipArchive",                    "2.4.2",       frameworkSource: FrameworkSource.Pods),
	};
	MLKIT_TEXT_RECOGNITION.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognitionCommon",      "1.0.0")
	};
	MLKIT_VISION.PodSpecs = new [] { 
		PodSpec.Create ("MLKitVision",                     "3.0.0")
	};
	MLKIT_TEXT_RECOGNITION_LATIN.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognition",            "1.4.0")
	};
	MLKIT_TEXT_RECOGNITION_CHINESE.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognitionChinese",     "1.0.0")
	};
	MLKIT_TEXT_RECOGNITION_DEVANAGARI.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognitionDevanagari",  "1.0.0")
	};
	MLKIT_TEXT_RECOGNITION_JAPANESE.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognitionJapanese",    "1.0.0")
	};
	MLKIT_TEXT_RECOGNITION_KOREAN.PodSpecs = new [] { 
		PodSpec.Create ("MLKitTextRecognitionKorean",      "1.0.0")
	};
	MLKIT_FACE_DETECTION.PodSpecs = new [] { 
		PodSpec.Create ("MLKitFaceDetection",              "1.5.0")
	};
	MLKIT_BARCODE_SCANNING.PodSpecs = new [] { 
		PodSpec.Create ("MLKitBarcodeScanning",            "6.0.0")
	};
	MLKIT_DIGITAL_INK_RECOGNITION.PodSpecs = new [] { 
		PodSpec.Create ("MLKitDigitalInkRecognition",      "1.5.0")
	};
	MLKIT_IMAGE_LABELING.PodSpecs = new [] { 
		PodSpec.Create ("MLKitImageLabeling",              "1.5.0")
	};
	MLKIT_OBJECT_DETECTION.PodSpecs = new [] { 
		PodSpec.Create ("MLKitObjectDetection",            "1.5.0")
	};
}

void SetArtifactsExtraPodfileLines ()
{
	var dynamicFrameworkLines = new [] {	
		"=begin",
		"This override the static_framework flag to false,",
		"in order to be able to build dynamic frameworks.",
		"=end",
		"pre_install do |installer|",
		"\tinstaller.pod_targets.each do |pod|",
		"\tputs \"Forcing a static_framework to false for #{pod.name}\"",
		"\t\tif Pod::VERSION >= \"1.7.0\"",
		"\t\t\tif pod.build_as_static?",
		"\t\t\t\tdef pod.build_as_static?; false end",
		"\t\t\t\tdef pod.build_as_static_framework?; false end",
		"\t\t\t\tdef pod.build_as_dynamic?; true end",
		"\t\t\t\tdef pod.build_as_dynamic_framework?; true end",
		"\t\t\tend",
		"\t\telse",
		"\t\t\tdef pod.static_framework?; false end",
		"\t\tend",
		"\tend",
		"end",
	};

	var avoidBundleSigning = new [] {
		"=begin",
		"It seems that there is an issue with bundles and Xcode 14, it asks for your Team ID to sign them when building.",
		"Here's a workaround for this: https://github.com/CocoaPods/CocoaPods/issues/8891#issuecomment-1201465446",
		"=end",
		"post_install do |installer|",
		"\tinstaller.pods_project.targets.each do |target|",
		"\t\tif target.respond_to?(:product_type) and target.product_type == \"com.apple.product-type.bundle\"",
      	"\t\t\ttarget.build_configurations.each do |config|",
		"\t\t\t\tputs \"Setting 'CODE_SIGNING_ALLOWED' to 'NO' for #{target.name}\"",
        "\t\t\t\tconfig.build_settings['CODE_SIGNING_ALLOWED'] = 'NO'",
      	"\t\t\tend",
    	"\t\tend",
		"\tend",
		"end",
	};

	var extraPodfileLines = new List<string> ();
	extraPodfileLines.AddRange (dynamicFrameworkLines);
	extraPodfileLines.AddRange (avoidBundleSigning);

	FIREBASE_AB_TESTING_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_AUTH_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_CLOUD_FIRESTORE_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_CLOUD_FUNCTIONS_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_CLOUD_MESSAGING_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_CRASHLYTICS_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_CORE_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_DATABASE_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_DYNAMIC_LINKS_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_INSTALLATIONS_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	//FIREBASE_PERFORMANCE_MONITORING_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_REMOTE_CONFIG_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	FIREBASE_STORAGE_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	
	GOOGLE_GOOGLE_APP_MEASUREMENT_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	GOOGLE_PROMISES_OBJC_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	GOOGLE_GTM_SESSION_FETCHER_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	GOOGLE_NANOPB_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	GOOGLE_GOOGLE_UTILITIES_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	GOOGLE_GOOGLE_DATA_TRANSPORT_ARTIFACT.ExtraPodfileLines = extraPodfileLines.ToArray ();
	// FIREBASE_APP_DISTRIBUTION_ARTIFACT.ExtraPodfileLines = dynamicFrameworkLines;
	// FIREBASE_APP_CHECK_ARTIFACT.ExtraPodfileLines = dynamicFrameworkLines;
	
	var inAppMessagingWorkaround = new [] {
		"post_install do |installer|",
		"\tinstaller.pods_project.targets.each do |target|",
		"\t\tif target.name == \"FirebaseInAppMessaging\"",
		"\t\t\ttarget.build_configurations.each do |config|",
		"\t\t\t\tif config.name == 'Release'",
		"\t\t\t\t\tputs \"Linking missing 'GoogleUtilities' framework to #{target.name}\"",
		"\t\t\t\t\tconfig.build_settings['OTHER_LDFLAGS'] ||= ['$(inherited)','-framework \"GoogleUtilities\"']",
		"\t\t\t\tend",
		"\t\t\tend",
		"\t\tend",
		"\t\tif target.respond_to?(:product_type) and target.product_type == \"com.apple.product-type.bundle\"",
      	"\t\t\ttarget.build_configurations.each do |config|",
		"\t\t\t\tputs \"Setting 'CODE_SIGNING_ALLOWED' to 'NO' for #{target.name}\"",
        "\t\t\t\tconfig.build_settings['CODE_SIGNING_ALLOWED'] = 'NO'",
      	"\t\t\tend",
    	"\t\tend",
		"\tend",
		"end",
	};

	var inAppMessagingLines = new List<string> ();
	inAppMessagingLines.AddRange (dynamicFrameworkLines);
	inAppMessagingLines.AddRange (inAppMessagingWorkaround);

	//FIREBASE_IN_APP_MESSAGING_ARTIFACT.ExtraPodfileLines = inAppMessagingLines.ToArray ();
}

void SetArtifactsSamples ()
{
	// Firebase components
	FIREBASE_AB_TESTING_ARTIFACT.Samples              = null;
	FIREBASE_ANALYTICS_ARTIFACT.Samples               = new [] { "AnalyticsSample" };
	FIREBASE_AUTH_ARTIFACT.Samples                    = new [] { "AuthSample" };
	FIREBASE_CLOUD_FIRESTORE_ARTIFACT.Samples         = new [] { "CloudFirestoreSample" };
	FIREBASE_CLOUD_FUNCTIONS_ARTIFACT.Samples         = new [] { "CloudFunctionsSample" };
	FIREBASE_CLOUD_MESSAGING_ARTIFACT.Samples         = new [] { "CloudMessagingSample" };
	FIREBASE_CORE_ARTIFACT.Samples                    = null;
	FIREBASE_CRASHLYTICS_ARTIFACT.Samples             = new [] { "CrashlyticsSample" };
	FIREBASE_DATABASE_ARTIFACT.Samples                = new [] { "DatabaseSample" };
	FIREBASE_DYNAMIC_LINKS_ARTIFACT.Samples           = new [] { "DynamicLinksSample" };
	//FIREBASE_IN_APP_MESSAGING_ARTIFACT.Samples        = new [] { "InAppMessagingSample" };
	FIREBASE_INSTALLATIONS_ARTIFACT.Samples           = null;
	//FIREBASE_PERFORMANCE_MONITORING_ARTIFACT.Samples  = new [] { "PerformanceMonitoringSample" };
	FIREBASE_REMOTE_CONFIG_ARTIFACT.Samples           = new [] { "RemoteConfigSample" };
	FIREBASE_STORAGE_ARTIFACT.Samples                 = new [] { "StorageSample" };
	//FIREBASE_APP_DISTRIBUTION_ARTIFACT.Samples        = new [] { "AppDistributionSample" };
	//FIREBASE_APP_CHECK_ARTIFACT.Samples               = new [] { "AppCheckSample" };

	// Google components
	GOOGLE_ANALYTICS_ARTIFACT.Samples                 = new [] { "AnalyticsSample" };
	GOOGLE_CAST_ARTIFACT.Samples                      = new [] { "CastSample" };
	GOOGLE_MAPS_ARTIFACT.Samples                      = new [] { "GoogleMapsSample" };
	GOOGLE_MOBILE_ADS_ARTIFACT.Samples                = new [] { "MobileAdsExample" };
	GOOGLE_PLACES_ARTIFACT.Samples                    = new [] { "GooglePlacesSample" };
	GOOGLE_SIGN_IN_ARTIFACT.Samples                   = new [] { "SignInExample" };
	GOOGLE_TAG_MANAGER_ARTIFACT.Samples               = new [] { "TagManagerSample" };

	// MLKit
	MLKIT_VISION.Samples                              = new [] { "MLKitVisionSample" };
}
