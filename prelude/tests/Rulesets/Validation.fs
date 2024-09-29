﻿namespace Prelude.Tests.Scoring

open NUnit.Framework
open Prelude
open Prelude.Gameplay.RulesetsV2

module Validation =

    [<Test>]
    let SCJ4_Valid () =
        let ruleset = SC.create 4

        match RulesetV2.check ruleset with
        | Ok _ -> Assert.Pass()
        | Error reason -> Assert.Fail(reason)

    [<Test>]
    let SC_AllJudges_Valid () =

        for judge = 2 to 9 do
            let ruleset = SC.create judge

            match RulesetV2.check ruleset with
            | Ok _ -> printfn "%s: valid" ruleset.Name
            | Error reason -> Assert.Fail(reason)

        Assert.Pass()
    
    [<Test>]
    let OsuOd8_Valid () =
        let ruleset = OsuMania.create 8.0f (OsuMania.Mode.NoMod)

        match RulesetV2.check ruleset with
        | Ok _ -> Assert.Pass()
        | Error reason -> Assert.Fail(reason)

    [<Test>]
    let Osu_AllVariations_Valid () =

        for step = 0 to 20 do
            let od = 0.5f * float32 step

            for mode in [ OsuMania.Mode.NoMod; OsuMania.Mode.Easy; OsuMania.Mode.HardRock ] do
                
                let ruleset = OsuMania.create od mode

                match RulesetV2.check ruleset with
                | Ok _ -> printfn "%s: valid" ruleset.Name
                | Error reason -> Assert.Fail(reason)

        Assert.Pass()

    let VALID_RULESET = SC.create 4

    [<Test>]
    let Judgements_Required () =
        
        let invalid_ruleset = { VALID_RULESET with Judgements = [||] }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)
    
    [<Test>]
    let Judgements_WindowsSigns_EarlyWindow () =
        
        let invalid_ruleset = 
            { VALID_RULESET with 
                Judgements = 
                    [|
                        {
                            Name = "A"
                            Color = Color.White
                            TimingWindows = Some (25.0f<ms / rate>, 50.0f<ms / rate>)
                            BreaksCombo = false
                        }
                        {
                            Name = "B"
                            Color = Color.White
                            TimingWindows = Some (50.0f<ms / rate>, 100.0f<ms / rate>)
                            BreaksCombo = false
                        }
                    |]
                HoldMechanics = HoldMechanics.OnlyJudgeReleases 0
            }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)
    
    [<Test>]
    let Judgements_WindowsSigns_LateWindow () =
        
        let invalid_ruleset = 
            { VALID_RULESET with 
                Judgements = 
                    [|
                        {
                            Name = "A"
                            Color = Color.White
                            TimingWindows = Some (-50.0f<ms / rate>, -25.0f<ms / rate>)
                            BreaksCombo = false
                        }
                        {
                            Name = "B"
                            Color = Color.White
                            TimingWindows = Some (-100.0f<ms / rate>, -50.0f<ms / rate>)
                            BreaksCombo = false
                        }
                    |]
                HoldMechanics = HoldMechanics.OnlyJudgeReleases 0
            }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)
    
    [<Test>]
    let Judgements_WindowsOrdering () =
        
        let invalid_ruleset = 
            { VALID_RULESET with 
                Judgements = 
                    [|
                        {
                            Name = "A"
                            Color = Color.White
                            TimingWindows = Some (-50.0f<ms / rate>, 50.0f<ms / rate>)
                            BreaksCombo = false
                        }
                        {
                            Name = "B"
                            Color = Color.White
                            TimingWindows = Some (-25.0f<ms / rate>, -25.0f<ms / rate>)
                            BreaksCombo = false
                        }
                    |]
                HoldMechanics = HoldMechanics.OnlyJudgeReleases 0
            }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)
    
    [<Test>]
    let Judgements_InvalidWindowValues_NaN () =
        
        let invalid_ruleset = 
            { VALID_RULESET with 
                Judgements = 
                    [|
                        {
                            Name = "A"
                            Color = Color.White
                            TimingWindows = Some (nanf * 1.0f<ms / rate>, 50.0f<ms / rate>)
                            BreaksCombo = false
                        }
                        {
                            Name = "B"
                            Color = Color.White
                            TimingWindows = Some (-100.0f<ms / rate>, 100.0f<ms / rate>)
                            BreaksCombo = false
                        }
                    |]
                HoldMechanics = HoldMechanics.OnlyJudgeReleases 0
            }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)

    [<Test>]
    let Judgements_InvalidWindowValues_Infinity () =
        
        let invalid_ruleset = 
            { VALID_RULESET with 
                Judgements = 
                    [|
                        {
                            Name = "A"
                            Color = Color.White
                            TimingWindows = Some (-50.0f<ms / rate>, 50.0f<ms / rate>)
                            BreaksCombo = false
                        }
                        {
                            Name = "B"
                            Color = Color.White
                            TimingWindows = Some (-infinityf * 1.0f<ms / rate>, 100.0f<ms / rate>)
                            BreaksCombo = false
                        }
                    |]
                HoldMechanics = HoldMechanics.OnlyJudgeReleases 0
            }
        
        match RulesetV2.check invalid_ruleset with
        | Ok _ -> Assert.Fail()
        | Error reason -> Assert.Pass(reason)