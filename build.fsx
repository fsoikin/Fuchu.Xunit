#r @"packages/build/FAKE/tools/FakeLib.dll"
#load "./build/Publish.fsx"
#load "./build/PatchVersion.fsx"
open Fake

let config = getBuildParamOrDefault "Config" "Debug"

Target "Build" <| fun _ ->
  PatchVersion.patchVersion "./Fuchu.Xunit/AssemblyInfo.fs"
  ["Fuchu.Xunit/Fuchu.Xunit.fsproj"]
  |> MSBuild "" "Build" ["Configuration", config]
  |> Log "Build: "

Target "BuildTests" <| fun _ ->
  ["Fuchu.Xunit.Tests/Fuchu.Xunit.Tests.fsproj"]
  |> MSBuild "" "Build" ["Configuration", config]
  |> Log "Build tests: "

Target "Clean" <| fun _ ->
  !! "**/*.fsproj"
  |> MSBuild "" "Clean" ["Configuration", config]
  |> Log "Build tests: "

Target "RunTests" <| fun _ ->
  [sprintf "Fuchu.Xunit.Tests/bin/%s/Fuchu.Xunit.Tests.dll" config]
  |> Fake.Testing.XUnit2.xUnit2 id

Target "Publish" <| Publish.publishPackage config "./Fuchu.Xunit/paket.template"

"Build" ==> "BuildTests" ==> "RunTests" ==> "Publish"

RunTargetOrDefault "Build"