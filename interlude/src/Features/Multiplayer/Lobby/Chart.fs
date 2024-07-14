﻿namespace Interlude.Features.Multiplayer

open System.Linq
open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Audio
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Data.Library.Caching
open Prelude.Data.Library.Collections
open Prelude.Gameplay.Mods
open Interlude.Web.Shared
open Interlude.Content
open Interlude.UI
open Interlude.Features.Import
open Interlude.Features.Gameplay
open Interlude.Features.Collections
open Interlude.Features.Online
open Interlude.Features.Play

type MultiplayerChartContextMenu(cc: CachedChart) =
    inherit Page()

    override this.Content() =
        FlowContainer.Vertical(PRETTYHEIGHT, Position = Position.Margin(100.0f, 200.0f))

        |+ PageButton(
            %"chart.add_to_collection",
            (fun () -> AddToCollectionPage(cc).Show()),
            Icon = Icons.FOLDER_PLUS
        )
        :> Widget

    override this.Title = cc.Title
    override this.OnClose() = ()

module LobbyChart =

    let mutable private last_seen_lobby_chart : LobbyChart option = None
    let mutable private last_seen_loaded_chart : LoadedChartInfo option = None
    let mutable private is_loading = false

    let info_if_selected() : LoadedChartInfo option =
        match last_seen_loaded_chart, last_seen_lobby_chart with
        | Some real_chart, Some expected_chart ->
            if real_chart.CacheInfo.Hash = expected_chart.Hash then
                Some real_chart
            else None
        | _ -> None

    let is_loaded_or_loading() =
        if is_loading then true else info_if_selected().IsSome

    let attempt_match_lobby_chart (lobby: Lobby) =
        last_seen_lobby_chart <- lobby.Chart
        match lobby.Chart with
        | None -> ()
        | Some chart ->

        match info_if_selected() with
        | Some _ ->
            SelectedChart.rate.Set chart.Rate

            SelectedChart.selected_mods.Set(
                chart.Mods
                |> Map.ofArray
                |> Map.filter (fun id _ -> Mods.AVAILABLE_MODS.ContainsKey id)
            )
        | None ->
            match Cache.by_hash chart.Hash Content.Cache with
            | None ->
                is_loading <- true
                Logging.Debug("Multiplayer chart not found, downloading")
                Backbeat.download_missing_chart.Request((chart.Hash, "Multiplayer"),
                    function
                    | false -> 
                        Logging.Debug("Multiplayer chart not found on the server either")
                        Network.lobby.Value.ReportMissingChart()
                        is_loading <- false
                        Notifications.error(%"notification.multiplayer_chart_not_found.title", %"notification.multiplayer_chart_not_found.body")
                    | true ->
                        let newly_installed = (Cache.by_hash chart.Hash Content.Cache).Value
                        Notifications.task_feedback(Icons.DOWNLOAD, %"notification.install_song", newly_installed.Title)
                        defer
                        <| fun () ->
                        SelectedChart.change (newly_installed, LibraryContext.None, true)
                        SelectedChart.rate.Set chart.Rate
                        SelectedChart.selected_mods.Set(
                            chart.Mods
                            |> Map.ofArray
                            |> Map.filter (fun id _ -> Mods.AVAILABLE_MODS.ContainsKey id)
                        )
                )
            | Some cc ->
                SelectedChart.change (cc, LibraryContext.None, true)
                SelectedChart.rate.Set chart.Rate

                SelectedChart.selected_mods.Set(
                    chart.Mods
                    |> Map.ofArray
                    |> Map.filter (fun id _ -> Mods.AVAILABLE_MODS.ContainsKey id)
                )

    let on_screen_enter(lobby: Lobby) =
        SelectedChart.if_loaded (fun info ->
            is_loading <- false
            last_seen_loaded_chart <- Some info
        )
        attempt_match_lobby_chart lobby

    do 
        SelectedChart.on_chart_change_finished.Add(fun info ->
            is_loading <- false
            last_seen_loaded_chart <- Some info
        )

        Content.OnChartAdded.Add(fun () ->
            match Network.lobby with
            | Some lobby ->
                if 
                    Screen.current_type = Screen.Type.Lobby
                    && not (is_loaded_or_loading()) 
                then
                    attempt_match_lobby_chart (lobby)
            | None -> ()
        )

