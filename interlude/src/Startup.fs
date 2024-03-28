﻿namespace Interlude.UI

open Percyqaz.Common
open Prelude.Charts.Processing.NoteColors
open Prelude.Data
open Prelude.Data.Library.Caching
open Interlude
open Interlude.Content
open Interlude.Features
open Interlude.Features.Stats
open Interlude.Features.MainMenu
open Interlude.Features.Import
open Interlude.Features.Score
open Interlude.Features.Play
open Interlude.Features.LevelSelect
open Interlude.Features.Multiplayer
open Interlude.Features.Printerlude
open Interlude.Features.Toolbar
open Interlude.Features.Online

//module private Migrations =

//let run_1 () =
//    Logging.Info "---- ---- ----"
//    Logging.Info "Hold it! Update 0.7.2 changes how chart hashing works - Please wait while your data gets migrated"
//    Logging.Info "This may take a couple of minutes, do not close your game ..."

//    // build mapping from cache
//    let mapping = Dictionary<string, ResizeArray<string>>()

//    for folder in
//        Directory.EnumerateDirectories Library.cache.RootPath
//        |> Seq.filter (fun p -> Path.GetFileName p <> ".assets") do
//        for file in Directory.EnumerateFiles folder do
//            match Path.GetExtension(file).ToLower() with
//            | ".yav" ->
//                match Chart.from_file file with
//                | Some c ->
//                    match Chart.check c with
//                    | Error _ ->

//                        let fix = Chart.LegacyHash.fix c

//                        match Chart.check fix with
//                        | Ok() ->
//                            let old_hash = Chart.LegacyHash.hash c
//                            let new_hash = Chart.hash fix

//                            if not (mapping.ContainsKey new_hash) then
//                                mapping.[new_hash] <- ResizeArray<_>()

//                            mapping.[new_hash].Add(old_hash)
//                            let file_hash = Path.GetFileNameWithoutExtension(file)

//                            if file_hash <> old_hash then
//                                mapping.[new_hash].Add(file_hash)
//                        | Error _ -> ()

//                        File.Delete file

//                    | Ok() ->
//                        let old_hash = Chart.LegacyHash.hash c
//                        let new_hash = Chart.hash c

//                        if not (mapping.ContainsKey new_hash) then
//                            mapping.[new_hash] <- ResizeArray<_>()

//                        mapping.[new_hash].Add(old_hash)
//                | None -> ()
//            | _ -> ()

//    // migrate scores
//    Logging.Info "Migrating your scores ..."

//    File.Copy(
//        Path.Combine(get_game_folder "Data", "scores.json"),
//        Path.Combine(get_game_folder "Data", "scores-migration-backup.json")
//    )

//    let old_scores = Dictionary(Scores.data.Entries)
//    Scores.data.Entries.Clear()

//    let mutable migrated_scores = 0

//    for new_hash in mapping.Keys do
//        let old_hashes = mapping.[new_hash] |> List.ofSeq

//        let old_datas =
//            old_hashes
//            |> List.choose (fun old_hash ->
//                if old_scores.ContainsKey old_hash then
//                    let r = old_scores.[old_hash]
//                    old_scores.Remove(old_hash) |> ignore
//                    Some r
//                else
//                    None
//            )

//        match old_datas with
//        | data :: ds ->
//            for other_data in ds do
//                if data.Comment = "" then
//                    data.Comment <- other_data.Comment

//                data.Scores.AddRange(other_data.Scores)
//                data.LastPlayed <- max data.LastPlayed other_data.LastPlayed

//            Scores.data.Entries.[new_hash] <- data
//            migrated_scores <- migrated_scores + data.Scores.Count
//        | [] -> ()

//    let mutable unmigrated_scores = 0

//    for d in old_scores.Values do
//        unmigrated_scores <- unmigrated_scores + d.Scores.Count

//    Logging.Info(
//        sprintf
//            "Migrated %i scores, %i have been left behind (probably because they are scores on charts you have deleted)"
//            migrated_scores
//            unmigrated_scores
//    )

