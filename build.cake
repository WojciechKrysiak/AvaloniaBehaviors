///////////////////////////////////////////////////////////////////////////////
// ADDINS
///////////////////////////////////////////////////////////////////////////////

#addin "nuget:?package=Polly&version=5.3.1"
#addin "nuget:?package=PackageReferenceEditor&version=0.0.3"

///////////////////////////////////////////////////////////////////////////////
// TOOLS
///////////////////////////////////////////////////////////////////////////////

#tool "nuget:?package=NuGet.CommandLine&version=4.3.0"
#tool "nuget:?package=xunit.runner.console&version=2.3.1"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using PackageReferenceEditor;
using Polly;

///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "Any CPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "wieslawsoltes/AvaloniaBehaviors";
var MasterBranch = "master";
var AssemblyInfoPath = File("./src/Shared/SharedAssemblyInfo.cs");
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";
var MSBuildSolution = "./AvaloniaBehaviors.sln";
var UnitTestsFramework = "net461";

///////////////////////////////////////////////////////////////////////////////
// .NET Core Projects
///////////////////////////////////////////////////////////////////////////////

var netCoreAppsRoot= "./samples";
var netCoreApps = new string[] { "BehaviorsTestApplication.NetCore" };
var netCoreProjects = netCoreApps.Select(name => 
    new {
        Path = string.Format("{0}/{1}", netCoreAppsRoot, name),
        Name = name,
        Framework = XmlPeek(string.Format("{0}/{1}/{1}.csproj", netCoreAppsRoot, name), "//*[local-name()='TargetFramework']/text()"),
        Runtimes = XmlPeek(string.Format("{0}/{1}/{1}.csproj", netCoreAppsRoot, name), "//*[local-name()='RuntimeIdentifiers']/text()").Split(';')
    }).ToList();

///////////////////////////////////////////////////////////////////////////////
// .NET Core UnitTests
///////////////////////////////////////////////////////////////////////////////

var netCoreUnitTestsRoot= "./tests";
var netCoreUnitTests = new string[] { 
    "Avalonia.Xaml.Interactivity.UnitTests",
    "Avalonia.Xaml.Interactions.UnitTests",
    "Avalonia.Xaml.Interactions.Custom.UnitTests"
};
var netCoreUnitTestsProjects = netCoreUnitTests.Select(name => 
    new {
        Name = name,
        Path = string.Format("{0}/{1}", netCoreUnitTestsRoot, name),
        File = string.Format("{0}/{1}/{1}.csproj", netCoreUnitTestsRoot, name)
    }).ToList();
var netCoreUnitTestsFrameworks = new List<string>() { "netcoreapp2.0" };
if (IsRunningOnWindows())
{
    netCoreUnitTestsFrameworks.Add("net461");
}

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isPlatformAnyCPU = StringComparer.OrdinalIgnoreCase.Equals(platform, "Any CPU");
var isPlatformX86 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x86");
var isPlatformX64 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x64");
var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals(MainRepo, BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals(MasterBranch, BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);
var isReleasable = StringComparer.OrdinalIgnoreCase.Equals(ReleasePlatform, platform) 
                   && StringComparer.OrdinalIgnoreCase.Equals(ReleaseConfiguration, configuration);
var isMyGetRelease = !isTagged && isReleasable;
var isNuGetRelease = isTagged && isReleasable;

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = ParseAssemblyInfo(AssemblyInfoPath).AssemblyVersion;

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER");
    }
}

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var testResultsDir = artifactsDir.Combine("test-results");
var nugetRoot = artifactsDir.Combine("nuget");
var dirSuffix = isPlatformAnyCPU ? configuration : platform + "/" + configuration;
var buildDirs = 
    GetDirectories("./src/**/bin/" + dirSuffix) + 
    GetDirectories("./src/**/obj/" + dirSuffix) + 
    GetDirectories("./samples/**/bin/" + dirSuffix) + 
    GetDirectories("./samples/**/obj/" + dirSuffix) +
    GetDirectories("./tests/**/bin/" + dirSuffix) + 
    GetDirectories("./tests/**/obj/" + dirSuffix);

///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////

var result = Updater.FindReferences("./build", "*.props", new string[] { });	

result.ValidateVersions();

Information("Setting NuGet package dependencies versions:");

var AvaloniaVersion = result.GroupedReferences["Avalonia"].FirstOrDefault().Version;

Information("Package: Avalonia, version: {0}", AvaloniaVersion);