type SelectedChart(lobby: Lobby) =
    inherit Container(NodeType.None)

    override this.Init(parent: Widget) =

        this
        |+ Text(
            (fun () ->
                match lobby.Chart with
                | Some c -> c.Title
                | None -> %"lobby.no_song_selected"
            ),
            Align = Alignment.LEFT,
            Position = Position.SliceTop(40.0f).Margin(10.0f, 0.0f)
        )
        |+ Text(
            (fun () ->
                match lobby.Chart with
                | Some c -> c.Artist + "  •  " + c.Creator
                | None -> ""
            ),
            Color = K Colors.text_subheading,
            Align = Alignment.LEFT,
            Position = Position.TrimTop(40.0f).SliceTop(30.0f).Margin(10.0f, 0.0f)
        )
        |+ Text(
            (fun () ->
                match LobbyChart.info_if_selected() with
                | Some info -> info.CacheInfo.DifficultyName
                | None -> "???"
            ),
            Color = K Colors.text_subheading,
            Align = Alignment.LEFT,
            Position = Position.TrimTop(70.0f).SliceTop(30.0f).Margin(10.0f, 0.0f)
        )

        |+ Text(
            (fun () ->
                match LobbyChart.info_if_selected() with
                | Some info -> sprintf "%s %.2f" Icons.STAR info.Rating.Physical
                | None -> ""
            ),
            Align = Alignment.LEFT,
            Position = Position.TrimTop(100.0f).SliceTop(60.0f)
        )
        |+ Text(
            (fun () -> 
                match LobbyChart.info_if_selected() with
                | Some info -> info.DurationString
                | None -> ""
            ),
            Align = Alignment.CENTER,
            Position = Position.TrimTop(100.0f).SliceTop(60.0f)
        )
        |+ Text(
            (fun () -> 
                match LobbyChart.info_if_selected() with
                | Some info -> info.BpmString
                | None -> ""
            ),
            Align = Alignment.RIGHT,
            Position = Position.TrimTop(100.0f).SliceTop(60.0f)
        )
        |+ Text(
            (fun () -> 
                match LobbyChart.info_if_selected() with
                | Some _ -> Mods.format (SelectedChart.rate.Value, SelectedChart.selected_mods.Value, false)
                | None -> ""
            ),
            Align = Alignment.LEFT,
            Position = Position.TrimTop(160.0f).SliceTop(40.0f)
        )
        |+ Text(
            (fun () -> 
                match LobbyChart.info_if_selected() with
                | Some info -> info.NotecountsString
                | None -> ""
            ),
            Align = Alignment.RIGHT,
            Position = Position.TrimTop(160.0f).SliceTop(40.0f)
        )
        |+ Text(
            (fun () ->
                if LobbyChart.is_loaded_or_loading() then
                    ""
                else
                    %"lobby.missing_chart"
            ),
            Align = Alignment.CENTER,
            Position = Position.TrimTop(100.0f).SliceTop(60.0f)
        )

        |+ Clickable(
            fun () -> if lobby.YouAreHost then Screen.change Screen.Type.LevelSelect Transitions.Default |> ignore
            , Position = Position.SliceTop(100.0f)
        )

        |+ StylishButton(
            (fun () -> lobby.Spectate <- not lobby.Spectate),
            (fun () ->
                if lobby.Spectate then
                    sprintf "%s %s" Icons.EYE (%"lobby.spectator")
                else
                    sprintf "%s %s" Icons.PLAY (%"lobby.player")
            ),
            !%Palette.MAIN_100,
            Position =
                { Position.SliceBottom(50.0f) with
                    Right = 0.5f %- 25.0f
                }
        )
            .Conditional(fun () -> 
                LobbyChart.info_if_selected().IsSome
                && not lobby.GameInProgress
                && lobby.ReadyStatus = ReadyFlag.NotReady
            )

        |+ StylishButton(
            (fun () ->
                match lobby.Replays |> Seq.tryHead with
                | Some (KeyValue (username, replay_info)) ->
                    match LobbyChart.info_if_selected() with
                    | Some info -> 
                        Screen.change_new
                            (fun () -> SpectateScreen.spectate_screen (info, username, replay_info, lobby))
                            Screen.Type.Replay
                            Transitions.Default
                        |> ignore
                    | None -> ()
                | None -> Logging.Debug("Couldn't find anyone with replay data to spectate")
            ),
            K(sprintf "%s %s" Icons.EYE (%"lobby.spectate")),
            !%Palette.DARK_100,
            TiltRight = false,
            Position =
                { Position.SliceBottom(50.0f) with
                    Left = 0.5f %- 0.0f
                }
        )
            .Conditional(fun () ->
                LobbyChart.info_if_selected().IsSome
                && lobby.GameInProgress
            )

        |+ StylishButton(
            (fun () ->
                lobby.SetReadyStatus (
                    match lobby.ReadyStatus with
                    | ReadyFlag.NotReady ->
                        if lobby.Spectate then
                            ReadyFlag.Spectate
                        else
                            ReadyFlag.Play
                    | _ -> ReadyFlag.NotReady
                )
            ),
            (fun () ->
                match lobby.ReadyStatus with
                | ReadyFlag.NotReady ->
                    if lobby.Spectate then
                        sprintf "%s %s" Icons.EYE (%"lobby.ready")
                    else
                        sprintf "%s %s" Icons.CHECK (%"lobby.ready")
                | _ -> sprintf "%s %s" Icons.X (%"lobby.not_ready")
            ),
            !%Palette.DARK_100,
            TiltRight = false,
            Position =
                { Position.SliceBottom(50.0f) with
                    Left = 0.5f %- 0.0f
                }
        )
            .Conditional(fun () ->
                LobbyChart.info_if_selected().IsSome
                && not Song.loading
                && not lobby.GameInProgress
            )

        |* StylishButton(
            (fun () ->
                if lobby.Countdown then
                    lobby.CancelRound()
                else
                    lobby.StartRound()
            ),
            (fun () ->
                if lobby.Countdown then
                    sprintf "%s %s" Icons.SLASH (%"lobby.cancel_game")
                else
                    sprintf "%s %s" Icons.PLAY (%"lobby.start_game")
            ),
            !%Palette.MAIN_100,
            Position =
                { Position.SliceBottom(50.0f) with
                    Right = 0.5f %- 25.0f
                }
        )
            .Conditional(fun () ->
                lobby.YouAreHost
                && lobby.ReadyStatus <> ReadyFlag.NotReady
                && not lobby.GameInProgress
            )

        lobby.OnChartChanged.Add(fun () ->
            if Screen.current_type = Screen.Type.Lobby then
                LobbyChart.attempt_match_lobby_chart lobby
        )

        base.Init parent

    override this.Draw() =
        let is_loaded = LobbyChart.is_loaded_or_loading()
        Draw.rect
            (this.Bounds.SliceTop(70.0f))
            (if is_loaded then
                 (!*Palette.DARK).O4a 180
             else
                 Color.FromArgb(180, 100, 100, 100))

        Draw.rect
            (this.Bounds.SliceTop(100.0f).SliceBottom(30.0f))
            (if is_loaded then
                 (!*Palette.DARKER).O4a 180
             else
                 Color.FromArgb(180, 50, 50, 50))

        Draw.rect
            (this.Bounds.SliceTop(100.0f).SliceLeft(5.0f))
            (if is_loaded then
                 !*Palette.MAIN
             else
                 Colors.white)

        base.Draw()
