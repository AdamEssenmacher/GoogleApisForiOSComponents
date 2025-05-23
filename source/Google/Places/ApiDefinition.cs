﻿using System;
using System.Runtime.InteropServices;

using UIKit;
using Foundation;
using ObjCRuntime;
using CoreGraphics;
using CoreLocation;

namespace Google.Places {
	// @interface GMSAddressComponent : NSObject
	[BaseType (typeof (NSObject), Name = "GMSAddressComponent")]
	interface AddressComponent {
		// @property(nonatomic, readonly, copy) NSString *type;
		[Obsolete ("Type property is deprecated in favor of Types.")]
		[BindAs (typeof (PlaceType))]
		[Export ("type")]
		NSString Type { get; }

		// @property (readonly, nonatomic, strong) NSArray<NSString *> * _Nonnull types;
		[BindAs (typeof (PlaceType []))]
		[Export ("types", ArgumentSemantic.Strong)]
		NSString [] Types { get; }

		// @property(nonatomic, readonly, copy) NSString *name;
		[Export ("name")]
		string Name { get; }

		// @property (readonly, copy, nonatomic) NSString * _Nullable shortName;
		[NullAllowed]
		[Export ("shortName")]
		string ShortName { get; }
	}

	interface IAutocompleteFetcherDelegate {
	}

	// @protocol GMSAutocompleteFetcherDelegate <NSObject>
	[Model]
	[Protocol]
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteFetcherDelegate")]
	interface AutocompleteFetcherDelegate {
		// - (void)didAutocompleteWithPredictions:(NSArray *)predictions;
		[Abstract]
		[Export ("didAutocompleteWithPredictions:")]
		void DidAutocomplete (AutocompletePrediction [] predictions);

		// - (void)didFailAutocompleteWithError:(NSError *)error;
		[Abstract]
		[Export ("didFailAutocompleteWithError:")]
		void DidFailAutocomplete (NSError error);
	}

	// @interface GMSAutocompleteFetcher : NSObject
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteFetcher")]
	interface AutocompleteFetcher {
		// - (instancetype)initWithFilter:(nullable GMSAutocompleteFilter *)filter NS_DESIGNATED_INITIALIZER;
		[Export ("initWithFilter:")]
		NativeHandle Constructor ([NullAllowed] AutocompleteFilter filter);

		// @property(nonatomic, weak) id<GMSAutocompleteFetcherDelegate> delegate;
		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		IAutocompleteFetcherDelegate Delegate { get; set; }

		// @property(nonatomic, strong) GMSAutocompleteFilter *autocompleteFilter;
		[NullAllowed]
		[Export ("autocompleteFilter", ArgumentSemantic.Strong)]
		AutocompleteFilter AutocompleteFilter { get; set; }

		// -(void)provideSessionToken:(GMSAutocompleteSessionToken * _Nonnull)sessionToken;
		[Export ("provideSessionToken:")]
		void ProvideSessionToken ([NullAllowed] AutocompleteSessionToken sessionToken);

		// - (void)sourceTextHasChanged:(NSString *)text;
		[Export ("sourceTextHasChanged:")]
		void SourceTextHasChanged ([NullAllowed] string text);
	}

	// @interface GMSAutocompleteFilter : NSObject
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteFilter")]
	interface AutocompleteFilter {
		// @property (assign, nonatomic) GMSPlacesAutocompleteTypeFilter type;
		[Export ("type", ArgumentSemantic.Assign)]
		PlacesAutocompleteTypeFilter Type { get; set; }

		// @property(nonatomic, copy) NSString *country;
		[NullAllowed]
		[Export ("country", ArgumentSemantic.Copy)]
		string Country { get; set; }

		// @property (nonatomic) CLLocation * _Nullable origin;
		[NullAllowed]
		[Export ("origin", ArgumentSemantic.Assign)]
		CLLocation Origin { get; set; }

		// @property (nonatomic) id<GMSPlaceLocationBias> _Nullable locationBias;
		[NullAllowed]
		[Export ("locationBias", ArgumentSemantic.Assign)]
		NSObject LocationBias { get; set; }

		// @property (nonatomic) id<GMSPlaceLocationRestriction> _Nullable locationRestriction;
		[NullAllowed]
		[Export ("locationRestriction", ArgumentSemantic.Assign)]
		NSObject LocationRestriction { get; set; }		
	}

	// @interface GMSAutocompleteMatchFragment : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteMatchFragment")]
	interface AutocompleteMatchFragment {
		// @property (readonly, nonatomic) NSUInteger offset;
		[Export ("offset")]
		nuint Offset { get; }

