namespace Fuchu.Xunit
open Xunit.Sdk
open Xunit

[<XunitTestCaseDiscoverer("Fuchu.Xunit.Discoverer","Fuchu.Xunit")>]
type FuchuTestsAttribute() =
  inherit FactAttribute()

type Discoverer(bus) =
  interface IXunitTestCaseDiscoverer with
    member x.Discover(discoveryOptions, testMethod, factAttribute) = 
      Impl.discover discoveryOptions testMethod bus |> Seq.cast<IXunitTestCase>