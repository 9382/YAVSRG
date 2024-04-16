﻿namespace Interlude.Features.Play


open System.IO
open Percyqaz.Common
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude
open Prelude.Gameplay
open Prelude.Charts.Processing
open Interlude.Options
open Interlude.Content
open Interlude.UI
open Interlude.Web.Shared
open Interlude.Features
open Interlude.Features.Pacemaker
open Interlude.Features.Gameplay.Chart
open Interlude.Features.Stats
open Interlude.Features.Online
open Interlude.Features.Score
open Interlude.Features.Play.HUD

module PlayScreenMultiplayer =

    let multiplayer_screen (info: LoadedChartInfo, lobby: Lobby) =

        let ruleset = Rulesets.current
        let first_note = info.WithMods.FirstNote
        let liveplay = LiveReplayProvider first_note

        let scoring =
            Metrics.create ruleset info.WithMods.Keys liveplay info.WithMods.Notes Gameplay.rate.Value

        let binds = options.GameplayBinds.[info.WithMods.Keys - 3]
        let mutable key_state = 0us
        let mutable packet_count = 0

        lobby.StartPlaying()
        lobby.AddReplayInfo(
            Network.credentials.Username,
            { 
                Replay = liveplay
                ScoreMetric = scoring
                GetScoreInfo = fun () ->
                    if not (liveplay :> IReplayProvider).Finished then
                        liveplay.Finish()

                    scoring.Update Time.infinity

                    let replay_data = (liveplay :> IReplayProvider).GetFullReplay()

                    {
                        CachedChart = info.CacheInfo
                        Chart = info.Chart
                        WithMods = info.WithMods

                        PlayedBy = Data.ScorePlayedBy.You
                        TimePlayed = Timestamp.now ()
                        Rate = Gameplay.rate.Value

                        Replay = replay_data
                        Scoring = scoring
                        Lamp = Lamp.calculate scoring.Ruleset.Grading.Lamps scoring.State
                        Grade = Grade.calculate scoring.Ruleset.Grading.Grades scoring.State

                        Rating = info.Rating
                        Patterns = info.Patterns
                        Physical = Performance.calculate info.Rating info.WithMods.Keys scoring |> fst

                        ImportedFromOsu = false
                    }
            }
        )

        scoring.OnHit.Add(fun h ->
            match h.Guts with
            | Hit d when not d.Missed -> Stats.session.NotesHit <- Stats.session.NotesHit + 1
            | _ -> ()
        )

        let send_replay_packet (now: Time) =
            // todo: not need this any more (used to make sure the replay buffers to correct position)
            if not (liveplay :> IReplayProvider).Finished then
                liveplay.Add(now, key_state)

            use ms = new MemoryStream()
            use bw = new BinaryWriter(ms)
            liveplay.ExportLiveBlock bw
            lobby.SendReplayData(ms.ToArray())
            packet_count <- packet_count + 1

        { new IPlayScreen(info.Chart, info.WithColors, PacemakerState.None, scoring) with
            override this.AddWidgets() =
                let user_options = options.HUD.Value
                let noteskin_options = Content.NoteskinConfig.HUD
                let inline add_widget position constructor =
                    add_widget (this, this.Playfield, this.State, user_options, noteskin_options) position constructor

                if user_options.ComboEnabled then add_widget noteskin_options.ComboPosition Combo
                if user_options.ProgressMeterEnabled then add_widget noteskin_options.ProgressMeterPosition ProgressMeter
                if user_options.AccuracyEnabled then add_widget noteskin_options.AccuracyPosition Accuracy
                if user_options.TimingDisplayEnabled then add_widget noteskin_options.TimingDisplayPosition TimingDisplay
                if this.State.Pacemaker <> PacemakerState.None then add_widget noteskin_options.PacemakerPosition Pacemaker
                if user_options.JudgementCounterEnabled then add_widget noteskin_options.JudgementCounterPosition JudgementCounter
                if user_options.JudgementMeterEnabled then add_widget noteskin_options.JudgementMeterPosition JudgementMeter
                if user_options.EarlyLateMeterEnabled then add_widget noteskin_options.EarlyLateMeterPosition EarlyLateMeter
                if user_options.RateModMeterEnabled then add_widget noteskin_options.RateModMeterPosition RateModMeter
                if user_options.BPMMeterEnabled then add_widget noteskin_options.BPMMeterPosition BPMMeter
                // todo: better positioning + ability to isolate and test this component
                add_widget noteskin_options.PacemakerPosition 
                    (fun (user_options, noteskin_options, state) -> MultiplayerScoreTracker(user_options, noteskin_options, state, lobby.Replays))

                let give_up () =
                    Screen.back Transitions.Flags.Default |> ignore

                this
                |* HotkeyHoldAction(
                    "exit",
                    (if options.HoldToGiveUp.Value then ignore else give_up),
                    (if options.HoldToGiveUp.Value then give_up else ignore)
                )

            override this.OnEnter(previous) =
                Stats.session.PlaysStarted <- Stats.session.PlaysStarted + 1
                base.OnEnter(previous)

            override this.OnExit(next) =
                if next = Screen.Type.Score then
                    Stats.session.PlaysCompleted <- Stats.session.PlaysCompleted + 1
                else
                    Stats.session.PlaysQuit <- Stats.session.PlaysQuit + 1

                if options.AutoCalibrateOffset.Value then
                    LocalAudioSync.apply_automatic this.State info.SaveData

                if next <> Screen.Type.Score then
                    lobby.AbandonPlaying()

                base.OnExit(next)

                DiscordRPC.playing_timed (
                    "Multiplayer",
                    info.CacheInfo.Title,
                    info.CacheInfo.Length / Gameplay.rate.Value
                )

            override this.Update(elapsed_ms, moved) =
                Stats.session.PlayTime <- Stats.session.PlayTime + elapsed_ms
                base.Update(elapsed_ms, moved)
                let now = Song.time_with_offset ()
                let chart_time = now - first_note

                if not (liveplay :> IReplayProvider).Finished then

                    Input.pop_gameplay (
                        binds,
                        fun column time is_release ->
                            if time > now then
                                Logging.Debug("Received input event from the future")
                            else
                                if is_release then
                                    key_state <- Bitmask.unset_key column key_state
                                else
                                    key_state <- Bitmask.set_key column key_state

                                liveplay.Add(time, key_state)
                    )

                    if chart_time / MULTIPLAYER_REPLAY_DELAY_MS / 1.0f<ms> |> floor |> int > packet_count then
                        send_replay_packet (now)

                    this.State.Scoring.Update chart_time

                if this.State.Scoring.Finished && not (liveplay :> IReplayProvider).Finished then
                    liveplay.Finish()
                    send_replay_packet (now)
                    lobby.FinishPlaying()

                    Screen.change_new
                        (fun () ->
                            let score_info =
                                Gameplay.score_info_from_gameplay
                                    info
                                    scoring
                                    ((liveplay :> IReplayProvider).GetFullReplay())

                            (score_info, Gameplay.set_score true score_info info.SaveData, true)
                            |> ScoreScreen
                        )
                        Screen.Type.Score
                        Transitions.Flags.Default
                    |> ignore
        }