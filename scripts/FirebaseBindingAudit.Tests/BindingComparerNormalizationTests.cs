using Xunit;

namespace FirebaseBindingAudit.Tests;

public sealed class BindingComparerNormalizationTests
{
    private static readonly string[] ManualAttributes = ["Wrap", "Advice", "Async", "Internal"];
    private static readonly string[] BindingAttributes = ["Export", "Field", "Notification"];

    [Fact]
    public void Compare_MatchesFirPrefixedEnumType()
    {
        var result = Compare(
            """
            public enum RemoteConfigFetchStatus
            {
                Success
            }
            """,
            """
            public enum FIRRemoteConfigFetchStatus
            {
                Success
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesAbtPrefixedEnumType()
    {
        var result = Compare(
            """
            public enum ExperimentPayloadExperimentOverflowPolicy
            {
                DiscardOldest
            }
            """,
            """
            public enum ABTExperimentPayloadExperimentOverflowPolicy
            {
                DiscardOldest
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesDelegateSuffixAndAcronymShape()
    {
        var result = Compare(
            """
            public delegate void MessagingFcmTokenFetchCompletionHandler(string token, NSError error);
            """,
            """
            public delegate void FIRMessagingFCMTokenFetchCompletion(string token, NSError error);
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesFirPrefixedProtocolInterfaceShape()
    {
        var result = Compare(
            """
            [Protocol(Name = "FIRPerformanceAttributable")]
            public interface IPerformanceAttributable
            {
                [Export("setValue:forAttribute:")]
                void SetValue(string value, string attribute);
            }
            """,
            """
            [Protocol]
            public interface IFIRPerformanceAttributable
            {
                [Export("setValue:forAttribute:")]
                void SetValue(string value, string attribute);
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesEnumMemberWithContainingEnumPrefix()
    {
        var result = Compare(
            """
            public enum AggregateSource
            {
                Server
            }
            """,
            """
            public enum FIRAggregateSource
            {
                FIRAggregateSourceServer
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesNormalizedTypeReferencesInExportedMembers()
    {
        var result = Compare(
            """
            public enum RemoteConfigFetchStatus
            {
                Success
            }

            [BaseType(typeof(NSObject), Name = "FIRRemoteConfig")]
            public interface RemoteConfig
            {
                [Export("lastFetchStatus")]
                RemoteConfigFetchStatus LastFetchStatus { get; }
            }
            """,
            """
            public enum FIRRemoteConfigFetchStatus
            {
                Success
            }

            [BaseType(typeof(NSObject))]
            public interface FIRRemoteConfig
            {
                [Export("lastFetchStatus")]
                FIRRemoteConfigFetchStatus LastFetchStatus { get; }
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesNormalizedTypeReferencesInMethodParameters()
    {
        var result = Compare(
            """
            [BaseType(typeof(NSObject), Name = "FIRLifecycleEvents")]
            public interface LifecycleEvents
            {
            }

            public enum ExperimentPayloadExperimentOverflowPolicy
            {
                DiscardOldest
            }

            [BaseType(typeof(NSObject), Name = "FIRExperimentController")]
            public interface ExperimentController
            {
                [Export("updateExperimentsWithServiceOrigin:events:policy:lastStartTime:payloads:completionHandler:")]
                void UpdateExperiments(string origin, LifecycleEvents events, ExperimentPayloadExperimentOverflowPolicy policy, double lastStartTime, NSData [] payloads, Action<NSError> completionHandler);
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRLifecycleEvents
            {
            }

            public enum ABTExperimentPayloadExperimentOverflowPolicy
            {
                DiscardOldest
            }

            [BaseType(typeof(NSObject))]
            public interface FIRExperimentController
            {
                [Export("updateExperimentsWithServiceOrigin:events:policy:lastStartTime:payloads:completionHandler:")]
                void UpdateExperimentsWithServiceOrigin(string origin, FIRLifecycleEvents events, ABTExperimentPayloadExperimentOverflowPolicy policy, double lastStartTime, NSData[] payloads, Action<NSError> completionHandler);
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesBooleanGetterAndPropertySelectorShape()
    {
        var result = Compare(
            """
            [BaseType(typeof(NSObject), Name = "FIRMessaging")]
            public interface Messaging
            {
                [Export("autoInitEnabled")]
                bool AutoInitEnabled { [Bind("isAutoInitEnabled")] get; set; }
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRMessaging
            {
                [Export("isAutoInitEnabled")]
                bool AutoInitEnabled { get; set; }
            }
            """);

        AssertNoFailures(result);
    }

    [Fact]
    public void Compare_MatchesReadOnlyPropertyAndZeroArgumentGetterMethodShape()
    {
        var result = Compare(
            """
            [BaseType(typeof(NSObject), Name = "FIRApp")]
            public interface App
            {
                [Static]
                [Export("defaultApp")]
                App DefaultInstance { get; }
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRApp
            {
                [Static]
                [Export("defaultApp")]
                FIRApp DefaultApp();
            }
            """);

        AssertNoFailures(result);
        Assert.Empty(result.Infos);
    }

    [Fact]
    public void Compare_MatchesRawAndTypedFoundationCollections()
    {
        var result = Compare(
            """
            [BaseType(typeof(NSObject), Name = "FIRAnalytics")]
            public interface Analytics
            {
                [Static]
                [Export("logEventWithName:parameters:")]
                void LogEvent(string name, NSDictionary<NSString, NSObject> parameters);
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRAnalytics
            {
                [Static]
                [Export("logEventWithName:parameters:")]
                void LogEventWithName(string name, NSDictionary parameters);
            }
            """);

        AssertNoFailures(result);
        Assert.Empty(result.Infos);
    }

    [Fact]
    public void Compare_DemotesNamedDelegateAndActionSmoothingToInfo()
    {
        var result = Compare(
            """
            public delegate void DatabaseReferenceCompletionHandler(NSError error, DatabaseReference reference);

            [BaseType(typeof(NSObject), Name = "FIRDatabaseReference")]
            public interface DatabaseReference
            {
                [Export("setValue:withCompletionBlock:")]
                void SetValue(NSObject value, DatabaseReferenceCompletionHandler completionBlock);
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRDatabaseReference
            {
                [Export("setValue:withCompletionBlock:")]
                void SetValue(NSObject value, Action<NSError, FIRDatabaseReference> completionBlock);
            }
            """);

        AssertNoFailures(result);
        var info = Assert.Single(result.Infos);
        Assert.Equal("preferred-api-shape", info.Category);
        Assert.Contains("Action<> smoothing", info.Message);
    }

    [Fact]
    public void Compare_KeepsNonEquivalentActionCallbackAsSignatureDrift()
    {
        var result = Compare(
            """
            public delegate void DatabaseReferenceCompletionHandler(NSError error, DatabaseReference reference);

            [BaseType(typeof(NSObject), Name = "FIRDatabaseReference")]
            public interface DatabaseReference
            {
                [Export("setValue:withCompletionBlock:")]
                void SetValue(NSObject value, DatabaseReferenceCompletionHandler completionBlock);
            }
            """,
            """
            [BaseType(typeof(NSObject))]
            public interface FIRDatabaseReference
            {
                [Export("setValue:withCompletionBlock:")]
                void SetValue(NSObject value, Action<NSError> completionBlock);
            }
            """);

        Assert.Contains(result.Failures, static failure => failure.Category == "signature-drift");
    }

    [Fact]
    public void Parse_HonorsConfiguredBindingAttributes()
    {
        var baselineFile = Path.Combine(Path.GetTempPath(), $"firebase-binding-audit-baseline-{Guid.NewGuid():N}.cs");

        try
        {
            File.WriteAllText(
                baselineFile,
                """
                [BaseType(typeof(NSObject), Name = "FIRThing")]
                public interface Thing
                {
                    [BindMe("doThing:")]
                    void DoThing(string value);
                }
                """);

            var parser = new BindingSyntaxParser(ManualAttributes, ["BindMe"]);
            var snapshot = parser.Parse([baselineFile], []);
            var type = Assert.Single(snapshot.BoundTypes.Values);
            var member = Assert.Single(type.Members.Values);

            Assert.Equal("BindMe", member.BindingAttribute);
            Assert.Equal("doThing:", member.BindingValue);
        }
        finally
        {
            if (File.Exists(baselineFile))
            {
                File.Delete(baselineFile);
            }
        }
    }

    private static TargetComparisonResult Compare(string baselineSource, string generatedSource)
    {
        var baselineFile = Path.Combine(Path.GetTempPath(), $"firebase-binding-audit-baseline-{Guid.NewGuid():N}.cs");
        var generatedFile = Path.Combine(Path.GetTempPath(), $"firebase-binding-audit-generated-{Guid.NewGuid():N}.cs");

        try
        {
            File.WriteAllText(baselineFile, baselineSource);
            File.WriteAllText(generatedFile, generatedSource);

            var parser = new BindingSyntaxParser(ManualAttributes, BindingAttributes);
            var baseline = parser.Parse([baselineFile], []);
            var generated = parser.Parse([generatedFile], []);

            return new BindingComparer().Compare(baseline, generated);
        }
        finally
        {
            if (File.Exists(baselineFile))
            {
                File.Delete(baselineFile);
            }

            if (File.Exists(generatedFile))
            {
                File.Delete(generatedFile);
            }
        }
    }

    private static void AssertNoFailures(TargetComparisonResult result)
    {
        Assert.True(
            result.Failures.Count == 0,
            string.Join(Environment.NewLine, result.Failures.Select(static failure => $"{failure.Category}: {failure.Message}")));
    }
}
