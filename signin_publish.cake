#load "poco.cake"
#load "components.cake"
#load "common.cake"
#load "custom_externals_download.cake"
using System.Text.Json;
var SOLUTION_PATH = "./Xamarin.Google.sln";

// An independent metadata entry point leaves the existing package build fingerprint unchanged.
Task ("google-signin-publish-metadata").Does (() => {
    SetArtifactsDependencies ();
    SetArtifactsPodSpecs ();
    SetArtifactsExtraPodfileLines ();
    var artifacts = new List<Artifact> ();
    var visiting = new HashSet<Artifact> ();
    Action<Artifact> visit = null;
    visit = artifact => {
        if (artifacts.Contains (artifact)) return;
        if (!visiting.Add (artifact)) throw new Exception ("Cyclic Sign-In dependency closure.");
        foreach (var dependency in artifact.Dependencies ?? new Artifact [0]) visit (dependency);
        visiting.Remove (artifact);
        artifacts.Add (artifact);
    };
    visit (GOOGLE_SIGN_IN_ARTIFACT);
    var records = artifacts.Select (a => {
        var project = $"source/{a.ComponentGroup}/{a.CsprojName}/{a.CsprojName}.csproj";
        return new {
            key = ARTIFACTS.First (pair => Object.ReferenceEquals (pair.Value, a)).Key,
            id = XmlPeek (project, "/Project/PropertyGroup/PackageId"), version = a.NugetVersion,
            project, firebase = false, minimumOS = a.MinimunSupportedVersion,
            dependencies = (a.Dependencies ?? new Artifact [0]).Select (d => XmlPeek ($"source/{d.ComponentGroup}/{d.CsprojName}/{d.CsprojName}.csproj", "/Project/PropertyGroup/PackageId")).ToArray (),
            pods = a.PodSpecs, extraPodfileLines = a.ExtraPodfileLines,
            downloads = ExternalDownloads.Where (pair => (a.PodSpecs ?? new PodSpec [0]).Any (p => p.Name == pair.Key)).ToDictionary (pair => pair.Key, pair => pair.Value)
        };
    }).ToArray ();
    var destination = Argument ("metadata-output", "output/signin-metadata.json");
    EnsureDirectoryExists (System.IO.Path.GetDirectoryName (destination));
    System.IO.File.WriteAllText (destination, JsonSerializer.Serialize (records, new JsonSerializerOptions { WriteIndented = true }));
});
RunTarget ("google-signin-publish-metadata");
