# Firebase Binding API Shape Policy

This note records the comparison policy used when triaging Firebase binding drift. The goal is to keep bindings thin and runtime-correct while avoiding unnecessary breaking changes for APIs that are already in use.

## Preferred Shape

- Selector correctness wins. If a binding has the wrong selector, wrong native argument type, or another mismatch that can throw before Firebase business logic runs, fix the binding directly.
- Prefer `Action<>` callback smoothing for new or additive APIs that bind Objective-C block callbacks.
- Do not replace existing named-delegate callback APIs just because `Action<>` is preferred. Keep the existing API for compatibility, then add the preferred `Action<>` shape case-by-case when it is useful and can be validated.
- Obsolete non-preferred callback shapes only after the preferred shape exists and the migration path is clear.
- Keep bindings thin. Avoid adding managed behavior in `ApiDefinition.cs` just to paper over native API shape differences.

## Audit Treatment

- Treat read-only properties and zero-argument getter methods as equivalent when they bind the same native selector and have equivalent return types.
- Treat raw and typed Foundation collection wrappers, such as `NSDictionary` and `NSDictionary<NSString, NSObject>`, as equivalent for binding-surface comparison when the selector is otherwise the same.
- Treat named-delegate callbacks and equivalent `Action<>` callbacks as compatible, but report them as informational preferred-shape candidates so they can be reviewed case-by-case.
- Do not normalize semantic delegate aliases that are not mechanically equivalent. Those should stay visible until header/source review proves the mapping.
- Do not churn enum underlying types or `nint`/`long`-style numeric differences without a concrete runtime or interoperability reason.

## Case-By-Case Obsoletion Flow

1. Confirm the native header shape and the current managed shape.
2. Add the preferred binding shape only when it improves runtime correctness or developer experience without hiding native semantics.
3. Add or update a targeted E2E case when the API can be exercised locally.
4. Keep the existing non-preferred API unless it was already runtime-broken.
5. Mark the older API obsolete only when the preferred replacement is present and the compatibility tradeoff is intentional.
