Feature: LongPathGuard.Maui prevents MSB3026 long path errors in MAUI iOS builds

As a .NET MAUI developer
I want the build to fail early when any file (from any NuGet package) has a path that is too long for Windows
So that I get a clear message with exact fixes instead of cryptic MSB3026 errors during iOS publish from Windows

Background:
  Given I have a MAUI project targeting net9.0-ios
  And I build from Windows with a paired Mac build agent

Scenario: Guard detects long paths and fails with helpful message
  Given the solution lives in a deep folder
  And LongPathGuard.Maui package is referenced
  When I run dotnet build -f net9.0-ios -c Release
  Then the build fails
  And the error clearly lists the offending long file paths
  And the message explains the two workarounds: short solution path or enable LongPathsEnabled

Scenario: Guard stays silent on short safe paths
  Given the solution is in a short path
  When I build the iOS target
  Then the build succeeds without long-path errors

Scenario: Guard can be disabled with MSBuild property
  Given LongPathGuardEnabled is set to false
  When long path files are present
  Then the guard does not run and build proceeds normally

Scenario: Guard skips auto-generated scaffolding on a Release build
  Given the project sits in a deep folder without long source files
  And LongPathGuard.Maui package is referenced
  And an auto-generated scaffolding file with a long path exists under obj
  When I run dotnet build -f net9.0-ios -c Release
  Then the build succeeds without long-path errors

Scenario: Guard still flags a long native asset in the NuGet cache on a Release build
  Given the project sits in a deep folder without long source files
  And LongPathGuard.Maui package is referenced
  And a long native asset exists in the NuGet package cache
  When I run dotnet build -f net9.0-ios -c Release
  Then the build fails
  And the error clearly lists the offending long file paths
