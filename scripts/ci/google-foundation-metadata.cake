// Metadata only: no native downloads or builds.
#load "../../poco.cake"
#load "../../components.cake"
#load "../../common.cake"
#load "../../custom_externals_download.cake"
using System.Text.Json;
var SOLUTION_PATH = "./Xamarin.Google.sln";
Task ("metadata").Does (() => {
    SetArtifactsDependencies ();
    SetArtifactsPodSpecs ();
    SetArtifactsExtraPodfileLines ();
    var name = Argument ("component", "");
    if (name != "Google.Maps" && name != "Google.Places") throw new Exception ("Unexpected foundation component.");
    var ordered = new List<Artifact> ();
    var visiting = new HashSet<Artifact> ();
    Action<Artifact> visit = null;
    visit = a => {
        if (ordered.Contains (a)) return;
        if (!visiting.Add (a)) throw new Exception ("Cyclic foundation dependency closure.");
        foreach (var d in a.Dependencies ?? new Artifact [0]) visit (d);
        visiting.Remove (a);
        ordered.Add (a);
    };
    visit (ARTIFACTS [name]);
    var records = ordered.Select (a => new {
        artifactId = a.Id,
        key = ARTIFACTS.First (p => Object.ReferenceEquals (p.Value, a)).Key,
        project = $"source/{a.ComponentGroup}/{a.CsprojName}/{a.CsprojName}.csproj",
        version = a.NugetVersion, minimumOS = a.MinimunSupportedVersion,
        dependencies = (a.Dependencies ?? new Artifact [0]).Select (d => d.Id).ToArray (),
        pods = a.PodSpecs, extraPodfileLines = a.ExtraPodfileLines,
        downloads = ExternalDownloads.Where (p => (a.PodSpecs ?? new PodSpec [0]).Any (pod => pod.Name == p.Key)).ToDictionary (p => p.Key, p => p.Value)
    }).ToArray ();
    System.IO.File.WriteAllText (Argument ("metadata-output", ""), JsonSerializer.Serialize (records));
});
RunTarget ("metadata");
