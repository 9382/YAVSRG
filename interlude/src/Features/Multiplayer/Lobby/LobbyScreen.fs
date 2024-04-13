﻿namespace Interlude.Features.Multiplayer

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Audio
open Prelude
open Interlude.Web.Shared
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Options
open Interlude.Features.Play
open Interlude.Features.Online
open Interlude.Features.LevelSelect
open Interlude.Features

type LobbySettingsPage(lobby: Lobby) as this =
    inherit Page()

    let settings = lobby.Settings

    let name = Setting.simple settings.Name
    let host_rotation = Setting.simple settings.HostRotation
    let auto_countdown = Setting.simple settings.AutomaticRoundCountdown

    do
        page_container()
        |+ PageTextEntry("lobby.name", name).Pos(0)
        |+ PageSetting("lobby.host_rotation", Selector<_>.FromBool(host_rotation))
            .Pos(3)
            .Tooltip(Tooltip.Info("lobby.host_rotation"))
        |+ PageSetting("lobby.auto_countdown", Selector<_>.FromBool(auto_countdown))
            .Pos(5)
            .Tooltip(Tooltip.Info("lobby.auto_countdown"))
        |> this.Content

    override this.Title = %"lobby.name"

    override this.OnClose() =
        lobby.ChangeSettings
            {
                Name = name.Value
                HostRotation = host_rotation.Value
                AutomaticRoundCountdown = auto_countdown.Value
            }

type LobbyUI(lobby: Lobby) =
    inherit Container(NodeType.None)

    override this.Init(parent) =
        this
        |+ Conditional(
            (fun () -> lobby.YouAreHost),
            Button(Icons.SETTINGS, (fun () -> LobbySettingsPage(lobby).Show())),
            Position = Position.SliceTop(90.0f).Margin(10.0f).SliceRight(70.0f)
        )
        |+ Text(
            (fun () -> lobby.Settings.Name),
            Align = Alignment.CENTER,
            Position =
                { Position.SliceTop(90.0f).Margin(10.0f) with
                    Right = 0.4f %- 0.0f
                }
        )
        |+ PlayerList(lobby,
            Position =
                {
                    Left = 0.0f %+ 50.0f
                    Right = 0.4f %- 50.0f
                    Top = 0.0f %+ 100.0f
                    Bottom = 1.0f %- 100.0f
                }
        )
        |+ StylishButton(
            (fun () -> Gameplay.Chart.if_loaded <| fun info -> Preview(info, ignore).Show()),
            K(sprintf "%s %s" Icons.EYE (%"levelselect.preview.name")),
            !%Palette.MAIN_100,
            TiltLeft = false,
            Hotkey = "preview",
            Position =
                { Position.SliceBottom(50.0f) with
                    Right = (0.4f / 3f) %- 25.0f
                }
        )
            .Tooltip(Tooltip.Info("levelselect.preview"))
        |+ StylishButton(
            ignore,
            K(sprintf "%s %s" Icons.ZAP (%"levelselect.mods.name")),
            !%Palette.DARK_100,
            Hotkey = "mods",
            Position =
                { Position.SliceBottom(50.0f) with
                    Left = (0.4f / 3f) %- 0.0f
                    Right = (0.4f / 1.5f) %- 25.0f
                }
        )
            .Tooltip(Tooltip.Info("levelselect.mods"))
        |+ Rulesets
            .QuickSwitcher(
                options.SelectedRuleset,
                Position =
                    {
                        Left = (0.4f / 1.5f) %+ 0.0f
                        Top = 1.0f %- 50.0f
                        Right = 0.4f %- 0.0f
                        Bottom = 1.0f %- 0.0f
                    }
            )
            .Tooltip(
                Tooltip
                    .Info("levelselect.rulesets", "ruleset_switch")
            )
        |+ SelectedChart(lobby,
            Position =
                {
                    Left = 0.5f %+ 20.0f
                    Top = 0.0f %+ 100.0f
                    Right = 1.0f %- 20.0f
                    Bottom = 0.5f %- 0.0f
                }
        )
        |* Chat(lobby,
            Position =
                { Position.Margin(20.0f) with
                    Left = 0.4f %+ 20.0f
                    Top = 0.5f %+ 0.0f
                }
        )

        base.Init parent

        lobby.OnGameStart.Add(fun () ->
            if
                Screen.current_type = Screen.Type.Lobby
                && lobby.ReadyStatus = ReadyFlag.Play
            then
                Gameplay.Chart.if_loaded
                <| fun info ->
                    if
                        Screen.change_new
                            (fun () -> PlayScreen.multiplayer_screen(info, lobby))
                            Screen.Type.Play
                            Transitions.Flags.Default
                        |> not
                    then
                        Logging.Warn("Missed the start of the lobby song because you were changing screen")

        )

        lobby.OnPlayerStatusChanged.Add(fun (username, status) ->
            if
                status = LobbyPlayerStatus.Playing
                && Screen.current_type = Screen.Type.Lobby
                && lobby.ReadyStatus = ReadyFlag.Spectate
            then
                Gameplay.Chart.if_loaded
                <| fun info ->
                    if
                        Screen.change_new
                            (fun () -> SpectateScreen.spectate_screen (info, username, lobby))
                            Screen.Type.Replay
                            Transitions.Flags.Default
                        |> not
                    then
                        Logging.Warn("Missed the start of spectating because you were changing screen")
        )

// Screen

type LobbyScreen() =
    inherit Screen()

    // todo: rename ui when lobby changes
    let swap = SwapContainer()
    let current_lobby = None

    do
        NetworkEvents.join_lobby.Add(fun lobby -> 
            let lobby_ui = LobbyUI(lobby)
            swap.Current <- lobby_ui
        )

    override this.OnEnter(_) =
        match Network.lobby with
        | Some lobby -> SelectedChart.switch lobby.Chart
        | None -> ()

        Song.on_finish <- SongFinishAction.LoopFromPreview
        DiscordRPC.in_menus ("Multiplayer lobby")

    override this.OnExit(_) = ()

    override this.OnBack() =
        match Network.lobby with
        | Some lobby ->
            ConfirmPage("Leave this lobby?", lobby.Leave).Show()
            None
        | None -> Some Screen.Type.LevelSelect

    override this.Init(parent) =
        this |* swap

        base.Init parent

    override this.Update(elapsed_ms, moved) =
        match Network.lobby with
        | None -> if not Transitions.active then Screen.back Transitions.Flags.Default |> ignore
        | Some _ -> base.Update(elapsed_ms, moved)
