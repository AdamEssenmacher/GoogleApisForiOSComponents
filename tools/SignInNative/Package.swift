// swift-tools-version:6.0
import PackageDescription

// Producer-only dependency graph. No SwiftPM files or build tasks are shipped to consumers.
let package = Package(
    name: "AdamESignInNative",
    platforms: [.iOS(.v15), .macCatalyst(.v15)],
    products: [.library(name: "SignInClosure", type: .static, targets: ["SignInClosure"])],
    dependencies: [
        .package(url: "https://github.com/google/GoogleSignIn-iOS.git", exact: "10.1.0"),
        .package(url: "https://github.com/openid/AppAuth-iOS.git", exact: "3.0.0"),
        .package(url: "https://github.com/google/GTMAppAuth.git", exact: "6.0.0"),
        .package(url: "https://github.com/google/app-check.git", exact: "12.0.0"),
        .package(url: "https://github.com/google/gtm-session-fetcher.git", exact: "5.3.1"),
        .package(url: "https://github.com/google/GoogleUtilities.git", exact: "8.1.5"),
        .package(url: "https://github.com/google/promises.git", exact: "2.4.1"),
    ],
    targets: [.target(name: "SignInClosure", dependencies: [
        .product(name: "GoogleSignIn", package: "GoogleSignIn-iOS"),
        .product(name: "AppAuth", package: "AppAuth-iOS"),
        .product(name: "AppAuthCore", package: "AppAuth-iOS"),
        .product(name: "GTMAppAuth", package: "GTMAppAuth"),
    ])]
)