var nuspecNuGetInteractivity = new NuGetPackSettings()
{
    Id = "Avalonia.Xaml.Interactivity",
    Version = version,
    Authors = new [] { "wieslawsoltes" },
    Owners = new [] { "wieslawsoltes" },
    LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
    ProjectUrl = new Uri("https://github.com/wieslawsoltes/AvaloniaBehaviors/"),
    RequireLicenseAcceptance = false,
    Symbols = false,
    NoPackageAnalysis = true,
    Description = "Easily add interactivity to your Avalonia apps using XAML Behaviors. Behaviors encapsulate reusable functionalities for elements that can be easily added to your XAML without the need for more imperative code.",
    Copyright = "Copyright 2017",
    Tags = new [] { "Avalonia", "Behavior", "Action", "Behaviors", "Actions", "Managed", "C#", "Interaction", "Interactivity", "Interactions", "Xaml" },
    Dependencies = new []
    {
        new NuSpecDependency { Id = "Avalonia", Version = AvaloniaVersion }
    },
    Files = new []
    {
        // netstandard2.0
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactivity/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactivity.dll", Target = "lib/netstandard2.0" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactivity/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactivity.xml", Target = "lib/netstandard2.0" },
        // net461
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactivity/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactivity.dll", Target = "lib/net461" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactivity/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactivity.xml", Target = "lib/net461" }
    },
    BasePath = Directory("./"),
    OutputDirectory = nugetRoot
};

var nuspecNuGetInteractions = new NuGetPackSettings()
{
    Id = "Avalonia.Xaml.Interactions",
    Version = version,
    Authors = new [] { "wieslawsoltes" },
    Owners = new [] { "wieslawsoltes" },
    LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
    ProjectUrl = new Uri("https://github.com/wieslawsoltes/AvaloniaBehaviors/"),
    RequireLicenseAcceptance = false,
    Symbols = false,
    NoPackageAnalysis = true,
    Description = "Easily add interactivity to your Avalonia apps using XAML Behaviors. Behaviors encapsulate reusable functionalities for elements that can be easily added to your XAML without the need for more imperative code.",
    Copyright = "Copyright 2017",
    Tags = new [] { "Avalonia", "Behavior", "Action", "Behaviors", "Actions", "Managed", "C#", "Interaction", "Interactivity", "Interactions", "Xaml" },
    Dependencies = new []
    {
        new NuSpecDependency { Id = "Avalonia.Xaml.Interactivity", Version = version }
    },
    Files = new []
    {
        // netstandard2.0
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactions.dll", Target = "lib/netstandard2.0" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactions.xml", Target = "lib/netstandard2.0" },
        // net461
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactions.dll", Target = "lib/net461" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactions.xml", Target = "lib/net461" }
    },
    BasePath = Directory("./"),
    OutputDirectory = nugetRoot
};

var nuspecNuGetInteractionsCustom = new NuGetPackSettings()
{
    Id = "Avalonia.Xaml.Interactions.Custom",
    Version = version,
    Authors = new [] { "wieslawsoltes" },
    Owners = new [] { "wieslawsoltes" },
    LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
    ProjectUrl = new Uri("https://github.com/wieslawsoltes/AvaloniaBehaviors/"),
    RequireLicenseAcceptance = false,
    Symbols = false,
    NoPackageAnalysis = true,
    Description = "Easily add interactivity to your Avalonia apps using XAML Behaviors. Behaviors encapsulate reusable functionalities for elements that can be easily added to your XAML without the need for more imperative code.",
    Copyright = "Copyright 2017",
    Tags = new [] { "Avalonia", "Behavior", "Action", "Behaviors", "Actions", "Managed", "C#", "Interaction", "Interactivity", "Interactions", "Xaml" },
    Dependencies = new []
    {
        new NuSpecDependency { Id = "Avalonia.Xaml.Interactivity", Version = version }
    },
    Files = new []
    {
        // netstandard2.0
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions.Custom/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactions.Custom.dll", Target = "lib/netstandard2.0" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions.Custom/bin/" + dirSuffix + "/netstandard2.0/" + "Avalonia.Xaml.Interactions.Custom.xml", Target = "lib/netstandard2.0" },
        // net461
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions.Custom/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactions.Custom.dll", Target = "lib/net461" },
        new NuSpecContent { Source = "src/Avalonia.Xaml.Interactions.Custom/bin/" + dirSuffix + "/net461/" + "Avalonia.Xaml.Interactions.Custom.xml", Target = "lib/net461" }
    },
    BasePath = Directory("./"),
    OutputDirectory = nugetRoot
};

