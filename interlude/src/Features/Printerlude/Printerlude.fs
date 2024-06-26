﻿namespace Interlude.Features.Printerlude

open System
open System.Threading
open System.IO
open Percyqaz.Common
open Percyqaz.Shell
open Percyqaz.Shell.Shell
open Prelude
open Prelude.Charts
open Prelude.Data.Library.Caching
open Prelude.Data.``osu!``
open Prelude.Data
open Prelude.Gameplay
open Interlude
open Interlude.Content
open Interlude.Features.Gameplay
open Interlude.Features.Online
open Interlude.Web.Shared.Requests

module Printerlude =

    let mutable private ctx: ShellContext = Unchecked.defaultof<_>

    module private Utils =

        let mutable cmp = None

        let cmp_1 () =
            match SelectedChart.CHART with
            | None -> failwith "Select a chart"
            | Some c -> cmp <- Some c

        let cmp_2 () =
            match cmp with
            | None -> failwith "Use cmp_1 first"
            | Some cmp ->

            match SelectedChart.CHART with
            | None -> failwith "Select a chart"
            | Some c -> Chart.diff cmp c

        let show_version (io: IOContext) =
            io.WriteLine(sprintf "You are running %s" Updates.version)
            io.WriteLine(sprintf "The latest version online is %s" Updates.latest_version_name)

        let timescale (io: IOContext) (v: float) =
            UI.Screen.timescale <- System.Math.Clamp(v, 0.01, 10.0)
            io.WriteLine(sprintf "Entering warp speed (%.0f%%)" (UI.Screen.timescale * 100.0))

        open SixLabors.ImageSharp

        let private banner (hex: string) (emoji: string) =
            use banner =
                ImageServices.generate_banner
                    {
                        BaseColor = Color.FromHex hex
                        Emoji = emoji.ToLower()
                    }

            banner.SaveAsPng("banner.png")

        let private sync_table_scores () =
            match Content.Table with
            | None -> ()
            | Some table ->

            Tables.Records.get (
                Interlude.Features.Online.Network.credentials.Username,
                table.Id,
                function
                | None -> ()
                | Some res ->
                    let lookup = res.Scores |> Seq.map (fun s -> s.Hash, s.Score) |> Map.ofSeq

                    for chart in table.Charts do
                        let data = ScoreDatabase.get chart.Hash Content.Scores

                        match
                            data.PersonalBests
                            |> Bests.ruleset_best_above table.Info.RulesetId (_.Accuracy) 1.0f
                        with
                        | Some acc when acc > (Map.tryFind chart.Hash lookup |> Option.defaultValue 0.0) ->
                            for score in data.Scores do
                                Charts.Scores.Save.post (
                                    ({
                                        ChartId = chart.Hash
                                        Replay = score.Replay |> Replay.compressed_bytes_to_string
                                        Rate = score.Rate
                                        Mods = score.Mods
                                        Timestamp = Timestamp.to_datetime score.Timestamp
                                    }
                                    : Charts.Scores.Save.Request),
                                    ignore
                                )
                        | _ -> ()
            )

        let private personal_best_fixer =
            { new Async.Service<string * Ruleset, unit>() with
                override this.Handle((ruleset_id, ruleset)) =
                    async {
                        for cc in Content.Cache.Entries.Values |> Seq.toArray do
                            let data = ScoreDatabase.get cc.Hash Content.Scores

                            if not data.Scores.IsEmpty then
                                match Cache.load cc Content.Cache with
                                | Error reason ->
                                    Logging.Debug(sprintf "Couldn't load %s for pb processing: %s" cc.Key reason)
                                | Ok chart ->

                                let existing_bests = data.PersonalBests
                                let mutable new_bests = existing_bests

                                for score in data.Scores do
                                    let score_info = ScoreInfo.from_score cc chart ruleset score

                                    if new_bests.ContainsKey ruleset_id then
                                        let ruleset_bests, _ = Bests.update score_info new_bests.[ruleset_id]
                                        new_bests <- Map.add ruleset_id ruleset_bests new_bests
                                    else
                                        new_bests <- Map.add ruleset_id (Bests.create score_info) new_bests

                                if new_bests <> existing_bests then
                                    data.PersonalBests <- new_bests

                        Logging.Info(sprintf "Finished processing personal bests for %s" ruleset.Name)
                    }
            }

        let fix_personal_bests () =
            personal_best_fixer.Request((Rulesets.current_hash, Rulesets.current), ignore)
            Logging.Info("Queued a reprocess of personal bests")

        let register_commands (ctx: ShellContext) =
            ctx
                .WithCommand("exit", "Exits the game", (fun () -> UI.Screen.exit <- true))
                .WithCommand("clear", "Clears the terminal", Terminal.Log.clear)
                .WithCommand("fix_personal_bests", "Fix personal best display values", fix_personal_bests)
                .WithCommand("sync_table_scores", "Sync local table scores with online server", sync_table_scores)
                .WithIOCommand(
                    "local_server",
                    "Switch to local development server",
                    "flag",
                    fun (io: IOContext) (b: bool) ->
                        Network.credentials.Host <- (if b then "localhost" else "online.yavsrg.net")
                        Network.credentials.Api <- (if b then "localhost" else "api.yavsrg.net")
                        Updates.restart_on_exit <- true
                        UI.Screen.exit <- true
                )
                .WithIOCommand("timescale", "Sets the timescale of all UI animations, for testing", "speed", timescale)
                .WithCommand("banner", "Generates a banner image (for testing)", "color", "emoji", banner)
                .WithCommand("fake_update", "Fakes an update for testing the update UI button", fun () -> if Updates.latest_release.IsSome then Updates.update_available <- true)
                .WithCommand("cmp_1", "Select chart to compare against", cmp_1)
                .WithCommand("cmp_2", "Compare current chart to selected chart", cmp_2)

        let register_ipc_commands (ctx: ShellContext) =
            ctx.WithIOCommand("version", "Shows info about the current game version", show_version)

    let private ms = new MemoryStream()
    let private context_output = new StreamReader(ms)
    let private context_writer = new StreamWriter(ms)

    let io = { In = stdin; Out = context_writer }

    let exec (s: string) =
        let current_stream_position = ms.Position
        ctx.Evaluate io s
        context_writer.Flush()
        ms.Position <- current_stream_position
        Terminal.add_message (context_output.ReadToEnd())

    let mutable logging_disposable: IDisposable option = None
    let mutable ipc_shutdown_token: CancellationTokenSource option = None

    let ipc_commands = ShellContext.Empty |> Utils.register_ipc_commands

    let init_window (instance: int) =

        ctx <-
            ShellContext.Empty
            |> Utils.register_ipc_commands
            |> Utils.register_commands

        Terminal.exec_command <- exec

        logging_disposable <-
            Some
            <| Logging.Subscribe(fun (level, main, details) -> sprintf "[%A] %s" level main |> Terminal.add_message)

        Terminal.add_message @"================================================"
        Terminal.add_message @"=   ___      _      __          __        __   ="
        Terminal.add_message @"=  / _ \____(_)__  / /____ ____/ /_ _____/ /__ ="
        Terminal.add_message @"= / ___/ __/ / _ \/ __/ -_) __/ / // / _  / -_)="
        Terminal.add_message @"=/_/  /_/ /_/_//_/\__/\__/_/ /_/\_,_/\_,_/\__/ ="
        Terminal.add_message @"================================================"

        if instance = 0 then
            ipc_shutdown_token <- Some(IPC.start_server_thread "Interlude" ipc_commands)

    let deinit () =
        logging_disposable |> Option.iter (fun d -> d.Dispose())
        ipc_shutdown_token |> Option.iter (fun token -> token.Cancel())
 