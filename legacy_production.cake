// Compatibility dispatcher. Actual CocoaPods production uses the isolated locked source tree.
void RunLegacyProductionCommand(string command, string target, string names, string mode, string channel = "signin", string output = "")
{
    var args = new ProcessArgumentBuilder();
    args.AppendQuoted("tools/legacy/producer.py").Append(command);
    if (!string.IsNullOrWhiteSpace(target)) args.Append("--target").AppendQuoted(target);
    if (!string.IsNullOrWhiteSpace(names)) args.Append("--names").AppendQuoted(names);
    args.Append("--mode").AppendQuoted(mode).Append("--channel").AppendQuoted(channel);
    if (!string.IsNullOrWhiteSpace(output)) args.Append("--output").AppendQuoted(MakeAbsolute((FilePath)output).FullPath);
    foreach (var key in new [] { "archive", "cache" }) {
        var value = Argument(key, "");
        if (!string.IsNullOrWhiteSpace(value)) args.Append("--" + key).AppendQuoted(value);
    }
    if (StartProcess("python3", new ProcessSettings { Arguments = args }) != 0)
        throw new Exception("Isolated producer operation failed; see diagnostics above.");
}
