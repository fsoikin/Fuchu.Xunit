# Fuchu.Xunit
Fuchu.Xunit is a binding for running tests written in Fuchu framework with xUnit runners.

![Travis CI build](https://travis-ci.org/erecruit/Fuchu.Xunit.svg?branch=master)

[Install as NuGet package](https://www.nuget.org/packages/Fuchu.Xunit/): `Install-Package Fuchu.Xunit`

# Getting started
To have your tests picked up by xUnit infrastructure, declare a function `() -> Fuchu.Test` and mark it with `[<FuchuTests>]`, e.g.:

```f#
let [<FuchuTests>] ``My component``() = 
  TestList [
    test "should be awesome" { 2+3 |> should equal 5 }
    test "should be cool, too" { 7 + 1 |> should equal 8 }
  ]
```

The above code will result in two tests visible to xUnit: "_My component should be awesome_" and "_My component should be cool, too_".

Test names are derived by concatenating labels from the tree root down to leaves, separating them with a single space, and prefixed by the name of the containing function. For example, the above two tests may be encoded with three levels of labels:

```f#
let [<FuchuTests>] ``My component``() = 
  testList "should be" 
    [ test "awesome" { 2+3 |> should equal 5 }
      test "cool, too" { 7 + 1 |> should equal 8 } ]
```

If several tests end up named identically, they will be visible to xUnit as a single "multi-run" test, similar to how xUnit's own parametrized tests (aka "theories") work. For example:

```f#
let [<FuchuTests>] ``My component``() = 
  TestList 
    [ test "abc" { ... }
      test "xyz" { ... }
      test "abc" { ... } ]
```

The above code will result in discovering two tests named "_My component abc_" and "_My component xyz_", but during test run the former will show two runs.
