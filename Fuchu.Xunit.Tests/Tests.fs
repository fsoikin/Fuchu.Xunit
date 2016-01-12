module Fuchu.Xunit.Tests
open Fuchu
open FsUnit.Xunit

let [<FuchuTests>] ``My component``() = 
  TestList [
    test "should be awesome" { 2+3 |> should equal 5 }
    test "should be cool, too" { 7 + 1 |> should equal 5 }

    testList "plus" <| seq { 
      for i in 1..5 do 
        yield test (sprintf "number %d" i) {
          i-i |> should equal 0 
        } 
    }
  ]