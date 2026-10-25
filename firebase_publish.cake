// Metadata only: this target never downloads native SDKs or edits project files.
Task ("firebase-publish-metadata")
	.Does (() => {
		SetArtifactsDependencies ();
		SetArtifactsPodSpecs ();
		SetArtifactsExtraPodfileLines ();
		var roots = ARTIFACTS.Values.Where (a => a.ComponentGroup == ComponentGroup.Firebase && !a.Ignore).ToArray ();
		var artifacts = OrderArtifactsByDependencies (roots);
		var records = artifacts.Select (a => {
			var project = $"source/{a.ComponentGroup}/{a.CsprojName}/{a.CsprojName}.csproj";
			return new {
				key = ARTIFACTS.First (pair => Object.ReferenceEquals (pair.Value, a)).Key,
				id = XmlPeek (project, "/Project/PropertyGroup/PackageId"),
				version = a.NugetVersion,
				project,
				firebase = a.ComponentGroup == ComponentGroup.Firebase,
				minimumOS = a.MinimunSupportedVersion,
				dependencies = (a.Dependencies ?? new Artifact [0]).Select (d => XmlPeek ($"source/{d.ComponentGroup}/{d.CsprojName}/{d.CsprojName}.csproj", "/Project/PropertyGroup/PackageId")).ToArray (),
				pods = a.PodSpecs,
				extraPodfileLines = a.ExtraPodfileLines,
				downloads = ExternalDownloads.Where (pair => (a.PodSpecs ?? new PodSpec [0]).Any (p => p.Name == pair.Key)).ToDictionary (pair => pair.Key, pair => pair.Value)
			};
		}).ToArray ();
		var destination = Argument ("metadata-output", "output/firebase-metadata.json");
		EnsureDirectoryExists (System.IO.Path.GetDirectoryName (destination));
		System.IO.File.WriteAllText (destination, JsonSerializer.Serialize (records, new JsonSerializerOptions { WriteIndented = true }));
	});