		// @property (readonly, nonatomic) NSUInteger length;
		[Export ("length")]
		nuint Length { get; }
	}

	// @interface GMSAutocompletePrediction : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSAutocompletePrediction")]
	interface AutocompletePrediction {
		// extern NSString *const _Nonnull kGMSAutocompleteMatchAttribute;
		[Field ("kGMSAutocompleteMatchAttribute", "__Internal")]
		NSString AutocompleteMatchAttribute { get; }

		// @property (readonly, copy, nonatomic) NSAttributedString * attributedFullText;
		[Export ("attributedFullText", ArgumentSemantic.Copy)]
		NSAttributedString AttributedFullText { get; }

		// @property(nonatomic, copy, readonly) NSAttributedString *attributedPrimaryText;
		[Export ("attributedPrimaryText", ArgumentSemantic.Copy)]
		NSAttributedString AttributedPrimaryText { get; }

		// @property(nonatomic, copy, readonly) NSAttributedString *attributedSecondaryText;
		[Export ("attributedSecondaryText", ArgumentSemantic.Copy)]
		NSAttributedString AttributedSecondaryText { get; }

		// @property (readonly, copy, nonatomic) NSString * placeID;
		[Export ("placeID", ArgumentSemantic.Copy)]
		string PlaceId { get; }

		// @property (readonly, copy, nonatomic) NSArray * types;
		[BindAs (typeof (PlaceType []))]
		[Export ("types", ArgumentSemantic.Copy)]
		NSString [] Types { get; }

		// @property (readonly, nonatomic) NSNumber * _Nullable distanceMeters;
		[NullAllowed]
		[Export ("distanceMeters")]
		NSNumber DistanceMeters { get; }
	}

	interface IAutocompleteResultsViewControllerDelegate {
	}

	// @protocol GMSAutocompleteResultsViewControllerDelegate <NSObject>
	[Model]
	[Protocol]
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteResultsViewControllerDelegate")]
	interface AutocompleteResultsViewControllerDelegate {
		// @required - (void)resultsController:(GMSAutocompleteResultsViewController *)resultsController didAutocompleteWithPlace:(GMSPlace *)place;
		[Abstract]
		[EventArgs ("AutocompleteResultsViewControllerAutocompleted")]
		[EventName ("Autocompleted")]
		[Export ("resultsController:didAutocompleteWithPlace:")]
		void DidAutocomplete (AutocompleteResultsViewController resultsController, Place place);

		// @required - (void)resultsController:(GMSAutocompleteResultsViewController *)resultsController didFailAutocompleteWithError:(NSError *)error;
		[Abstract]
		[EventArgs ("AutocompleteResultsViewControllerAutocompleteFailed")]
		[EventName ("AutocompleteFailed")]
		[Export ("resultsController:didFailAutocompleteWithError:")]
		void DidFailAutocomplete (AutocompleteResultsViewController resultsController, NSError error);

		// @optional - (BOOL)resultsController:(GMSAutocompleteResultsViewController *)resultsController didSelectPrediction:(GMSAutocompletePrediction *)prediction;
		[DefaultValue (true)]
		[DelegateName ("AutocompleteResultsViewControllerPredictionSelected")]
		[Export ("resultsController:didSelectPrediction:")]
		bool DidSelectPrediction (AutocompleteResultsViewController resultsController, AutocompletePrediction prediction);

		// @optional - (void)didUpdateAutocompletePredictionsForResultsController:(GMSAutocompleteResultsViewController *)resultsController;
		[EventArgs ("AutocompleteResultsViewControllerAutocompletePredictionsUpdated")]
		[EventName ("AutocompletePredictionsUpdated")]
		[Export ("didUpdateAutocompletePredictionsForResultsController:")]
		void DidUpdateAutocompletePredictions (AutocompleteResultsViewController resultsController);

		// @optional - (void)didRequestAutocompletePredictionsForResultsController:(GMSAutocompleteResultsViewController *)resultsController;
		[EventArgs ("AutocompleteResultsViewControllerAutocompletePredictionsRequested")]
		[EventName ("AutocompletePredictionsRequested")]
		[Export ("didRequestAutocompletePredictionsForResultsController:")]
		void DidRequestAutocompletePredictions (AutocompleteResultsViewController resultsController);
	}

	// @interface GMSAutocompleteResultsViewController : UIViewController <UISearchResultsUpdating>
	[BaseType (typeof (UIViewController),
		Name = "GMSAutocompleteResultsViewController",
		Delegates = new string [] { "Delegate" },
		Events = new Type [] { typeof (AutocompleteResultsViewControllerDelegate) })]
	interface AutocompleteResultsViewController : IUISearchResultsUpdating {
		// @property(nonatomic, weak) id<GMSAutocompleteResultsViewControllerDelegate> delegate;
		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		IAutocompleteResultsViewControllerDelegate Delegate { get; set; }

