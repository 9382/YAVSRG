﻿namespace Interlude.Web.Tests.Integration

open NUnit.Framework
open Interlude.Web.Shared.Requests
open System.Threading

module Charts =

    [<Test>]
    let Identify_KnownChart () =

        use done_signal = new AutoResetEvent(false)

        Charts.Identify.get (
            CRESCENT_MOON,
            Option.get
            >> fun (res: Charts.Identify.Response) ->
                printfn "%A" res.Info.Value
                done_signal.Set() |> ignore
        )

        Assert.IsTrue(done_signal.WaitOne(500))

    [<Test>]
    let Identify_UnknownChart () =

        use done_signal = new AutoResetEvent(false)

        Charts.Identify.get (
            "NOTAVALIDCHARTID",
            Option.get
            >> fun (res: Charts.Identify.Response) ->
                match res.Info with
                | Some info -> failwithf "Unexpected data %A" info
                | None -> ()

                done_signal.Set() |> ignore
        )

        Assert.IsTrue(done_signal.WaitOne(500))

    module Scores =

        [<Test>]
        let Leaderboard () =

            use done_signal = new AutoResetEvent(false)

            Charts.Scores.Leaderboard.get (
                CRESCENT_MOON,
                SCJ4,
                Option.get
                >> fun (res: Charts.Scores.Leaderboard.Response) ->
                    Assert.AreEqual(SCJ4, res.RulesetId)
                    printfn "%A" res
                    done_signal.Set() |> ignore
            )

            Assert.IsTrue(done_signal.WaitOne(500))

        [<Test>]
        let Leaderboard_SubstituteRuleset () =

            use done_signal = new AutoResetEvent(false)

            Charts.Scores.Leaderboard.get (
                CRESCENT_MOON,
                "Ruleset",
                Option.get
                >> fun (res: Charts.Scores.Leaderboard.Response) ->
                    Assert.AreEqual(SCJ4, res.RulesetId)
                    printfn "%A" res
                    done_signal.Set() |> ignore
            )

            Assert.IsTrue(done_signal.WaitOne(500))

        [<Test>]
        let Leaderboard_NotFound () =

            use done_signal = new AutoResetEvent(false)

            Charts.Scores.Leaderboard.get (
                "DoesntExist",
                SCJ4,
                function
                | Some _ -> Assert.Fail()
                | None -> done_signal.Set() |> ignore
            )

            Assert.IsTrue(done_signal.WaitOne(500))