var nuspecNuGetBehaviors = new NuGetPackSettings()
{
    Id = "Avalonia.Xaml.Behaviors",
    Version = version,
    Authors = new [] { "wieslawsoltes" },
    Owners = new [] { "wieslawsoltes" },
    LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
    ProjectUrl = new Uri("https://github.com/wieslawsoltes/AvaloniaBehaviors/"),
    RequireLicenseAcceptance = false,
    Symbols = false,
    NoPackageAnalysis = true,
    Description = "Easily add interactivity to your Avalonia apps using XAML Behaviors. Behaviors encapsulate reusable functionalities for elements that can be easily added to your XAML without the need for more imperative code.",
    Copyright = "Copyright 2017",
    Tags = new [] { "Avalonia", "Behavior", "Action", "Behaviors", "Actions", "Managed", "C#", "Interaction", "Interactivity", "Interactions", "Xaml" },
    Dependencies = new []
    {
        new NuSpecDependency { Id = "Avalonia.Xaml.Interactivity", Version = version },
        new NuSpecDependency { Id = "Avalonia.Xaml.Interactions", Version = version }
    },
    BasePath = Directory("./"),
    OutputDirectory = nugetRoot
};

var nuspecNuGetSettings = new List<NuGetPackSettings>();

nuspecNuGetSettings.Add(nuspecNuGetInteractivity);
nuspecNuGetSettings.Add(nuspecNuGetInteractions);
nuspecNuGetSettings.Add(nuspecNuGetInteractionsCustom);
nuspecNuGetSettings.Add(nuspecNuGetBehaviors);

var nugetPackages = nuspecNuGetSettings.Select(nuspec => {
    return nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));
}).ToArray();

///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////

Information("Building version {0} of AvaloniaBehaviors ({1}, {2}, {3}) using version {4} of Cake.", 
    version,
    platform,
    configuration,
    target,
    typeof(ICakeContext).Assembly.GetName().Version.ToString());

if (isRunningOnAppVeyor)
{
    Information("Repository Name: " + BuildSystem.AppVeyor.Environment.Repository.Name);
    Information("Repository Branch: " + BuildSystem.AppVeyor.Environment.Repository.Branch);
}

Information("Target: " + target);
Information("Platform: " + platform);
Information("Configuration: " + configuration);
Information("IsLocalBuild: " + isLocalBuild);
Information("IsRunningOnUnix: " + isRunningOnUnix);
Information("IsRunningOnWindows: " + isRunningOnWindows);
Information("IsRunningOnAppVeyor: " + isRunningOnAppVeyor);
Information("IsPullRequest: " + isPullRequest);
Information("IsMainRepo: " + isMainRepo);
Information("IsMasterBranch: " + isMasterBranch);
Information("IsTagged: " + isTagged);
Information("IsReleasable: " + isReleasable);
Information("IsMyGetRelease: " + isMyGetRelease);
Information("IsNuGetRelease: " + isNuGetRelease);

///////////////////////////////////////////////////////////////////////////////
// TASKS: VISUAL STUDIO
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectories(buildDirs);
    CleanDirectory(artifactsDir);
    CleanDirectory(testResultsDir);
    CleanDirectory(nugetRoot);
});

Task("Restore-NuGet-Packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    var maxRetryCount = 5;
    var toolTimeout = 1d;
    Policy
        .Handle<Exception>()
        .Retry(maxRetryCount, (exception, retryCount, context) => {
            if (retryCount == maxRetryCount)
            {
                throw exception;
            }
            else
            {
                Verbose("{0}", exception);
                toolTimeout+=0.5;
            }})
        .Execute(()=> {
            if(isRunningOnWindows)
            {
                NuGetRestore(MSBuildSolution, new NuGetRestoreSettings {
                    ToolTimeout = TimeSpan.FromMinutes(toolTimeout)
                });
            }
        });
});

Task("Build")
    .IsDependentOn("Restore-NuGet-Packages")
    .Does(() =>
{
    if (isRunningOnWindows)
    {
        MSBuild(MSBuildSolution, settings => {
            settings.UseToolVersion(MSBuildToolVersion.VS2017);
            settings.SetConfiguration(configuration);
            settings.WithProperty("Platform", "\"" + platform + "\"");
            settings.SetVerbosity(Verbosity.Minimal);
            settings.SetMaxCpuCount(0);
        });
    }
});