		// @property(nonatomic, strong) GMSAutocompleteFilter *autocompleteFilter;
		[NullAllowed]
		[Export ("autocompleteFilter", ArgumentSemantic.Strong)]
		AutocompleteFilter AutocompleteFilter { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellBackgroundColor;
		[Export ("tableCellBackgroundColor", ArgumentSemantic.Strong)]
		UIColor TableCellBackgroundColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellSeparatorColor;
		[Export ("tableCellSeparatorColor", ArgumentSemantic.Strong)]
		UIColor TableCellSeparatorColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextColor;
		[Export ("primaryTextColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextHighlightColor;
		[Export ("primaryTextHighlightColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextHighlightColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *secondaryTextColor;
		[Export ("secondaryTextColor", ArgumentSemantic.Strong)]
		UIColor SecondaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *GMS_NULLABLE_PTR tintColor;
		[NullAllowed]
		[Export ("tintColor", ArgumentSemantic.Strong)]
		UIColor TintColor { get; set; }

		// @property (assign, nonatomic) GMSPlaceField placeFields;
		[Export ("placeFields", ArgumentSemantic.Assign)]
		PlaceField PlaceFields { get; set; }
	}

	// @interface GMSAutocompleteSessionToken : NSObject
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteSessionToken")]
	interface AutocompleteSessionToken {
	}

	interface IAutocompleteTableDataSourceDelegate {
	}

	[Model]
	[Protocol]
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteTableDataSourceDelegate")]
	interface AutocompleteTableDataSourceDelegate {
		// @required - (void)tableDataSource:(GMSAutocompleteTableDataSource *)tableDataSource didAutocompleteWithPlace:(GMSPlace *)place;
		[Abstract]
		[EventArgs ("AutocompleteTableDataSourceAutocompleted")]
		[EventName ("Autocompleted")]
		[Export ("tableDataSource:didAutocompleteWithPlace:")]
		void DidAutocomplete (AutocompleteTableDataSource tableDataSource, Place place);

		// @required - (void)tableDataSource:(GMSAutocompleteTableDataSource *)tableDataSource didFailAutocompleteWithError:(NSError *)error;
		[Abstract]
		[EventArgs ("AutocompleteTableDataSourceAutocompleteFailed")]
		[EventName ("AutocompleteFailed")]
		[Export ("tableDataSource:didFailAutocompleteWithError:")]
		void DidFailAutocomplete (AutocompleteTableDataSource tableDataSource, NSError error);

		// @optional - (BOOL)tableDataSource:(GMSAutocompleteTableDataSource *)tableDataSource didSelectPrediction:(GMSAutocompletePrediction *)prediction;
		[DefaultValue (true)]
		[DelegateName ("AutocompleteTableDataSourcePredictionSelected")]
		[Export ("tableDataSource:didSelectPrediction:")]
		bool DidSelectPrediction (AutocompleteTableDataSource tableDataSource, AutocompletePrediction prediction);

		// @optional - (void)didUpdateAutocompletePredictionsForTableDataSource: (GMSAutocompleteTableDataSource *)tableDataSource;
		[EventArgs ("AutocompleteTableDataSourceAutocompletePredictionsUpdated")]
		[EventName ("AutocompletePredictionsUpdated")]
		[Export ("didUpdateAutocompletePredictionsForTableDataSource:")]
		void DidUpdateAutocompletePredictions (AutocompleteTableDataSource tableDataSource);

		// @optional - (void)didRequestAutocompletePredictionsForTableDataSource:(GMSAutocompleteTableDataSource *)tableDataSource;
		[EventArgs ("AutocompleteTableDataSourceAutocompletePredictionsRequested")]
		[EventName ("AutocompletePredictionsRequested")]
		[Export ("didRequestAutocompletePredictionsForTableDataSource:")]
		void DidRequestAutocompletePredictions (AutocompleteTableDataSource tableDataSource);
	}

	// @interface GMSAutocompleteTableDataSource : NSObject <UITableViewDataSource, UITableViewDelegate>
	[BaseType (typeof (NSObject),
		Name = "GMSAutocompleteTableDataSource",
		Delegates = new string [] { "Delegate" },
		Events = new Type [] { typeof (AutocompleteTableDataSourceDelegate) })]
	interface AutocompleteTableDataSource : IUITableViewDataSource, IUITableViewDelegate {
		// @property(nonatomic, weak) IBOutlet id<GMSAutocompleteTableDataSourceDelegate> delegate;
		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		IAutocompleteTableDataSourceDelegate Delegate { get; set; }

		// @property(nonatomic, strong) GMSAutocompleteFilter *autocompleteFilter;
		[NullAllowed]
		[Export ("autocompleteFilter", ArgumentSemantic.Strong)]
		AutocompleteFilter AutocompleteFilter { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellBackgroundColor;
		[Export ("tableCellBackgroundColor", ArgumentSemantic.Strong)]
		UIColor TableCellBackgroundColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellSeparatorColor;
		[Export ("tableCellSeparatorColor", ArgumentSemantic.Strong)]
		UIColor TableCellSeparatorColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextColor;
		[Export ("primaryTextColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextHighlightColor;
		[Export ("primaryTextHighlightColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextHighlightColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *secondaryTextColor;
		[Export ("secondaryTextColor", ArgumentSemantic.Strong)]
		UIColor SecondaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *GMS_NULLABLE_PTR tintColor;
		[NullAllowed]
		[Export ("tintColor", ArgumentSemantic.Strong)]
		UIColor TintColor { get; set; }

		// @property (assign, nonatomic) GMSPlaceField placeFields;
		[Export ("placeFields", ArgumentSemantic.Assign)]
		PlaceField PlaceFields { get; set; }

		// - (void)sourceTextHasChanged:(NSString *)text;
		[Export ("sourceTextHasChanged:")]
		void SourceTextHasChanged ([NullAllowed] string text);

		// -(void)clearResults;
		[Export ("clearResults")]
		void ClearResults ();
	}

	interface IAutocompleteViewControllerDelegate {
	}

	[Model]
	[Protocol]
	[BaseType (typeof (NSObject), Name = "GMSAutocompleteViewControllerDelegate")]
	interface AutocompleteViewControllerDelegate {
		// @required - (void)viewController:(GMSAutocompleteViewController *)viewController didAutocompleteWithPlace:(GMSPlace *)place;
		[Abstract]
		[EventArgs ("AutocompleteViewControllerAutocompleted")]
		[EventName ("Autocompleted")]
		[Export ("viewController:didAutocompleteWithPlace:")]
		void DidAutocomplete (AutocompleteViewController viewController, Place place);

		// @required - (void)viewController:(GMSAutocompleteViewController *)viewController didFailAutocompleteWithError:(NSError *)error;
		[Abstract]
		[EventArgs ("AutocompleteViewControllerAutocompleteFailed")]
		[EventName ("AutocompleteFailed")]
		[Export ("viewController:didFailAutocompleteWithError:")]
		void DidFailAutocomplete (AutocompleteViewController viewController, NSError error);

		// @required - (void)wasCancelled:(GMSAutocompleteViewController *)viewController;
		[Abstract]
		[EventArgs ("AutocompleteViewControllerWasCancelled")]
		[Export ("wasCancelled:")]
		void WasCancelled (AutocompleteViewController viewController);

		// @optional - (BOOL)viewController:(GMSAutocompleteViewController *)viewController didSelectPrediction:(GMSAutocompletePrediction *)prediction;
		[DefaultValue (true)]
		[DelegateName ("AutocompleteViewControllerPredictionSelected")]
		[Export ("viewController:didSelectPrediction:")]
		bool DidSelectPrediction (AutocompleteViewController viewController, AutocompletePrediction prediction);

		// @optional - (void)didUpdateAutocompletePredictions:(GMSAutocompleteViewController *)viewController;
		[EventArgs ("AutocompleteViewControllerAutocompletePredictionsUpdated")]
		[EventName ("AutocompletePredictionsUpdated")]
		[Export ("didUpdateAutocompletePredictions:")]
		void DidUpdateAutocompletePredictions (AutocompleteViewController viewController);

		// @optional - (void)didRequestAutocompletePredictions:(GMSAutocompleteViewController *)viewController;
		[EventArgs ("AutocompleteViewControllerPredictionsRequested")]
		[EventName ("AutocompletePredictionsRequested")]
		[Export ("didRequestAutocompletePredictions:")]
		void DidRequestAutocompletePredictions (AutocompleteViewController viewController);
	}

	// @interface GMSAutocompleteViewController : UIViewController
	[BaseType (typeof (UIViewController),
			   Name = "GMSAutocompleteViewController",
		   Delegates = new string [] { "Delegate" },
			   Events = new Type [] { typeof (AutocompleteViewControllerDelegate) })]
	interface AutocompleteViewController {
		// @property(nonatomic, weak) IBOutlet id<GMSAutocompleteViewControllerDelegate> delegate;
		[NullAllowed]
		[Export ("delegate", ArgumentSemantic.Weak)]
		IAutocompleteViewControllerDelegate Delegate { get; set; }

		// @property(nonatomic, strong) GMSAutocompleteFilter *autocompleteFilter;
		[NullAllowed]
		[Export ("autocompleteFilter", ArgumentSemantic.Strong)]
		AutocompleteFilter AutocompleteFilter { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellBackgroundColor;
		[Export ("tableCellBackgroundColor", ArgumentSemantic.Strong)]
		UIColor TableCellBackgroundColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *tableCellSeparatorColor;
		[Export ("tableCellSeparatorColor", ArgumentSemantic.Strong)]
		UIColor TableCellSeparatorColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextColor;
		[Export ("primaryTextColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *primaryTextHighlightColor;
		[Export ("primaryTextHighlightColor", ArgumentSemantic.Strong)]
		UIColor PrimaryTextHighlightColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *secondaryTextColor;
		[Export ("secondaryTextColor", ArgumentSemantic.Strong)]
		UIColor SecondaryTextColor { get; set; }

		// @property(nonatomic, strong) IBInspectable UIColor *GMS_NULLABLE_PTR tintColor;
		[NullAllowed]
		[Export ("tintColor", ArgumentSemantic.Strong)]
		UIColor TintColor { get; set; }

		// @property (assign, nonatomic) GMSPlaceField placeFields;
		[Export ("placeFields", ArgumentSemantic.Assign)]
		PlaceField PlaceFields { get; set; }
	}

	// @interface GMSTime : NSObject
	[BaseType (typeof (NSObject), Name = "GMSTime")]
	interface Time {
		// @property (readonly, assign, nonatomic) NSUInteger hour;
		[Export ("hour")]
		nuint Hour { get; }

		// @property (readonly, assign, nonatomic) NSUInteger minute;
		[Export ("minute")]
		nuint Minute { get; }
	}

	// @interface GMSEvent : NSObject
	[BaseType (typeof (NSObject), Name = "GMSEvent")]
	interface Event {
		// @property (readonly, assign, nonatomic) GMSDayOfWeek day;
		[Export ("day", ArgumentSemantic.Assign)]
		DayOfWeek Day { get; }

		// @property (readonly, nonatomic, strong) GMSTime * _Nonnull time;
		[Export ("time", ArgumentSemantic.Strong)]
		Time Time { get; }
	}

	// @interface GMSPeriod : NSObject
	[BaseType (typeof (NSObject), Name = "GMSPeriod")]
	interface Period {
		// @property (readonly, nonatomic, strong) GMSEvent * _Nonnull openEvent;
		[Export ("openEvent", ArgumentSemantic.Strong)]
		Event OpenEvent { get; }

		// @property (readonly, nonatomic, strong) GMSEvent * _Nullable closeEvent;
		[NullAllowed]
		[Export ("closeEvent", ArgumentSemantic.Strong)]
		Event CloseEvent { get; }
	}

	// @interface GMSOpeningHours : NSObject
	[BaseType (typeof (NSObject), Name = "GMSOpeningHours")]
	interface OpeningHours {
		// @property (readonly, nonatomic, strong) NSArray<GMSPeriod *> * _Nullable periods;
		[NullAllowed]
		[Export ("periods", ArgumentSemantic.Strong)]
		Period [] Periods { get; }

		// @property (readonly, nonatomic, strong) NSArray<NSString *> * _Nullable weekdayText;
		[NullAllowed]
		[Export ("weekdayText", ArgumentSemantic.Strong)]
		string [] WeekdayText { get; }
	}

	// @interface GMSPlace : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSPlace")]
	interface Place {
		// @property (readonly, copy, nonatomic) NSString * name;
		[Export ("name", ArgumentSemantic.Copy)]
		string Name { get; }

		// @property (readonly, copy, nonatomic) NSString * placeID;
		[Export ("placeID", ArgumentSemantic.Copy)]
		string Id { get; }

		// @property (readonly, nonatomic) CLLocationCoordinate2D coordinate;
		[Export ("coordinate", ArgumentSemantic.Assign)]
		CLLocationCoordinate2D Coordinate { get; }

		// @property (readonly, copy, nonatomic) NSString * phoneNumber;
		[NullAllowed]
		[Export ("phoneNumber", ArgumentSemantic.Copy)]
		string PhoneNumber { get; }

		// @property (readonly, copy, nonatomic) NSString * formattedAddress;
		[NullAllowed]
		[Export ("formattedAddress", ArgumentSemantic.Copy)]
		string FormattedAddress { get; }

		// @property (readonly, nonatomic) float rating;
		[Export ("rating", ArgumentSemantic.Assign)]
		float Rating { get; }

		// @property (readonly, nonatomic) GMSPlacesPriceLevel priceLevel;
		[Export ("priceLevel", ArgumentSemantic.Assign)]
		PlacesPriceLevel PriceLevel { get; }

		// @property (readonly, copy, nonatomic) NSArray * types;
		[BindAs (typeof (PlaceType []))]
		[Export ("types", ArgumentSemantic.Copy)]
		NSString [] Types { get; }

		// @property (readonly, copy, nonatomic) NSURL * website;
		[NullAllowed]
		[Export ("website", ArgumentSemantic.Copy)]
		NSUrl Website { get; }

		// @property (readonly, copy, nonatomic) NSAttributedString * attributions;
		[NullAllowed]
		[Export ("attributions", ArgumentSemantic.Copy)]
		NSAttributedString Attributions { get; }

		// @property(nonatomic, strong, readonly) GMSCoordinateBounds *viewport;
		[NullAllowed]
		[Export ("viewportInfo", ArgumentSemantic.Strong)]
		PlaceViewportInfo Viewport { get; }

		// @property(nonatomic, copy, readonly) GMS_NSArrayOf(GMSAddressComponent *) *GMS_NULLABLE_PTR addressComponents;
		[NullAllowed]
		[Export ("addressComponents", ArgumentSemantic.Copy)]
		AddressComponent [] AddressComponents { get; }

		// @property (readonly, nonatomic, strong) GMSPlusCode * _Nullable plusCode;
		[NullAllowed]
		[Export ("plusCode", ArgumentSemantic.Strong)]
		PlusCode PlusCode { get; }

		// @property (readonly, nonatomic, strong) GMSOpeningHours * _Nullable openingHours;
		[NullAllowed]
		[Export ("openingHours", ArgumentSemantic.Strong)]
		OpeningHours OpeningHours { get; }

		// @property (readonly, assign, nonatomic) NSUInteger userRatingsTotal;
		[Export ("userRatingsTotal")]
		nuint UserRatingsTotal { get; }

		// @property (readonly, copy, nonatomic) NSArray<GMSPlacePhotoMetadata *> * _Nullable photos;
		[NullAllowed]
		[Export ("photos", ArgumentSemantic.Copy)]
		PlacePhotoMetadata [] Photos { get; }

		// @property (readonly, nonatomic) NSNumber * _Nullable UTCOffsetMinutes;
		[NullAllowed]
		[Export ("UTCOffsetMinutes")]
		NSNumber UtcOffsetMinutes { get; }

		// @property(nonatomic, readonly) GMSPlacesBusinessStatus businessStatus;
		[Export ("businessStatus")]
		PlacesBusinessStatus BusinessStatus { get; }

		// -(GMSPlaceOpenStatus)isOpenAtDate:(NSDate * _Nonnull)date;
		[Export ("isOpenAtDate:")]
		PlaceOpenStatus IsOpen (NSDate date);

		// -(GMSPlaceOpenStatus)isOpen;
		[Export ("isOpen")]
		PlaceOpenStatus IsOpen ();

		// @property(nonatomic, readonly, nullable) UIColor *iconBackgroundColor;
		[Export ("iconBackgroundColor")]
		UIColor IconBackgroundColor { get; }

		// @property(nonatomic, readonly, nullable) NSURL *iconImageURL;
		[Export ("iconImageURL")]
		NSUrl IconImageUrl ();
	}

	// @interface GMSPlaceViewportInfo : NSObject
	[BaseType (typeof(NSObject), Name = "GMSPlaceViewportInfo")]
	interface PlaceViewportInfo
	{
		// @property (readonly, nonatomic) CLLocationCoordinate2D northEast;
		[Export ("northEast")]
		CLLocationCoordinate2D NorthEast { get; }

		// @property (readonly, nonatomic) CLLocationCoordinate2D southWest;
		[Export ("southWest")]
		CLLocationCoordinate2D SouthWest { get; }

		// @property (readonly, getter = isValid, nonatomic) BOOL valid;
		[Export ("valid")]
		bool Valid { [Bind ("isValid")] get; }

		// -(id)initWithNorthEast:(CLLocationCoordinate2D)northEast southWest:(CLLocationCoordinate2D)southWest;
		[Export ("initWithNorthEast:southWest:")]
		NativeHandle Constructor (CLLocationCoordinate2D northEast, CLLocationCoordinate2D southWest);
	}

	// @interface GMSPlaceLikelihood : NSObject <NSCopying>
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSPlaceLikelihood")]
	interface PlaceLikelihood : INSCopying {
		// @property (readonly, nonatomic, strong) GMSPlace * place;
		[Export ("place", ArgumentSemantic.Strong)]
		Place Place { get; }

		// @property (readonly, assign, nonatomic) double likelihood;
		[Export ("likelihood")]
		double Likelihood { get; }

		// -(instancetype)initWithPlace:(GMSPlace *)place likelihood:(double)likelihood;
		[DesignatedInitializer]
		[Export ("initWithPlace:likelihood:")]
		NativeHandle Constructor (Place place, double likelihood);
	}

	// @interface GMSPlaceLikelihoodList : NSObject
	[BaseType (typeof (NSObject), Name = "GMSPlaceLikelihoodList")]
	interface PlaceLikelihoodList {
		// @property (copy, nonatomic) NSArray * likelihoods;
		[Export ("likelihoods", ArgumentSemantic.Copy)]
		PlaceLikelihood [] Likelihoods { get; set; }

		// @property (readonly, copy, nonatomic) NSAttributedString * attributions;
		[NullAllowed]
		[Export ("attributions", ArgumentSemantic.Copy)]
		NSAttributedString Attributions { get; }
	}

	// @interface GMSPlacePhotoMetadata : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSPlacePhotoMetadata")]
	interface PlacePhotoMetadata {
		// @property(nonatomic, readonly, copy) NSAttributedString* GMS_NULLABLE_PTR attributions;
		[NullAllowed]
		[Export ("attributions", ArgumentSemantic.Copy)]
		NSAttributedString Attributions { get; }

		// @property(nonatomic, readonly, assign) CGSize maxSize;
		[Export ("maxSize", ArgumentSemantic.Assign)]
		CGSize MaxSize { get; }
	}

	// @interface GMSPlacePhotoMetadataList : NSObject
	[BaseType (typeof (NSObject), Name = "GMSPlacePhotoMetadataList")]
	interface PlacePhotoMetadataList {
		// @property(nonatomic, readonly, copy) GMS_NSArrayOf(GMSPlacePhotoMetadata *) * results;
		[Export ("results", ArgumentSemantic.Copy)]
		PlacePhotoMetadata [] Results { get; }
	}

	// typedef void (^GMSPlaceResultCallback)(GMSPlace *NSError *);
	delegate void PlaceResultHandler ([NullAllowed] Place result, [NullAllowed] NSError error);

	// typedef void (^GMSPlaceLikelihoodListCallback)(GMSPlaceLikelihoodList *NSError *);
	delegate void PlaceLikelihoodListHandler ([NullAllowed] PlaceLikelihoodList likelihoodList, [NullAllowed] NSError error);

	// typedef void (^GMSPlaceLikelihoodsCallback)(NSArray<GMSPlaceLikelihood *> * _Nullable, NSError * _Nullable);
	delegate void PlaceLikelihoodsHandler ([NullAllowed] PlaceLikelihood [] likelihoods, [NullAllowed] NSError error);

	// typedef void (^GMSAutocompletePredictionsCallback)(NSArray *NSError *);
	delegate void AutocompletePredictionsHandler ([NullAllowed] AutocompletePrediction [] results, [NullAllowed] NSError error);

	// typedef void (^GMSPlacePhotoMetadataResultCallback)(GMSPlacePhotoMetadataList *GMS_NULLABLE_PTR photos, NSError *GMS_NULLABLE_PTR error);
	delegate void PlacePhotoMetadataResultHandler ([NullAllowed] PlacePhotoMetadataList photos, [NullAllowed] NSError error);

	// typedef void (^GMSPlacePhotoImageResultCallback)(UIImage *GMS_NULLABLE_PTR photo, NSError *GMS_NULLABLE_PTR error);
	delegate void PlacePhotoImageResultHandler ([NullAllowed] UIImage photo, [NullAllowed] NSError error);

	// @interface GMSPlacesClient : NSObject
	[DisableDefaultCtor]
	[BaseType (typeof (NSObject), Name = "GMSPlacesClient")]
	interface PlacesClient {
		// extern NSString *const _Nonnull kGMSPlacesErrorDomain;
		[Field ("kGMSPlacesErrorDomain", "__Internal")]
		NSString PlacesErrorDomain { get; }

		// +(instancetype)sharedClient;
		[Static]
		[Export ("sharedClient")]
		PlacesClient SharedInstance { get; }

		// + (BOOL)provideAPIKey:(NSString *)key;
		[Static]
		[Export ("provideAPIKey:")]
		bool ProvideApiKey (string key);

		// + (NSString *)openSourceLicenseInfo;
		[Static]
		[Export ("openSourceLicenseInfo")]
		string OpenSourceLicenseInfo { get; }

		// + (NSString *)SDKVersion;
		[Static]
		[Export ("SDKVersion")]
		string SdkVersion { get; }

		// +(NSString * _Nonnull)SDKLongVersion;
		[Static]
		[Export ("SDKLongVersion")]
		string SdkLongVersion { get; }

		// -(void)lookUpPlaceID:(NSString *)placeID callback:(GMSPlaceResultCallback)callback;
		[Async]
		[Export ("lookUpPlaceID:callback:")]
		void LookUpPlaceId (string placeId, PlaceResultHandler callback);

		// - (void)lookUpPhotosForPlaceID:(NSString *)placeID callback:(GMSPlacePhotoMetadataResultCallback)callback;
		[Async]
		[Export ("lookUpPhotosForPlaceID:callback:")]
		void LookUpPhotos (string placeId, PlacePhotoMetadataResultHandler callback);

		// - (void)loadPlacePhoto:(GMSPlacePhotoMetadata *)photo callback:(GMSPlacePhotoImageResultCallback)callback;
		[Async]
		[Export ("loadPlacePhoto:callback:")]
		void LoadPlacePhoto (PlacePhotoMetadata photoMetadata, PlacePhotoImageResultHandler callback);

		// - (void)loadPlacePhoto:(GMSPlacePhotoMetadata *)photo constrainedToSize:(CGSize)maxSize scale:(CGFloat)scale callback:(GMSPlacePhotoImageResultCallback)callback;
		[Async]
		[Export ("loadPlacePhoto:constrainedToSize:scale:callback:")]
		void LoadPlacePhoto (PlacePhotoMetadata photoMetadata, CGSize maxSize, nfloat scale, PlacePhotoImageResultHandler callback);

		// -(void)currentPlaceWithCallback:(GMSPlaceLikelihoodListCallback)callback;
		[Async]
		[Export ("currentPlaceWithCallback:")]
		void CurrentPlace (PlaceLikelihoodListHandler callback);

		// -(void)findAutocompletePredictionsFromQuery:(NSString * _Nonnull)query bounds:(GMSCoordinateBounds * _Nullable)bounds boundsMode:(GMSAutocompleteBoundsMode)boundsMode filter:(GMSAutocompleteFilter * _Nullable)filter sessionToken:(GMSAutocompleteSessionToken * _Nonnull)sessionToken callback:(GMSAutocompletePredictionsCallback _Nonnull)callback;
		[Export ("findAutocompletePredictionsFromQuery:filter:sessionToken:callback:")]
		void FindAutocompletePredictions (string query, [NullAllowed] AutocompleteFilter filter, [NullAllowed] AutocompleteSessionToken sessionToken, AutocompletePredictionsHandler callback);

		// -(void)fetchPlaceFromPlaceID:(NSString * _Nonnull)placeID placeFields:(GMSPlaceField)placeFields sessionToken:(GMSAutocompleteSessionToken * _Nullable)sessionToken callback:(GMSPlaceResultCallback _Nonnull)callback;
		[Export ("fetchPlaceFromPlaceID:placeFields:sessionToken:callback:")]
		void FetchPlace (string placeId, PlaceField placeFields, [NullAllowed] AutocompleteSessionToken sessionToken, PlaceResultHandler callback);

		// -(void)findPlaceLikelihoodsFromCurrentLocationWithPlaceFields:(GMSPlaceField)placeFields callback:(GMSPlaceLikelihoodsCallback _Nonnull)callback;
		[Export ("findPlaceLikelihoodsFromCurrentLocationWithPlaceFields:callback:")]
		void FindPlaceLikelihoodsFromCurrentLocation (PlaceField placeFields, PlaceLikelihoodsHandler callback);
	}

	// @interface GMSPlusCode : NSObject
	[BaseType (typeof (NSObject), Name = "GMSPlusCode")]
	interface PlusCode {
		// @property (readonly, copy, nonatomic) NSString * _Nonnull globalCode;
		[Export ("globalCode")]
		string GlobalCode { get; }

		// @property (readonly, copy, nonatomic) NSString * _Nullable compoundCode;
		[NullAllowed]
		[Export ("compoundCode")]
		string CompoundCode { get; }
	}
}

