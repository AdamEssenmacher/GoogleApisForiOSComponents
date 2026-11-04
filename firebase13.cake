#load "firebase13_common.cake"
// Firebase-only binary-distribution experiment. No CocoaPods code is loaded here.
var target = Argument("target", "native-assets-validate");
var names = Argument("names", "");
var archive = Argument("archive", "");
var cache = Argument("cache", "");
Task(target).Does(() => {
    RunFirebase13Command(target, names, archive, cache);
});
RunTarget(target);
