// Firebase-only binary-distribution experiment. No CocoaPods code is loaded here.
var target = Argument("target", "native-assets-validate");
var names = Argument("names", "");
var archive = Argument("archive", "");
var cache = Argument("cache", "");
Task(target).Does(() => {
    var args = new ProcessArgumentBuilder();
    args.AppendQuoted("tools/firebase13/native.py").Append(target);
    if (!string.IsNullOrEmpty(names)) args.Append("--names").AppendQuoted(names);
    if (!string.IsNullOrEmpty(archive)) args.Append("--archive").AppendQuoted(archive);
    if (!string.IsNullOrEmpty(cache)) args.Append("--cache").AppendQuoted(cache);
    if (StartProcess("python3", new ProcessSettings { Arguments = args }) != 0)
        throw new Exception("Firebase 13 XBD operation failed; see diagnostics above.");
});
RunTarget(target);
