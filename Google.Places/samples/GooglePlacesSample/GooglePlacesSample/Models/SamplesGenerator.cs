﻿using System;
namespace GooglePlacesSample
{
	public class SamplesGenerator
	{
		public static SampleInformation [] [] CreateSamples () => new [] {
			new [] {
				new SampleInformation {
					StoryboardId = nameof (AutocompleteBaseViewController),
					Title = "Full-Screen Autocomplete",
					ColorTheme = ColorTheme.Default
				},
				new SampleInformation {
					StoryboardId = nameof (AutocompleteBaseViewController),
					Title = "Custom Autocomplete Styling",
					ColorTheme = ColorTheme.WhiteOnBlack
				},
				new SampleInformation {
					StoryboardId = nameof (AutocompleteBaseViewController),
					Title = "Push Autocomplete",
					ColorTheme = ColorTheme.Default,
					PushAutocomplete = true
				},
				new SampleInformation {
					StoryboardId = nameof (UISearchAutocompleteViewController),
					Title = "UISearchController",
					ColorTheme = ColorTheme.Default
				}
			},
			new [] {
				new SampleInformation {
					StoryboardId = nameof (GoogleOpenSourceViewController),
					Title = "Google Maps License"
				},
				new SampleInformation {
					StoryboardId = nameof (GoogleOpenSourceViewController),
					Title = "Google Places License"
				}
			}
		};
	}
}
