// Shared producer-only invocation. No CocoaPods code or consumer downloader.
void RunFirebase13Command(string target, string names, string archive, string cache)
{
    var args = new ProcessArgumentBuilder();
    args.AppendQuoted("tools/firebase13/native.py").Append(target);
    if (!string.IsNullOrWhiteSpace(names)) args.Append("--names").AppendQuoted(names);
    if (!string.IsNullOrWhiteSpace(archive)) args.Append("--archive").AppendQuoted(archive);
    if (!string.IsNullOrWhiteSpace(cache)) args.Append("--cache").AppendQuoted(cache);
    if (StartProcess("python3", new ProcessSettings { Arguments = args }) != 0)
        throw new Exception("Firebase 13 XBD operation failed; see diagnostics above.");
}
