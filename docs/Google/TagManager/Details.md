# Google Tag Manager

This path is retained for existing links, but this repository does not maintain a copied Google Tag Manager walkthrough.

These packages are thin .NET bindings over native Google Apple SDKs. Use the official native documentation as the source of truth for setup, product behavior, console workflows, quotas, policy requirements, and examples.

## Native Documentation

- Google Tag Manager documentation: https://developers.google.com/tag-platform/tag-manager/ios/v5

## Binding Project

- Package ID: `AdamE.Google.iOS.TagManager`
- Managed namespace: `Google.TagManager`
- Status note: This binding project is present in the repository. Check the top-level README and package feed for current publication status before depending on it.

## Binding Notes

Tag Manager initialization is exposed as `Google.TagManager.TagManager.Configure()`. Firebase-backed Tag Manager usage also depends on Firebase app initialization through `Firebase.Core.App.Configure()`.

Document only binding-specific caveats in this repository. Product usage guidance belongs in the official native docs.
