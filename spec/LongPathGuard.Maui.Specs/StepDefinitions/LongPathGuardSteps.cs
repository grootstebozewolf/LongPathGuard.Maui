using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace LongPathGuard.Maui.Specs;

[Binding]
public class LongPathGuardSteps
{
    private static readonly string TargetsFilePath =
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "LongPathGuard.Maui", "build", "LongPathGuard.Maui.targets"));

    private string _projectDirectory = string.Empty;
    private string _nugetPackageRoot = string.Empty;
    private int _buildExitCode;
    private string _buildOutput = string.Empty;
    private readonly Dictionary<string, string> _extraBuildProps = new();

    [AfterScenario]
    public void Cleanup()
    {
        if (!string.IsNullOrEmpty(_projectDirectory) && Directory.Exists(_projectDirectory))
        {
            try { Directory.Delete(_projectDirectory, recursive: true); } catch { }
        }
    }

    [Given("I have a MAUI project targeting net{float}-ios")]
    public void GivenIHaveAMAUIProjectTargetingNet_Ios(Decimal p0)
    {
        Assert.IsTrue(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "These tests target the Windows + paired Mac build scenario and must run on Windows.");
    }

    [Given("I build from Windows with a paired Mac build agent")]
    public void GivenIBuildFromWindowsWithAPairedMacBuildAgent()
    {
        Assert.IsTrue(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
            "The paired Mac build scenario is initiated from Windows.");
    }

    [Given("the solution lives in a deep folder")]
    public void GivenTheSolutionLivesInADeepFolder()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "lpg", "deep");
        Directory.CreateDirectory(_projectDirectory);

        var needed = 201 - _projectDirectory.Length - 1;
        var fileName = new string('a', Math.Max(10, needed)) + ".txt";
        File.WriteAllText(Path.Combine(_projectDirectory, fileName), "dummy");
    }

    [Given("LongPathGuard.Maui package is referenced")]
    public void GivenLongPathGuard_MauiPackageIsReferenced()
    {
        WriteTestProject(_projectDirectory);
    }

    [When("I run dotnet build -f net{float}-ios -c Release")]
    public void WhenIRunDotnetBuild_FNet_Ios_CRelease(Decimal p0)
    {
        RunLongPathGuardTarget(_projectDirectory);
    }

    [Then("the build fails")]
    public void ThenTheBuildFails()
    {
        Assert.AreNotEqual(0, _buildExitCode,
            $"Expected build to fail but exit code was 0.\nOutput:\n{_buildOutput}");
    }

    [Then("the error clearly lists the offending long file paths")]
    public void ThenTheErrorClearlyListsTheOffendingLongFilePaths()
    {
        Assert.IsTrue(
            _buildOutput.Contains(_projectDirectory, StringComparison.OrdinalIgnoreCase),
            $"Expected output to list offending long file paths under '{_projectDirectory}'.\nOutput:\n{_buildOutput}");
    }

    [Then("the message explains the two workarounds: short solution path or enable LongPathsEnabled")]
    public void ThenTheMessageExplainsTheTwoWorkaroundsShortSolutionPathOrEnableLongPathsEnabled()
    {
        Assert.IsTrue(
            _buildOutput.Contains("LongPathsEnabled", StringComparison.OrdinalIgnoreCase),
            $"Expected 'LongPathsEnabled' in output.\nOutput:\n{_buildOutput}");
        Assert.IsTrue(
            _buildOutput.Contains("short path", StringComparison.OrdinalIgnoreCase)
            || _buildOutput.Contains("Move solution", StringComparison.OrdinalIgnoreCase),
            $"Expected a short-path workaround mention in output.\nOutput:\n{_buildOutput}");
    }

    [Given("the solution is in a short path")]
    public void GivenTheSolutionIsInAShortPath()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "lpg", "short");
        Directory.CreateDirectory(_projectDirectory);
        WriteTestProject(_projectDirectory);
    }

    [When("I build the iOS target")]
    public void WhenIBuildTheIOSTarget()
    {
        RunLongPathGuardTarget(_projectDirectory);
    }

    [Then("the build succeeds without long-path errors")]
    public void ThenTheBuildSucceedsWithoutLong_PathErrors()
    {
        Assert.AreEqual(0, _buildExitCode,
            $"Expected build to succeed but exit code was {_buildExitCode}.\nOutput:\n{_buildOutput}");
    }

    [Given("LongPathGuardEnabled is set to false")]
    public void GivenLongPathGuardEnabledIsSetToFalse()
    {
        _extraBuildProps["LongPathGuardEnabled"] = "false";
        _projectDirectory = Path.Combine(Path.GetTempPath(), "lpg", "disabled");
        Directory.CreateDirectory(_projectDirectory);
        WriteTestProject(_projectDirectory);
    }

    [When("long path files are present")]
    public void WhenLongPathFilesArePresent()
    {
        var needed = 201 - _projectDirectory.Length - 1;
        var fileName = new string('b', Math.Max(10, needed)) + ".txt";
        File.WriteAllText(Path.Combine(_projectDirectory, fileName), "dummy");
        RunLongPathGuardTarget(_projectDirectory);
    }

    [Then("the guard does not run and build proceeds normally")]
    public void ThenTheGuardDoesNotRunAndBuildProceedsNormally()
    {
        Assert.AreEqual(0, _buildExitCode,
            $"Expected guard to be disabled and build to succeed but exit code was {_buildExitCode}.\nOutput:\n{_buildOutput}");
        Assert.IsFalse(
            _buildOutput.Contains("LongPathGuard.Maui FAILURE", StringComparison.OrdinalIgnoreCase),
            $"Guard ran despite being disabled.\nOutput:\n{_buildOutput}");
    }

    [Given("the project sits in a deep folder without long source files")]
    public void GivenTheProjectSitsInADeepFolderWithoutLongSourceFiles()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "lpg", "deep-src");
        Directory.CreateDirectory(_projectDirectory);
    }

    [Given("an auto-generated scaffolding file with a long path exists under obj")]
    public void GivenAnAutoGeneratedScaffoldingFileWithALongPathExistsUnderObj()
    {
        var relative = Path.Combine(
            "obj",
            "ATT.Waldo.Shell",
            "x64",
            "Release",
            "net10.0-windows10.0.22621.0",
            "win-x64",
            "WinRT.SourceGenerator",
            "Generator.WinRTAotSourceGenerator");
        var dir = Path.Combine(_projectDirectory, relative);
        Directory.CreateDirectory(dir);

        var needed = 201 - dir.Length - 1;
        var fileName = new string('s', Math.Max(20, needed)) + ".g.cs";
        File.WriteAllText(Path.Combine(dir, fileName), "// <auto-generated />");
        Assert.IsTrue(Path.Combine(dir, fileName).Length > 200,
            "Scaffolding fixture must exceed MaxSafePathLength so a skip is observable.");
    }

    [Given("a long native asset exists in the NuGet package cache")]
    public void GivenALongNativeAssetExistsInTheNuGetPackageCache()
    {
        _nugetPackageRoot = Path.Combine(_projectDirectory, "nuget-cache");
        var shaderDir = Path.Combine(
            _nugetPackageRoot,
            "esri.arcgisruntime.maui",
            "200.8.1",
            "resources",
            "shaders");
        Directory.CreateDirectory(shaderDir);

        var needed = 201 - shaderDir.Length - 1;
        var fileName = new string('m', Math.Max(20, needed)) + ".metallib";
        File.WriteAllText(Path.Combine(shaderDir, fileName), "dummy-shader");
        Assert.IsTrue(Path.Combine(shaderDir, fileName).Length > 200,
            "Native-asset fixture must exceed MaxSafePathLength.");
    }

    private void WriteTestProject(string directory)
    {
        var targetsPath = TargetsFilePath.Replace("\\", "/");
        var content = $"""
            <Project>
              <Import Project="{targetsPath}" />
            </Project>
            """;
        File.WriteAllText(Path.Combine(directory, "TestApp.proj"), content);
    }

    private void RunLongPathGuardTarget(string directory)
    {
        var extraProps = string.Join(" ", _extraBuildProps.Select(kv => $"/p:{kv.Key}={kv.Value}"));
        var nugetRoot = string.IsNullOrEmpty(_nugetPackageRoot) ? string.Empty : _nugetPackageRoot;
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"msbuild TestApp.proj /t:LongPathGuard /p:TargetFramework=net9.0-ios /p:NuGetPackageRoot=\"{nugetRoot}\" {extraProps}",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        _buildOutput = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        _buildExitCode = process.ExitCode;
    }
}
