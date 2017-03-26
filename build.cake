var target = Argument("Target", "Default");
var configuration =
    HasArgument("Configuration") ? Argument<string>("Configuration") :
    EnvironmentVariable("Configuration") != null ? EnvironmentVariable("Configuration") :
    "Release";
var preReleaseSuffix =
    HasArgument("PreReleaseSuffix") ? Argument<string>("PreReleaseSuffix") :
    (AppVeyor.IsRunningOnAppVeyor && AppVeyor.Environment.Repository.Tag.IsTag) ? null :
    EnvironmentVariable("PreReleaseSuffix") != null ? EnvironmentVariable("PreReleaseSuffix") :
    "beta";
var buildNumber =
    HasArgument("BuildNumber") ? Argument<int>("BuildNumber") :
    AppVeyor.IsRunningOnAppVeyor ? AppVeyor.Environment.Build.Number :
    TravisCI.IsRunningOnTravisCI ? TravisCI.Environment.Build.BuildNumber :
    EnvironmentVariable("BuildNumber") != null ? int.Parse(EnvironmentVariable("BuildNumber")) :
    0;

var artifactsDirectory = Directory("./Artifacts");

Task("Clean")
    .Does(() =>
    {
        CleanDirectory(artifactsDirectory);
    });

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        DotNetCoreRestore();
    });

 Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        foreach(var project in GetFiles("./**/*.csproj"))
        {
            DotNetCoreBuild(
                project.GetDirectory().FullPath,
                new DotNetCoreBuildSettings()
                {
                    Configuration = configuration
                });
        }
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        foreach(var project in GetFiles("./**/*.Test.csproj"))
        {
            var absoluteXmlPath = MakeAbsolute(artifactsDirectory.Path.CombineWithFilePath(project.GetFilenameWithoutExtension())).FullPath + ".xml";
            DotNetCoreTest(
                project.ToString(),
                new DotNetCoreTestSettings()
                {
                    ArgumentCustomization = args => args
                        .Append("--test-adapter-path:.")
                        .Append("--logger:xunit;LogFilePath=" + absoluteXmlPath),
                    Configuration = configuration,
                    NoBuild = true
                });
        }
    });

Task("Pack")
    .IsDependentOn("Test")
    .Does(() =>
    {
        string versionSuffix = null;
        if (!string.IsNullOrEmpty(preReleaseSuffix))
        {
            versionSuffix = preReleaseSuffix + "-" + buildNumber.ToString("D4");
        }

        foreach (var project in GetFiles("./Source/**/Serilog.Exceptions.csproj"))
        {
            DotNetCorePack(
                project.GetDirectory().FullPath,
                new DotNetCorePackSettings()
                {
                    Configuration = configuration,
                    OutputDirectory = artifactsDirectory,
                    VersionSuffix = versionSuffix
                });
        }
    });

Task("Default")
    .IsDependentOn("Pack");

RunTarget(target);