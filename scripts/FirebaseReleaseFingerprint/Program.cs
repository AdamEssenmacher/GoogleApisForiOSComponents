using System.Text.Json;
using FirebaseReleaseFingerprint;

var root = args[0];
var selected = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!.ToHashSet();
var symbols = BuildInputs.ArtifactSymbols(File.ReadAllText(Path.Combine(root, "components.cake")));
var excluded = symbols.Where(p => !selected.Contains(p.Value)).Select(p => p.Key).ToHashSet();
var paths = new[] { "build.cake", "common.cake", "poco.cake", "custom_externals_download.cake" };
var sources = paths.ToDictionary(p => p, p => File.ReadAllText(Path.Combine(root, p)));
File.WriteAllText(args[2], BuildInputs.Normalize(sources, excluded));
