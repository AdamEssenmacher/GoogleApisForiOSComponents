# Google Analytics

This path is retained for existing links, but this repository does not maintain a copied Google Analytics walkthrough.

These packages are thin .NET bindings over native Google Apple SDKs. Use the official native documentation as the source of truth for setup, product behavior, console workflows, quotas, policy requirements, and examples.

## Native Documentation

- Google Analytics documentation: https://developers.google.com/analytics/devguides/collection/ios/v3

## Binding Project

- Package ID: `AdamE.Google.iOS.Analytics`
- Managed namespace: `Google.Analytics`
- Status note: This binding project is present in the repository. Check the top-level README and package feed for current publication status before depending on it.

## Binding Notes

The native Google Analytics v3 singleton maps to `Google.Analytics.Gai.SharedInstance` in this binding.

Document only binding-specific caveats in this repository. Product usage guidance belongs in the official native docs.