//    Logging.Info "Migrating your collections ..."
//    let reverse_mapping = Dictionary<string, string>()

//    for new_hash in mapping.Keys do
//        for old_hash in mapping.[new_hash] do
//            reverse_mapping.[old_hash] <- new_hash

//    for folder in Library.collections.Folders.Values do
//        let charts = List(folder.Charts)
//        folder.Charts.Clear()

//        for c in charts do
//            if reverse_mapping.ContainsKey(c.Hash) then
//                folder.Charts.Add(
//                    {
//                        Path = c.Path.Replace(c.Hash, reverse_mapping.[c.Hash])
//                        Hash = reverse_mapping.[c.Hash]
//                    }
//                )

//    for pl in Library.collections.Playlists.Values do
//        let charts = List(pl.Charts)
//        pl.Charts.Clear()

//        for c, info in charts do
//            if reverse_mapping.ContainsKey(c.Hash) then
//                pl.Charts.Add(
//                    ({
//                        Path = c.Path.Replace(c.Hash, reverse_mapping.[c.Hash])
//                        Hash = reverse_mapping.[c.Hash]
//                     },
//                     info)
//                )

//    // now recache which corrects all the hashes

//    Logging.Info "Running a recache ..."
//    Cache.recache_service.RequestAsync Library.cache |> Async.RunSynchronously

module Startup =
    let MIGRATION_VERSION = 2

    let migrate () =

        if Stats.total.MigrationVersion.IsNone then
            if Content.Cache.Entries.Count > 0 then
                Stats.total.MigrationVersion <- Some 0
            else
                Stats.total.MigrationVersion <- Some MIGRATION_VERSION

        match Stats.total.MigrationVersion with
        | None -> failwith "impossible"
        | Some i ->
            if i < 1 then
                // Originally a migration here for migrating to new hash format from before 0.7.2
                Stats.total.MigrationVersion <- Some 1

            if i < 2 then
                // Originally a migration here for generating pattern data
                Stats.total.MigrationVersion <- Some 2

    let init_startup (instance) =
        Options.init_startup instance
        Options.Hotkeys.init_startup Options.options.Hotkeys
        Stats.init_startup ()
        Content.init_startup ()

    let init_window (instance) =
        Screen.init_window
            [|
                LoadingScreen()
                MainMenuScreen()
                ImportScreen()
                LobbyScreen()
                LevelSelectScreen()
            |]

        ScoreScreenHelpers.watch_replay <-
            fun (score_info: ScoreInfo, with_colors: ColoredChart) ->
                if
                    Screen.change_new
                        (fun () ->
                            ReplayScreen.replay_screen (score_info.Chart, ReplayMode.Replay(score_info, with_colors))
                            :> Screen.T
                        )
                        Screen.Type.Replay
                        Transitions.Flags.Default
                then
                    Gameplay.rate.Value <- score_info.Rate

        OptionsMenu.Noteskins.Helpers.open_hud_editor <-
            fun () -> OptionsMenu.Noteskins.EditHUDPage().Show()

        AutoUpdate.check_for_updates ()
        Mounts.import_mounts_on_startup ()

        { new Screen.ScreenRoot(Toolbar()) with
            override this.Init() =
                Printerlude.init_window (instance)
                Content.init_window ()
                DiscordRPC.init_window ()
                migrate ()
                Gameplay.init_window ()
                Network.init_window ()
                base.Init()
        }

    let mutable private has_shutdown = false

    let deinit unexpected_shutdown crash_splash =
        if has_shutdown then
            ()
        else
            has_shutdown <- true
            Stats.deinit ()
            Content.deinit ()
            Options.deinit ()
            Network.deinit ()
            Printerlude.deinit ()
            DiscordRPC.deinit ()

            if unexpected_shutdown then
                crash_splash ()
                Logging.Critical("The game crashed or quit abnormally, but was able to shut down correctly")
            else
                Logging.Info("Thank you for playing")

            Logging.Shutdown()
