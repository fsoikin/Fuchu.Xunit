module Fuchu.Xunit.Impl
open Fuchu.Impl
open System
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open Xunit.Sdk
open FSharpx.Option

[<AutoOpen>]
module Utils =
  let orElse v = function | Some x -> x | None -> v
  let emptyIfNull sq = if sq = null then Seq.empty else sq :> _ seq
  let maybeFirst = Seq.tryFind (fun _ -> true)
  let ofType<'t> = Seq.cast<obj> >> Seq.filter (fun x -> x :? 't) >> Seq.cast<'t>

[<AutoOpen>]
module Discovery =
  /// Unfolds the tree of test cases to a flat list, using the given `makeCase` function to
  /// handle leaf case, and using `nestLabel` function to fold labels.
  let casesFromFuchu makeCase nestLabel = 
    let rec iter label = function
    | Fuchu.TestCase c -> [makeCase label c] |> Seq.ofList
    | Fuchu.TestList ts -> ts |> Seq.collect (iter label)
    | Fuchu.TestLabel (l, t) -> iter (nestLabel label l) t

    iter ""

  /// Provided the given method is of the right signature (i.e. unit -> Fuchu.Test), invokes it to obtain the Fuchu tree of tests.
  /// NOTE: we can't use Fuchu's own `testFromMember` because it has backed-in requirement that the method is decorated with [<Test>].
  let fuchuTestsFromTestMethod (methd: ITestMethod) =
    let m = methd.Method.ToRuntimeMethod()
    if m <> null && m.IsStatic && m.GetParameters().Length = 0 && m.ReturnType = typeof<Fuchu.Test> then
      let v = m.Invoke(null,[||]) 
      if v :? Fuchu.Test then Some (v :?> Fuchu.Test) else None
    else 
      None

  /// Given "parent" label (i.e. from parent test tree node) and "child" one, produces effective compound label
  let nestLabel parent child = sprintf "%s %s" parent child

[<AutoOpen>]
module Run =
  /// When given a test tree consisting of a single node, runs that node and returns result.
  /// Otherwise, returns None.
  let runTests tests = Fuchu.Impl.evalSeq tests

  let (|Seconds|) (t: TimeSpan) = decimal t.TotalSeconds

  let fuchuResultToXunitResult test result : (IMessageSinkMessage * RunSummary) = 
    match result with
    | { Result = Failed err; Time = (Seconds s) } -> TestFailed(test, s, err, null) :> _, RunSummary(Total=1, Failed=1, Time=s)
    | { Result = Error ex; Time = (Seconds s) } -> TestFailed(test, s, null, ex) :> _, RunSummary(Total=0, Failed=0, Time=s)
    | { Result = Ignored reason } -> TestSkipped(test, reason) :> _, RunSummary(Total=1, Skipped=1)
    | { Result = Passed; Time = (Seconds s) } -> TestPassed(test, s, null) :> _, RunSummary(Total=1, Time=s)

/// Implementation of Xunit test case
type TestCase(bus, methd, display, label) = 

  // NOTE: we're passing the test label as "test method arguments". We have to do this,
  // because otherwise all our tests cases will look identical to Xunit, because they
  // come from the same test method. But if we pass the label as "arguments", Xunit will
  // include it in the test case identity, thus making these tests distinct.
  inherit XunitTestCase(bus, TestMethodDisplay.ClassAndMethod, methd, [|label|])

  new() = new TestCase(null, null, null, null)

  interface ITestCase with
    member x.DisplayName = display

  override this.RunAsync (_, bus:IMessageBus, _, _, _): Task<RunSummary> =
    let test = XunitTest(this, this.DisplayName)
    let post (m: #IMessageSinkMessage) = bus.QueueMessage m |> ignore

    let run = async { 
        post (TestStarting test)

        let maybeSummary = 
          maybe {
            let! tests = fuchuTestsFromTestMethod this.TestMethod 
            let flatList = tests |> casesFromFuchu (fun l code -> l, code) nestLabel 
            let! label = this.TestMethodArguments |> emptyIfNull |> ofType<string> |> maybeFirst

            let results = 
              flatList
              |> Seq.filter (fst >> (=) label) // Find "our" tests
              |> Seq.collect (snd >> Fuchu.TestCase >> Fuchu.Impl.evalSeq) // Rewrap them in Fuchu TestCase, have Fuchu run them
              |> Seq.map (fuchuResultToXunitResult test) // Convert Fuchu results to Xunit message + Xunit summary
              |> Seq.toList // Persist the resulting list, don't do all the above twice

            // Post messages to let Xunit know of what's happening
            results |> Seq.iter (fst >> post)

            // Fold all summaries in (yeah, this is kinda ugly, but this is the API that Xunit offers)
            let summary = RunSummary()
            results |> Seq.iter (snd >> summary.Aggregate)

            // Return the summary
            return summary
          }

        let summary =
          match maybeSummary with
          | Some s -> s
          | None -> post <| TestSkipped(test, "Unknown error"); RunSummary(Total=1, Skipped=1) // TODO: better reporting
        
        return summary
      }
    Async.StartAsTask( run )//, ?cancellationToken = cancel.Token)
    
/// Produces list of Fuchu tests associated with given method, wrapping them up as Xunit test cases.
let discover (testMethod: ITestMethod) bus =
  let makeCase label = 
    let display = sprintf "%s.%s%s" testMethod.TestClass.Class.Name testMethod.Method.Name label
    new TestCase(bus, testMethod, display, label)

  let justLabel label _ = label

  fuchuTestsFromTestMethod testMethod
  |> Option.map (
    casesFromFuchu justLabel nestLabel
    >> Seq.distinct 
    >> Seq.map makeCase )
  |> orElse Seq.empty