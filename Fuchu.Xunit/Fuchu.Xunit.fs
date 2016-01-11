module Fuchu.Xunit.Impl

open Xunit.Sdk
open Xunit.Abstractions
open System.Runtime.Versioning
open System
open Xunit
open System.Threading.Tasks
open Fuchu.Impl

let casesFromFuchu makeCase nestLabel = 
  let rec iter label = function
  | Fuchu.TestCase c -> [makeCase label c] |> Seq.ofList
  | Fuchu.TestList ts -> ts |> Seq.collect (iter label)
  | Fuchu.TestLabel (l, t) -> iter (nestLabel label l) t

  iter ""

let (<?>) x condition = if condition x then Some x else None
let (>>=) o f = Option.bind f o
let (>>-) o f = Option.map f o
let orElse backup = function | Some x -> x | None -> backup

let invokeMethodReturnFuchuTest (m: System.Reflection.MethodInfo) =
  let v = m.Invoke(null,[||]) 
  if v :? Fuchu.Test then Some (v :?> Fuchu.Test) else None

let fuchuTestsFromTestMethod (methd: ITestMethod) =
  let m = methd.Method.ToRuntimeMethod()
  if m <> null && m.IsStatic && m.GetParameters().Length = 0 && m.ReturnType = typeof<Fuchu.Test>
  then invokeMethodReturnFuchuTest m
  else None

let findTestByLabel maybeLabel tests =
  maybeLabel >>= fun label ->
    tests
    |> Seq.tryFind (fun (l, _) -> l = label)
    >>- fun (_, code) -> Fuchu.TestCase code

let runTest test = 
  match Fuchu.Impl.evalSeq test with
  | [res] -> Some res
  | _ ->  None

let (|Seconds|) (t: TimeSpan) = decimal t.TotalSeconds
let success time = RunSummary(Total=1, Time=time)
let fail time = RunSummary(Total=1, Failed=1, Time=time)
let skip = RunSummary(Total=1, Skipped=1)
let err time = RunSummary(Total=0, Failed=0, Skipped=0, Time=time)

let nestLabel parent child = sprintf "%s %s" parent child

let emptyIfNull sq = if sq = null then Seq.empty else sq :> _ seq
let maybeFirst = Seq.tryFind (fun _ -> true)
let ofType<'t> = Seq.cast<obj> >> Seq.filter (fun x -> x :? 't) >> Seq.cast<'t>

type TestCase(bus, methd, display, id) = 
  inherit XunitTestCase(bus, TestMethodDisplay.ClassAndMethod, methd, [|id|])
  new() = new TestCase(null, null, null, null)

  interface ITestCase with
    member x.DisplayName = display

  override this.RunAsync (_, bus:IMessageBus, _, _, cancel): Task<RunSummary> =
    let test = XunitTest(this, this.DisplayName)
    let post (m: #IMessageSinkMessage) = bus.QueueMessage <| m |> ignore
    let label = this.TestMethodArguments |> emptyIfNull |> ofType<string> |> maybeFirst

    let run = async { 
        post (TestStarting test)

        let res =
          fuchuTestsFromTestMethod this.TestMethod
          >>- casesFromFuchu (fun l code -> l, code) nestLabel
          >>= findTestByLabel label
          >>= runTest

        let summary =
          match res with
          | Some { Result = Failed err; Time = (Seconds s) } -> post <| TestFailed(test, s, err, null); fail s
          | Some { Result = Error ex; Time = (Seconds s) } -> post <| TestFailed(test, s, null, ex); err s
          | Some { Result = Ignored reason } -> post <| TestSkipped(test, reason); skip
          | Some { Result = Passed; Time = (Seconds s) } -> post <| TestPassed(test, s, null); success s
          | _ -> post <| TestSkipped(test, "Unknown error"); skip
        
        return summary
      }
    Async.StartAsTask( run )//, ?cancellationToken = cancel.Token)
    
let discover (options: ITestFrameworkDiscoveryOptions) (testMethod: ITestMethod) bus =
  let makeCase label _ = 
    let display = sprintf "%s.%s%s" testMethod.TestClass.Class.Name testMethod.Method.Name label
    new TestCase(bus, testMethod, display, label)

  fuchuTestsFromTestMethod testMethod
  >>- casesFromFuchu makeCase nestLabel
  |> orElse Seq.empty