namespace Fuchu.Xunit
open Xunit.Sdk
open Xunit

/// This attribute marks functions containing Fuchu tests.
/// The function must have a specific signature of `unit -> Fuchu.Test`.
[<XunitTestCaseDiscoverer("Fuchu.Xunit.Discoverer","Fuchu.Xunit")>]
type FuchuTestsAttribute() =
  inherit FactAttribute()

type Discoverer(bus) =
  interface IXunitTestCaseDiscoverer with
    member x.Discover(_, testMethod, _) = 
      Impl.discover testMethod bus |> Seq.cast<IXunitTestCase>