Task("Run-Unit-Tests")
    .IsDependentOn("Build")
    .Does(() =>
{
    //if(!isRunningOnWindows)
    //   return;
    //var assemblies = GetFiles("./tests/**/bin/" + dirSuffix + "/" + UnitTestsFramework + "/*.UnitTests.dll");
    //var settings = new XUnit2Settings { 
    //    ToolPath = (isPlatformAnyCPU || isPlatformX86) ? 
    //        Context.Tools.Resolve("xunit.console.x86.exe") :
    //        Context.Tools.Resolve("xunit.console.exe"),
    //    OutputDirectory = testResultsDir,
    //    XmlReportV1 = true,
    //    NoAppDomain = true,
    //    Parallelism = ParallelismOption.None,
    //    ShadowCopy = false
    //};
    //foreach (var assembly in assemblies)
    //{
    //    XUnit2(assembly.FullPath, settings);
    //}
});

Task("Create-NuGet-Packages")
    .IsDependentOn("Run-Unit-Tests")
    .Does(() =>
{
    foreach(var nuspec in nuspecNuGetSettings)
    {
        NuGetPack(nuspec);
    }
});

Task("Publish-MyGet")
    .IsDependentOn("Create-NuGet-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)
    .WithCriteria(() => isMyGetRelease)
    .Does(() =>
{
    var apiKey = EnvironmentVariable("MYGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("MYGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }

    foreach(var nupkg in nugetPackages)
    {
        NuGetPush(nupkg, new NuGetPushSettings {
            Source = apiUrl,
            ApiKey = apiKey
        });
    }
})
.OnError(exception =>
{
    Information("Publish-MyGet Task failed, but continuing with next Task...");
});

Task("Publish-NuGet")
    .IsDependentOn("Create-NuGet-Packages")
    .WithCriteria(() => !isLocalBuild)
    .WithCriteria(() => !isPullRequest)
    .WithCriteria(() => isMainRepo)
    .WithCriteria(() => isMasterBranch)
    .WithCriteria(() => isNuGetRelease)
    .Does(() =>
{
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve NuGet API key.");
    }

    var apiUrl = EnvironmentVariable("NUGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve NuGet API url.");
    }

    foreach(var nupkg in nugetPackages)
    {
        NuGetPush(nupkg, new NuGetPushSettings {
            ApiKey = apiKey,
            Source = apiUrl
        });
    }
})
.OnError(exception =>
{
    Information("Publish-NuGet Task failed, but continuing with next Task...");
});

///////////////////////////////////////////////////////////////////////////////
// TASKS: .NET Core
///////////////////////////////////////////////////////////////////////////////

Task("Restore-NetCore")
    .IsDependentOn("Clean")
    .Does(() =>
{
    foreach (var project in netCoreProjects)
    {
        DotNetCoreRestore(project.Path);
    }
});

Task("Run-Unit-Tests-NetCore")
    .IsDependentOn("Clean")
    .Does(() => 
{
    foreach (var project in netCoreUnitTestsProjects)
    {
        DotNetCoreRestore(project.Path);
        foreach(var framework in netCoreUnitTestsFrameworks)
        {
            Information("Running tests for: {0}, framework: {1}", project.Name, framework);
            DotNetCoreTest(project.File, new DotNetCoreTestSettings {
                Configuration = configuration,
                Framework = framework
            });
        }
    }
});

Task("Build-NetCore")
    .IsDependentOn("Restore-NetCore")
    .Does(() => 
{
    foreach (var project in netCoreProjects)
    {
        Information("Building: {0}", project.Name);
        DotNetCoreBuild(project.Path, new DotNetCoreBuildSettings {
            Configuration = configuration,
            MSBuildSettings = new DotNetCoreMSBuildSettings() {
                MaxCpuCount = 0
            }
        });
    }
});

///////////////////////////////////////////////////////////////////////////////
// TARGETS
///////////////////////////////////////////////////////////////////////////////

Task("Package")
  .IsDependentOn("Create-NuGet-Packages");

Task("Default")
  .IsDependentOn("Run-Unit-Tests");

Task("AppVeyor")
  .IsDependentOn("Run-Unit-Tests-NetCore")
  .IsDependentOn("Build-NetCore")
  .IsDependentOn("Publish-MyGet")
  .IsDependentOn("Publish-NuGet");

Task("Travis")
  .IsDependentOn("Run-Unit-Tests-NetCore")
  .IsDependentOn("Build-NetCore");

///////////////////////////////////////////////////////////////////////////////
// EXECUTE
///////////////////////////////////////////////////////////////////////////////

RunTarget(target);
