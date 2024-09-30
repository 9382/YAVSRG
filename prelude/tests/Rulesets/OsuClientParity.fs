﻿namespace Prelude.Tests.Rulesets

open System.IO
open NUnit.Framework
open Prelude
open Percyqaz.Common
open Prelude.Charts.Formats.osu
open Prelude.Charts.Conversions
open Prelude.Data.OsuClientInterop
open Prelude.Gameplay
open Prelude.Gameplay.RulesetsV2
open Prelude.Gameplay.ScoringV2
open Prelude.Tests.Rulesets

module OsuClientParity =

    let RULESET = OsuMania.create 8.0f OsuMania.NoMod

    let TEST_REPLAY_FILE = 
        OsuScoreDatabase_Score.ReadReplay "./Data/Lylcaruis - Cardboard Box - He He He [SPEEEDDD!!!] (2023-09-29) OsuMania.osr"

    let TEST_OSU_FILE_HASH =
        Beatmap.HashFromFile "./Data/Cardboard Box - He He He (DannyPX) [SPEEEDDD!!!].osu" |> expect

    let TEST_OSU_FILE =
        Beatmap.FromFile "./Data/Cardboard Box - He He He (DannyPX) [SPEEEDDD!!!].osu" |> expect

    let TEST_CHART =
        (Osu_To_Interlude.convert TEST_OSU_FILE { Config = ConversionOptions.Default; Source = "./Data/Cardboard Box - He He He (DannyPX) [SPEEEDDD!!!].osu" } |> expect).Chart

    [<Test>]
    let Replay_MatchesTestFile () =
        Assert.AreEqual(TEST_OSU_FILE_HASH, TEST_REPLAY_FILE.BeatmapHash)

    [<Test>]
    let Replay_RoundTrip() =
        let as_interlude_replay = OsuReplay.decode_replay (TEST_REPLAY_FILE, TEST_CHART.FirstNote, 1.0f<rate>)

        let back_as_osu_replay = OsuReplay.encode_replay as_interlude_replay TEST_CHART.FirstNote Mods.None TEST_OSU_FILE_HASH
        let return_as_interlude_replay = OsuReplay.decode_replay (back_as_osu_replay, TEST_CHART.FirstNote, 1.0f<rate>)

        printfn "%A" as_interlude_replay
        printfn "%A" return_as_interlude_replay

        Assert.AreEqual(as_interlude_replay, return_as_interlude_replay)

    [<Test>]
    let OsuManiaColumnLockMechanic_Replicate_1 () =

        let notes = 
            ChartBuilder(4)
                .Note(0.0f<ms>)
                .Note(108.0f<ms>)
                .Note(222.0f<ms>)
                .Build()

        let replay =
            ReplayBuilder()
                .KeyDownFor(-71.0f<ms>, 30.0f<ms>)
                .KeyDownFor(108.0f<ms> - 121.0f<ms>, 30.0f<ms>) // this input is getting eaten because it is before note 0 which has already been hit
                .KeyDownFor(222.0f<ms> - 125.0f<ms>, 30.0f<ms>)
                .KeyDownFor(222.0f<ms>, 30.0f<ms>)
                .Build()

        let event_processing = GameplayEventCollector(RULESET, 4, replay, notes, 1.0f<rate>)
        event_processing.Update Time.infinity

        Assert.AreEqual(
            [
                HIT(-71.0f<ms / rate>, false)
                HIT(-11.0f<ms / rate>, false)
                HIT(0.0f<ms / rate>, false)
            ],
            event_processing.Events |> Seq.map _.Action
        )

    [<Test>]
    let OsuRuleset_MatchesReplayJudgements_HeHeHeSample () =
        let as_interlude_replay = OsuReplay.decode_replay (TEST_REPLAY_FILE, TEST_CHART.FirstNote, 1.0f<rate>)

        let as_interlude_ruleset = OsuMania.create (float32 TEST_OSU_FILE.Difficulty.OverallDifficulty) OsuMania.Mode.NoMod

        let score = ScoreProcessor.run as_interlude_ruleset TEST_CHART.Keys (StoredReplayProvider(as_interlude_replay)) TEST_CHART.Notes 1.0f<rate>

        Assert.AreEqual(
            [|
                TEST_REPLAY_FILE.CountGeki
                TEST_REPLAY_FILE.Count300
                TEST_REPLAY_FILE.CountKatu
                TEST_REPLAY_FILE.Count100
                TEST_REPLAY_FILE.Count50
                TEST_REPLAY_FILE.CountMiss
            |],
            score.JudgementCounts
        )

    [<Test>]
    let OsuRuleset_MatchesExpectedAccuracy_HeHeHeSample () =
        let as_interlude_replay = OsuReplay.decode_replay (TEST_REPLAY_FILE, TEST_CHART.FirstNote, 1.0f<rate>)
        let as_interlude_ruleset = OsuMania.create (float32 TEST_OSU_FILE.Difficulty.OverallDifficulty) OsuMania.Mode.NoMod
        let score = ScoreProcessor.run as_interlude_ruleset TEST_CHART.Keys (StoredReplayProvider(as_interlude_replay)) TEST_CHART.Notes 1.0f<rate>

        printfn "%.2f%%" (score.Accuracy * 100.0)

        Assert.AreEqual(92.15, System.Math.Round(100.0 * score.Accuracy, 2))

    [<Test>]
    let OsuRuleset_MatchesGosuMemoryDeltas_HeHeHeSample () =
        let as_interlude_replay = OsuReplay.decode_replay (TEST_REPLAY_FILE, TEST_CHART.FirstNote, 1.0f<rate>)

        let as_interlude_ruleset = OsuMania.create (float32 TEST_OSU_FILE.Difficulty.OverallDifficulty) OsuMania.Mode.NoMod

        let score = ScoringEventCollector(as_interlude_ruleset, TEST_CHART.Keys, (StoredReplayProvider(as_interlude_replay)), TEST_CHART.Notes, 1.0f<rate>)
        score.Update Time.infinity

        let event_deltas =
            score.Events
            |> Seq.map (_.Action)
            |> Seq.choose (
                function
                | Hit d -> if not d.Missed then Some d.Delta else None
                | Hold d -> if not d.Missed then Some d.Delta else None
                | Release d -> if not d.Missed then Some d.Delta else None
                | _ -> None
            )
            |> Seq.map (float32 >> int)

        // Obtained with GosuMemory
        let EXPECTED_DATA =
            [13; -1; 1; -2; 7; -14; -9; -5; 2; 1; 0; 2; -5; -2; 2; 6; -12; 6; -11; 3; -3; -8; -4; -13; -12; -2; -13; 0; -14; 4; -4; -4; -3; 0; -10; -9; -7; -7; -9; -13; -7; -12; -17; -8; -3; 3; 0; 8; -12; 4; 6; -3; -4; -2; 2; -5; -5; 0; 3; 2; -8; 9; -9; 4; 5; 3; 7; -4; -4; -1; -9; 3; -2; 6; 0; 3; -3; -10; 8; -3; 4; -1; 3; 10; 3; -1; 4; 5; 5; 7; -5; -14; -13; -1; -14; -4; 0; -5; -5; 3; -4; 2; -1; -5; -17; -4; -9; -18; -14; -2; -6; -37; 1; 2; 6; -16; 3; 14; 3; 11; -14; 0; 5; -5; 0; -15; -7; -4; -1; 0; 17; -13; -12; -3; -3; -19; -5; 16; 5; -5; -4; 1; -13; -56; -7; -5; -4; -10; -13; -7; -6; 7; -6; -8; 3; 5; 8; -15; 6; 3; -4; -3; 8; -31; -26; -2; -14; -2; -1; 2; 12; -6; 4; 12; 12; -14; -19; -14; -8; -1; 0; -3; 4; 10; 2; -14; 1; 2; -5; -3; -8; -6; 13; 22; 11; -32; 5; 12; -12; -23; -4; -4; -6; 27; -4; 5; -18; -13; -4; -10; -7; -14; -10; -15; -1; 10; -16; -13; -9; -10; -11; -10; -2; -9; 5; -11; -6; -2; -20; -10; 1; -4; -25; -4; -28; -4; -1; 17; -11; -21; -9; -40; -18; -2; 11; -4; -9; -3; 0; 0; 2; 16; -12; -9; -13; -4; -6; -3; 4; 9; 19; 67; -9; 17; -10; 0; 3; -10; -17; 39; -13; 19; 5; 13; 3; -21; -3; 2; 5; 5; 12; 1; 26; -4; 23; -11; -7; -1; -2; -11; 1; 2; -3; -27; -10; -17; 1; -28; -33; -13; 1; 0; -7; -22; -3; 14; -17; -1; 18; -20; 13; -16; -10; 8; 44; -1; 22; -4; -2; 12; -3; 0; -15; 2; 9; 10; -4; -8; -19; 3; 6; 8; -6; 4; -19; 11; -11; -10; 8; -5; 1; 4; -24; 3; 14; 15; -5; 2; 10; -19; -7; -29; -13; -13; -10; -12; -7; -23; -22; -22; -4; -18; -17; -22; -13; -10; -5; 0; -4; 2; 1; 25; -8; -1; 9; -13; -21; -5; 17; -20; -4; 6; -17; 0; -18; -21; -6; -1; -13; -8; -12; -11; -23; -10; -46; -14; 17; 8; -19; 20; 12; 20; 2; 34; 5; -8; 23; 6; 41; -11; 26; -1; 34; 15; 21; 26; 34; 1; -5; -27; 19; -6; 1; -18; -11; 27; -10; -8; -4; 9; -5; 2; 5; 9; -16; 2; -1; 5; 3; 5; 9; -11; 15; -10; -11; -6; -7; -19; 3; -50; 12; -38; -35; -4; -20; -25; -3; -9; 10; 4; -4; 12; -7; 26; 0; 24; -21; -6; -18; 16; -21; -13; 33; -26; -17; -19; -40; 6; -33; 16; -3; -37; -27; 12; -12; -42; -41; -3; -30; 6; -26; 13; 15; -15; 9; 15; -11; 39; 2; 27; 2; 53; 33; 8; -4; 42; 5; -28; -6; 1; -40; 1; -12; 2; -10; 44; 29; -10; 26; -7; -17; 18; 14; 14; 11; 61; 4; 3; 56; 2; 3; 47; 15; -4; -3; 34; -27; 25; -48; -35; -23; 29; 3; 55; -6; 0; 6; 59; -38; 11; -30; 18; -4; -37; 7; -13; 41; 3; -12; 2; -5; 36; -52; -4; -46; 1; -51; 0; 0; -33; -51; 2; -32; -37; 12; -34; 16; -36; -63; -17; -16; -18; -49; -4; -42; -26; 0; 3; -28; 18; 9; 10; -57; 32; 52; -34; -30; 54; 21; 52; 15; -25; -14; 8; -28; 53; 2; 43; -19; 10; 25; -14; -26; 21; -36; 99; 31; 75; 3; -33; 14; 55; -56; -43; -65; 15; 57; -81; 6; -68; -22; -5; -75; -19; -71; -54; -11; -121; -35; 34; -37; -149; 55; 17; -17; -44; 43; 4; -30; -47; 2; 42; 22; 12; -58; 35; 45; 6; -51; 29; 34; 10; -50; 19; 12; -91; 0; 2; 47; -143; -6; -82; 47; -7; -77; 49; -60; -6; -125; 5; -104; 5; 36; -15; 40; 17; -19; -17; 19; 17; -18; 12; -48; 16; 44; 40; 16; 41; -52; 30; 71; 30; -78; 67; 28; 27; -34; 70; 4; -4; -23; -5; 5; -9; -24; 6; 10; -17; -49; 9; -15; -28; -50; 27; -8; -32; -99; 29; -23; -10; 2; 2; -20; -2; -34; -31; -54; 9; -22; -50; -8; -9; -28; 25; 8; -12; 18; -8; -10; -46; -8; 5; -21; 19; 0; -31; -2; 6; 16; 17; -5; 3; 13; 25; 44; 45; 30; 79; 24; 29; 23; 75; 17; 21; 68; 39; 15; 59; 28; -3; -2; 52; 18; -13; 35; 14; -24; 25; 82; 7; -32; 14; -58; 2; -72; 58; -4; -39; -70; 0; -30; -58; -11; 7; -51; -6; 0; -29; -21; -10; 34; -15; 27; 7; -24; -20; -2; 41; -23; -17; 25; 8; -26; -57; 24; 6; 60; -2; 39; -38; -62; 28; -12; 69; 25; -13; -40; -29; -8; 32; -31; -100; 19; -14; -74; -1; 45; 3; -33; 16; -19; 0; -12; -41; -28; -5; -80; -32; 18; 5; 0; -20; 13; -61; 10; -67; 56; 10; -56; 29; -89; 6; -111; 22; 47; -8; 8; -56; 26; -53; -51; 1; 48; -57; -3; 39; -33; -11; 30; -73; -28; -44; 11; -7; -42; -69; 12; 4; -105; 16; -8; 16; 66; -131; -42; 2; -40; 39; 8; 38; 2; -28; -8; 41; 14; 6; 45; 13; 6; -22; 14; 35; 17; -3; 31; -2; 16; -9; -13; -4; 2; -15; 12; -4; -30; -49; -7; -19; -39; -18; -21; -10; -6; -20; 6; -5; -12; -1; -2; -46; -10; -23; -16; 16; -2; -4; 16; 32; 9; 18; 17; -18; -14; -18; 17; -19; 4; 16; 11; -18; -2; 4; 1; 13; -1; -1; 19; -14; 7; -6; -10; 3; 0; 6; -7; -9; -1; -9; -15; -15; -9; -4; -1; -15; 3; -10; 2; -38; -3; -13; -14; 1; -35; 9; -27; -31; 27; -18; -29; -23; -19; -11; 12; -17; 0; -12; 14; 25; -13; -9; -1; 0; 18; 3; 21; -16; -7; 1; -5; -14; -18; -14; -13; -4; -18; -15; -6; -4; -29; -4; 4; 6; 0; 14; -21; -25; 9; 7; -8; 0; -16; 4; -1; 12; 18; -28; 4; -13; -3; -23; -8; 3; -11; 6; -4; 7; -42; -11; -15; -30; 0; -20; -65; 8; -27; 20; -14; 5; 9; -53; -2; -2; -9; 1; 11; -15; -7; -4; -3; -9; -20; 2; -1; -10; -20; -7; -27; 0; 0; -5; 9; 9; 12; -4; -6; -6; 5; -17; -11; 1; -13; -12; 9; 14; -11; -11; 8; 6; -10; -1; -1; -2; 0; -57; 3; 0; 12; 0; 0; 8; 17; -35; -7; -1; 2; -13; -4; -20; -9; -6; 19; -9; -3; -16; -10; -7; -12; -18; -6; 3; -5; -8; -1; -58; -4; 3; -3; -1; -3; -1; -58; 4; -7; -16; -16; -13; 1; -31; -8; -7; 24; -1; 4; -6; 1; -8; -4; -25; -7; -3; -14; -4; -11; -10; -7; -13; 0; -14; -1; 17; 3; -28; 0; -19; -14; -6; -5; 1; -21; -16; 5; -15; -4; -13; -9; -11; -10; -18; -2; -1; -6; -1; -34; -45; -4; -12; -6; -11; -28; -19; -13; -21; -11; -18; 3; -27; 6; -14; -3; 5; -7; -3; -6; -10; -8; -9; -19; -14; -13; -8; -17; -11; -7; -10; 5; -17; 43; 22; -15; -11; -4; -18; -12; -11; -3; 3; -6; 17; -14; 3; -15; -22; -21; -22; -19; -1; -20; -17; -34; -31; -14; -36; -13; -39; 12; -40; 8; -1; 8; 10; -25; 1; -16; -29; -2; -1; 73; -29; 3; -15; -18; -10; -13; 3; -24; -1; -13; -19; 2; -26; -14; -17; -18; -9; -22; -11; -13; -7; -4; -19; -6; -30; -8; -5; -4; 1; -6; -7; -3; -27; -16; -25; -18; -17; -5; -20; -4; -15; -8; 27; -17; 2; -25; -18; 6; -26; 3; -14; 4; -28; -4; -29; -12; -7; -33; -6; -17; -12; -5; -7; 5; -7; -16; -5; 31; -11; 30; -12; 0; -18; -14; -22; -15; -9; -35; 13; -27; -17; -7; -3; -12; -9; -17; -15; -19; -14; 0; -5; 1; -25; -9; -11; -18; -2; 3; -14; -15; -17; -14; -34; -15; -24; -14; -5; 5; -4; -1; -19; -11; -5; -7; -14; -5; 17; -10; -9; -7; -17; -6; -6; -16; -11; -12; -11; -3; 2; -10; -18; -10; -4; -9; 9; -33; -12; 17; -10; 22; -4; 6; -1; 4; -4; -1; 7; 0; -9; 6; -21; -15; 7; -11; -2; -8; -46; 23; 7; 14; -14; 4; 6; -9; -2; -23; -6; -29; 11; -21; -11; 3; -21; 2; -14; -36; -13; 4; -12; 8; -17; 8; 35; -10; 15; -16; -10; 23; -22; 23; -16; 16; -13; 6; -18; 3; 8; -28; -4; -25; 10; 19; -16; 7; -9; -25; -9; 45; -11; 40; -15; -10; -21; 42; 27; -9; 5; 7; -17; -6; -23; -18; -18; -17; -16; -21; 0; -28; -2; -33; 28; -19; -36; -31; 12; -45; 4; -34; 6; -26; 10; -35; -5; -28; 10; -26; 15; -23; 15; -18; 17; 8; 14; -9; 31; 6; 17; -2; 48; -12; 32; 34; -30; 13; -38; 12; 52; 14; 35; -39; 9; 19; -14; -40; 7; 23; -9; -39; -111; 10; 12; 30; 1; -140; -12; 19; -15; -15; -19; 23; -53; 15; 60; -3; 36; -12; 32; 13; 20; 28; 2; 8; 11; 26; 0; 10; -22; -18; -46; -20; 20; -17; -86; 25; -8; 22; 25; 30; 7; 26; -11; -3; -32; -1; 38; -25; 25; 8; -19; 34; 26; -5; 22; -17; -9; -43; 2; 15; 21; -17; 17; -14; -33; 19; 31; -28; 12; 9; 12; 23; -2; 42; -17; -13; 29; 77; 16; -20; -8; 72; 55; -18; 33; -39; 41; -38; 15; 40; -36; 11; 19; -10; -47; -43; 9; -49; -8; 28; -4; 37; 40; 29; -5; -47; 37; 27; -45; 36; 5; 52; 35; -42; 6; 23; 24; 7; -25; 10; 14; -19; 1; 5; -36; 16; -14; -1; 19; 20; -15; -4; 19; 27; 31; 46; 13; 17; 23; 38; 3; 15; 24; -10; 8; 13; -37; 15; -1; 0; -70; -3; 49; -128; -6; -7; 50; -126; -3; 2; 9; 34; -151; -7; -65; 38; 7; -93; 31; 43; -141; 23; 43; 50; 53; 34; -30; 33; -75; 32; -74; 17; -93; 60; -127; -70; 15; 50; -30; -68; -4; 7; 4; -36; -1; -1; -22; 20; -41; 10; -49; -8; 8; -25; -8; -2; 20; -19; -20; 1; -24; 19; -53; -7; -15; -33; 14; 80; -16; -32; 13; -17; -17; 22; -35; 5; -7; -34; -14; -12; 3; -24; 4; -34; -13; 9; 14; -14; 35; 4; 10; -53; 46; 45; -20; -38; -28; -57; 30; 19; -99; -11; -7; 64; -7; -86; 52; -102; -19; -4; 50; -139; -13; 48; -17; 27; 29; 17; -9; 11; 15; 35; 39; 9; 58; 23; -45; -36; 55; 37; -33; -31; 53; 14; -24; -21; -10; 21; -15; -44; -62; -19; 22; -33; 8; -22; -41; -37; 25; -26; 16; 11; -60; -3; 54; -50; -5; 46; 57; -54; 34; -2; 21; -7; -29; -29; 14; -15; -18; -96; 22; -51; 11; -61; 25; -86; 26; -90; -7; -113; 40; -150; 41; 33; 37; -20; -106; 14; 38; -152; 47; 34; -26; 41; 77; -59; 27; 77; -110; 12; -26; 83; 48; -30; -149; 80; 52; -9; 34; -8; -7; 33; 10; -5; -3; 42; 13; 7; -30; 4; -27; 16; -23; 7; -62; 22; 10; -81; 9; 12; 55; -134; 5; -31; 33; -75; -19; 21; -12; -72; -14; 3; 7; -105; -17; 42; -16; -26; -15; -7; 28; -8; -19; 44; -34; -4; 22; -14; 22; -9; -33; 9; -8; 30; -29; 1; -5; 34; -18; 14; -4; 29; -18; 13; 22; 27; 37; 6; 21; 16; 18; 61; 29; -3; 2; 57; -52; -2; 7; 8; -20; -58; -14; -21; -26; 5; -32; 94; -1; -30; -29; -7; -5; 11; -20; -13; -27; 37; -10; -8; -3; -30; -25; -20; -1; -72; 20; -54; -64; 17; -49; 39; -72; 38; 21; -85; 15; 66; -130; 4; 16; -23; 8; -4; 30; -9; -8; 14; -5; 36; -55; -6; -33; 12; -42; 5; 10; -25; -56; 17; -19; -2; -36; -71; -10; 32; -76; -108; -45; 0; 31; -21; -55; -69; -141; -34; -33; -30; -96; -43; -29; 1; -148; 16; 58; -19; 39; -72; 22; -11; 32; 21; -19; 33; 19; -20; 31; 48; -28; 13; 28; -33; -66; 17; 34; 7; -70; 72; -77; 4; -111; 72; 39; 51; 14; -21; 11; 13; 54; -17; 41; 20; 64; 27; 10; 16; -1; 43; 2; -8; 36; -2; 13; 20; -15; -7; 5; 14; -14; 43; 13; -53; 34; -37; 56; -47; 27; -33; 32; -82; 30; -35; -84; 33; -73; 10; -83; -103; -33; -69; 74; -56; -82; -57; 61; -98; -97; 31; -78; -159; -63; 65; -61; -138; 72; 8; -34; 65; -39; -109; 72; 15; -39; -4; -55; -13; -8; 2; -70; -20; -12; 1; -98; -8; -53; 52; -87; -49; 43; -100; 25; 31; -100; 22; 76; 44; -27; -21; 27; -39; 5; 20; -54; 2; -111; 18; -91; -3; -141; 57; -39; 5; -13; 37; -36; 18; -37; 35; -38; 1; 90; -14; -32; -33; -32; 21; -66; 18; 62; 3; 20; -16; 2; 12; 4; -20; -16; 34; -38; 15; -42; -66; 23; 24; 28; 16; -89; 18; 19; 60; -93; 10; -96; 69; 5; 5; 73; -5; 56; 23; -13; 45; 97; -19; 27; 35; -30; 38; -41; 75; -24; 38; -40; -46; 25; -36; 24; 1; 3; -114; -10; 42; -5; 33; 6; -14; -122; -5; -15; -27; 23; -26; -23; 11; 0; -35; -2; 13; 39; 41; 30; 1; 25; 35; -8; 45; 23; -6; 35; 15; 18; -16; 30; 12; 14; -60; 39; 1; -3; -119; -7; 46; -51; 9; -17; -50; 5; -23; 6; 10; -30; 52; 38; 16; -19; -87; 37; -31; 10; 43; -21; 19; 46; 11; 12; -61; -3; -2; 83; -108; 28; -45; 94; -89; 24; -44; 34; -75; 32; -39; 39; -71; 30; 7; -38; 10; 45; 13; -82; 4; 42; -9; 31; 42; -20; 28; -12; 44; -48; -6; -6; -10; -23; -14; -49; 57; -14; -11; -51; 36; -117; -50; -47; 34; -100; -48; -52; -30; -54; -97; -22; -10; 10; -20; 27; 16; -12; 9; -31; -1; -24; -9; -10; -5]

        Assert.AreEqual(EXPECTED_DATA, event_deltas)

    [<Test>]
    let OsuRuleset_MatchesGosuMemoryDeltas_LongjackSample () =
        let replay =
            OsuScoreDatabase_Score.ReadReplay @"C:\Users\percy\AppData\Local\osu!\Replays/Percyqaz - Hirose Kohmi - Promise (Get Down) [thanks for holding] (2024-09-22) OsuMania.osr"

        let notes =
            let cb = ChartBuilder(4)
            for i = 0 to 63 do
                cb.Note(float32 i * 125.0f<ms>) |> ignore
            cb.Build()

        let as_interlude_replay = OsuReplay.decode_replay (replay, 0.0f<ms>, 1.0f<rate>)

        let score = ScoringEventCollector(OsuMania.create 10.0f OsuMania.Mode.NoMod, 4, (StoredReplayProvider(as_interlude_replay)), notes, 1.0f<rate>)
        score.Update Time.infinity

        let event_deltas =
            score.Events
            |> Seq.map (_.Action)
            |> Seq.choose (
                function
                | Hit d -> if not d.Missed then Some d.Delta else None
                | Hold d -> if not d.Missed then Some d.Delta else None
                | Release d -> if not d.Missed then Some d.Delta else None
                | _ -> None
            )
            |> Seq.map (float32 >> int)

        let EXPECTED_DATA =
            [-19; -41; -38; -35; -17; -14; -11; -9; -6; -4; 27; 29; 31; 34; 37; 23; 26; 44; 79; 93; 81; 83; 85; -21; -18; -16; 3; 21; 41; 55; 56; 75; 78; -28; -9; -6; 12; -2; 12; 15; 33; 36; 38; 58; 60; 62; 65; 67; 83; -24; -22; -20; 0; 2; 21; 24; 27; 56; 43; 62; 63]

        Assert.AreEqual(EXPECTED_DATA, event_deltas)

    let OD10_RULESET = OsuMania.create 10.0f OsuMania.NoMod

    [<Test>]
    let OsuManiaColumnLockMechanic_Replicate_2_OD10 () =
        let notes = 
            ChartBuilder(4)
                .Note(0.0f<ms>)
                .Note(125.0f<ms>)
                .Note(250.0f<ms>)
                .Note(375.0f<ms>)
                .Build()

        let replay =
            ReplayBuilder()
                .KeyDownFor(78.0f<ms>, 30.0f<ms>)
                .KeyDownFor(222.0f<ms>, 30.0f<ms>)
                .KeyDownFor(366.0f<ms>, 30.0f<ms>)
                .Build()
        
        let event_processing = GameplayEventCollector(OD10_RULESET, 4, replay, notes, 1.0f<rate>)
        event_processing.Update Time.infinity

        Assert.AreEqual(
            [
                HIT(78.0f<ms / rate>, false)
                HIT(-28.0f<ms / rate>, false)
                HIT(96.5f<ms / rate>, true)
                HIT(-9.0f<ms / rate>, false)
            ],
            event_processing.Events |> Seq.map _.Action
        )

    [<Test>]
    let OsuManiaColumnLockMechanic_Replicate_2_OD8 () =
        let notes = 
            ChartBuilder(4)
                .Note(0.0f<ms>)
                .Note(125.0f<ms>)
                .Note(250.0f<ms>)
                .Note(375.0f<ms>)
                .Build()

        let replay =
            ReplayBuilder()
                .KeyDownFor(78.0f<ms>, 30.0f<ms>)
                .KeyDownFor(228.0f<ms>, 30.0f<ms>)
                .KeyDownFor(366.0f<ms>, 30.0f<ms>)
                .Build()
        
        let event_processing = GameplayEventCollector(RULESET, 4, replay, notes, 1.0f<rate>)
        event_processing.Update Time.infinity

        Assert.AreEqual(
            [
                HIT(78.0f<ms / rate>, false)
                HIT(-22.0f<ms / rate>, false)
                HIT(102.5f<ms / rate>, true)
                HIT(-9.0f<ms / rate>, false)
            ],
            event_processing.Events |> Seq.map _.Action
        )

    [<Test>]
    let OsuManiaColumnLockMechanic_Replicate_2_OD8_AltCase () =
        let notes = 
            ChartBuilder(4)
                .Note(0.0f<ms>)
                .Note(125.0f<ms>)
                .Note(250.0f<ms>)
                .Note(375.0f<ms>)
                .Build()

        let replay =
            ReplayBuilder()
                .KeyDownFor(78.0f<ms>, 30.0f<ms>)
                .KeyDownFor(227.0f<ms>, 30.0f<ms>)
                .KeyDownFor(366.0f<ms>, 30.0f<ms>)
                .Build()
        
        let event_processing = GameplayEventCollector(RULESET, 4, replay, notes, 1.0f<rate>)
        event_processing.Update Time.infinity

        Assert.AreEqual(
            [
                HIT(78.0f<ms / rate>, false)
                HIT(102.0f<ms / rate>, false)
                HIT(102.5f<ms / rate>, true)
                HIT(-9.0f<ms / rate>, false)
            ],
            event_processing.Events |> Seq.map _.Action
        )