#load "legacy_production.cake"
var legacyTarget = Argument("target", "nuget");
RunLegacyProductionCommand(legacyTarget == "plan" ? "plan" : "build", legacyTarget == "plan" ? "nuget" : legacyTarget, Argument("names", ""), "legacy");
