# Google Places

This path is retained for existing links, but this repository does not maintain a copied Google Places walkthrough.

These packages are thin .NET bindings over native Google Apple SDKs. Use the official native documentation as the source of truth for setup, product behavior, console workflows, quotas, policy requirements, and examples.

## Native Documentation

- Google Places documentation: https://developers.google.com/maps/documentation/places/ios-sdk

## Binding Project

- Package ID: `AdamE.Google.iOS.Places`
- Managed namespace: `Google.Places`
- Status note: This binding project is present in the repository and is part of the current Google package set described by the top-level README.

## Binding Notes

The native `GMSPlacesClient.provideAPIKey` setup maps to `Google.Places.PlacesClient.ProvideApiKey(apiKey)` in this binding. The managed namespace is `Google.Places`.

Document only binding-specific caveats in this repository. Product usage guidance belongs in the official native docs.
