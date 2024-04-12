﻿namespace Interlude.Features.LevelSelect

open OpenTK.Mathematics
open Percyqaz.Common
open Percyqaz.Flux.Input
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Audio
open Percyqaz.Flux.UI
open Prelude
open Prelude.Data.Library.Sorting
open Prelude.Data.Library.Caching
open Prelude.Data.Library.Collections
open Prelude.Data.Library.Endless
open Interlude.Content
open Interlude.Options
open Interlude.Features.Gameplay
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.Online
open Interlude.Features.Score

type LevelSelectScreen() =
    inherit Screen()

    let search_text = Setting.simple ""

    let info_panel =
        ChartInfo(
            Position =
                {
                    Left = 0.0f %+ 0.0f
                    Top = 0.0f %+ 175.0f
                    Right = 0.4f %- 10.0f
                    Bottom = 1.0f %+ 0.0f
                }
        )

    let refresh () =
        Chart.if_loaded (fun info -> info_panel.OnChartUpdated(info))
        Tree.refresh ()

    let random_chart () =
        if options.AdvancedRecommendations.Value && Chart.RATING.IsSome then
            let ctx =
                {
                    BaseDifficulty = Chart.RATING.Value.Physical
                    BaseChart = Chart.CACHE_DATA.Value, rate.Value
                    Filter = LevelSelect.filter |> Filter.except_keywords
                    Mods = selected_mods.Value
                    RulesetId = Rulesets.current_hash
                    Ruleset = Rulesets.current
                    Library = Content.Library
                    ScoreDatabase = Content.Scores
                    Priority = Endless.priority.Value
                }

            match Suggestion.get_suggestion ctx with
            | Some (cc, rate) ->
                Chart._rate.Value <- rate
                TreeState.switch_chart (cc, LibraryContext.None, "")
                refresh ()
            | None -> Notifications.action_feedback (Icons.ALERT_CIRCLE, %"notification.suggestion_failed", "")
        else
            let ctx =
                {
                    Rate = rate.Value
                    RulesetId = Rulesets.current_hash
                    Ruleset = Rulesets.current
                    Library = Content.Library
                    ScoreDatabase = Content.Scores
                }

            match Suggestion.get_random LevelSelect.filter ctx with
            | Some c ->
                TreeState.switch_chart (c, LibraryContext.None, "")
                refresh ()
            | None -> ()

    override this.Init(parent: Widget) =
        base.Init parent

        ScoreScreenHelpers.continue_endless_mode <-
            fun () -> Endless.continue_endless_mode (fun info -> LevelSelect.try_play info)

        Setting.app (fun s -> if sorting_modes.ContainsKey s then s else "title") options.ChartSortMode
        Setting.app (fun s -> if grouping_modes.ContainsKey s then s else "pack") options.ChartGroupMode

        this
        |+ Text(
            (fun () ->
                match Chart.CACHE_DATA with
                | None -> ""
                | Some c -> c.Title
            ),
            Align = Alignment.CENTER,
            Position =
                {
                    Left = 0.0f %+ 30.0f
                    Top = 0.0f %+ 20.0f
                    Right = 0.4f %- 30.0f
                    Bottom = 0.0f %+ 100.0f
                }
        )

        |+ Text(
            (fun () ->
                match Chart.CACHE_DATA with
                | None -> ""
                | Some c -> c.DifficultyName
            ),
            Align = Alignment.CENTER,
            Position =
                {
                    Left = 0.0f %+ 30.0f
                    Top = 0.0f %+ 90.0f
                    Right = 0.4f %- 30.0f
                    Bottom = 0.0f %+ 140.0f
                }
        )

        |+ SearchBox(
            search_text,
            (fun f ->
                LevelSelect.filter <- f
                refresh ()
            ),
            Position =
                {
                    Left = 1.0f %- 580.0f
                    Top = 0.0f %+ 30.0f
                    Right = 1.0f %- (20.0f + Style.PADDING)
                    Bottom = 0.0f %+ 90.0f
                }
        )
            .Tooltip(Tooltip.Info("levelselect.search", "search"))

        |+ Conditional(
            (fun () -> Tree.is_empty),
            Container(NodeType.None)
            |+ Conditional((fun () -> LevelSelect.filter <> []), EmptyState(Icons.SEARCH, %"levelselect.empty.search"))
            |+ Conditional(
                (fun () ->
                    LevelSelect.filter = []
                    && options.LibraryMode.Value = LibraryMode.Table
                    && Content.Table.IsNone
                ),
                EmptyState(Icons.SIDEBAR, %"levelselect.empty.no_table")
            )
            |+ Conditional(
                (fun () -> LevelSelect.filter = [] && options.LibraryMode.Value = LibraryMode.Collections),
                EmptyState(Icons.FOLDER, %"levelselect.empty.no_collections")
            )
            |+ Conditional(
                (fun () -> LevelSelect.filter = [] && options.LibraryMode.Value = LibraryMode.All),
                EmptyState(Icons.FOLDER, %"levelselect.empty.no_charts")
            ),
            Position =
                { Position.TrimTop(170.0f) with
                    Left = 0.5f %+ 0.0f
                }
        )

        |+ ActionBar(
            random_chart,
            Position =
                {
                    Left = 1.0f %- 805.0f
                    Top = 0.0f %+ 30.0f
                    Right = 1.0f %- 605.0f
                    Bottom = 0.0f %+ 90.0f
                }
        )

        |+ LibraryViewControls()

        |+ StylishButton(
            LevelSelect.choose_this_chart,
            K (sprintf "%s %s" Icons.PLAY %"levelselect.play.name"),
            !%Palette.MAIN.O2,
            TiltRight = false,
            Position = Position.SliceBottom(50.0f).SliceRight(300.0f)
        )
            .Tooltip(Tooltip.Info("levelselect.play").Hotkey("select"))
        |+ Conditional(
            (fun () -> match Chart.LIBRARY_CTX with LibraryContext.Playlist _ -> true | _ -> false),
            StylishButton(
                (fun () ->
                    match Chart.LIBRARY_CTX with
                    | LibraryContext.Playlist (_, name, _) ->
                        Endless.begin_endless_mode (
                            EndlessModeState.create_from_playlist
                                0
                                (Content.Collections.GetPlaylist(name).Value)
                                Content.Library
                        )

                        Endless.continue_endless_mode (fun info -> LevelSelect.try_play info) |> ignore
                    | _ -> ()
                ),
                K (sprintf "%s %s" Icons.PLAY_CIRCLE %"playlist.play.name"),
                !%Palette.DARK.O2,
                Position = Position.SliceBottom(50.0f).SliceRight(300.0f).Translate(-325.0f, 0.0f),
                Hotkey = "endless_mode",
                Disabled = (fun () -> Network.lobby.IsSome)
            ).Tooltip(Tooltip.Info("playlist.play").Hotkey("endless_mode"))
        )
        |+ Conditional(
            (fun () -> match Chart.LIBRARY_CTX with LibraryContext.Playlist _ -> false | _ -> true),
            StylishButton(
                (fun () -> Chart.if_loaded(fun info -> EndlessModeMenu(info).Show())),
                K (sprintf "%s %s" Icons.PLAY_CIRCLE %"levelselect.endless_mode.name"),
                !%Palette.DARK.O2,
                Position = Position.SliceBottom(50.0f).SliceRight(300.0f).Translate(-325.0f, 0.0f),
                Hotkey = "endless_mode",
                Disabled = (fun () -> Network.lobby.IsSome)
            )
                .Tooltip(Tooltip.Info("levelselect.endless_mode").Hotkey("endless_mode"))
        )
        |+ StylishButton(
            (fun () -> Chart.if_loaded(fun info -> ChartContextMenu(info.CacheInfo, info.LibraryContext).Show())),
            K Icons.LIST,
            !%Palette.MAIN.O2,
            Position = Position.SliceBottom(50.0f).SliceRight(60.0f).Translate(-650.0f, 0.0f)
        )
            .Tooltip(Tooltip.Info("levelselect.context_menu").Hotkey("context_menu"))

        |* info_panel

        Comments.init this

        LevelSelect.on_refresh_all.Add refresh

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        Comments.update (elapsed_ms, moved)

        if (%%"select").Tapped() then
            LevelSelect.choose_this_chart ()

        elif (%%"next").Tapped() then
            Tree.next ()
        elif (%%"previous").Tapped() then
            Tree.previous ()
        elif (%%"next_group").Tapped() then
            Tree.next_group ()
        elif (%%"previous_group").Tapped() then
            Tree.previous_group ()
        elif (%%"start").Tapped() then
            Tree.top_of_group ()
        elif (%%"end").Tapped() then
            Tree.bottom_of_group ()

        Tree.update (this.Bounds.Top + 170.0f, this.Bounds.Bottom, elapsed_ms)

    override this.Draw() =

        Tree.draw (this.Bounds.Top + 170.0f, this.Bounds.Bottom)

        let w = this.Bounds.Width * 0.4f

        let {
                Rect.Left = left
                Top = top
                Right = right
            } =
            this.Bounds

        Draw.untextured_quad
            (Quad.create
             <| Vector2(left, top)
             <| Vector2(left + w + 85.0f, top)
             <| Vector2(left + w, top + 170.0f)
             <| Vector2(left, top + 170.0f))
            (Quad.color !*Palette.DARK_100)

        Draw.rect (this.Bounds.SliceTop(170.0f).SliceLeft(w).Shrink(20.0f)) (Colors.shadow_2.O2)

        Draw.untextured_quad
            (Quad.create
             <| Vector2(left + w + 85.0f, top)
             <| Vector2(right, top)
             <| Vector2(right, top + 170.0f)
             <| Vector2(left + w, top + 170.0f))
            (Quad.color (Colors.shadow_2.O2))

        Draw.rect (this.Bounds.SliceTop(175.0f).SliceBottom(5.0f)) (Palette.color (255, 0.8f, 0.0f))

        base.Draw()
        Comments.draw ()

    override this.OnEnter prev =
        Endless.exit_endless_mode ()
        Song.on_finish <- SongFinishAction.LoopFromPreview

        if Cache.recache_service.Status <> Async.ServiceStatus.Idle then
            Notifications.system_feedback (
                Icons.ALERT_OCTAGON,
                %"notification.recache_running.title",
                %"notification.recache_running.body"
            )

        refresh ()
        DiscordRPC.in_menus ("Choosing a song")

    override this.OnExit next = Input.remove_listener ()

    override this.OnBack() =
        if Network.lobby.IsSome then
            Some Screen.Type.Lobby
        else
            Some Screen.Type.MainMenu
