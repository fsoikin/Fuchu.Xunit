#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake

let config = getBuildParamOrDefault "Config" "Debug"

Target "Build" <| fun _ ->
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

"Build" ==> "BuildTests" ==> "RunTests"

RunTargetOrDefault "Build"