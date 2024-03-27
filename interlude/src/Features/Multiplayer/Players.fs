﻿namespace Interlude.Features.Multiplayer

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Interlude.Web.Shared
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.Online

type InvitePlayerPage() as this =
    inherit Page()

    let value = Setting.simple ""
    let submit () = Lobby.invite value.Value

    let submit_button =
        PageButton(
            "confirm.yes",
            (fun () ->
                submit ()
                Menu.Back()
            ),
            Enabled = false
        )

    do
        this.Content(
            page_container()
            |+ PageTextEntry(
                "invite_to_lobby.username",
                value |> Setting.trigger (fun s -> submit_button.Enabled <- s.Length > 0)
            )
                .Pos(0)
            |+ submit_button.Pos(3)
        )

    override this.Title = %"invite_to_lobby.name"
    override this.OnClose() = ()

type Player(name: string, player: Network.LobbyPlayer) =
    inherit StaticWidget(NodeType.None)

    override this.Draw() =
        let icon, fill, border =
            match player.Status with
            | LobbyPlayerStatus.Ready -> Icons.CHECK, Colors.green, Colors.green_accent
            | LobbyPlayerStatus.ReadyToSpectate -> Icons.EYE, Colors.green, Colors.green_accent
            | LobbyPlayerStatus.Playing -> Icons.PLAY, Colors.green, Colors.green_accent
            | LobbyPlayerStatus.Spectating -> Icons.EYE, Colors.green, Colors.green_accent
            | LobbyPlayerStatus.AbandonedPlay -> Icons.X, Colors.grey_2.O1, Colors.white
            | LobbyPlayerStatus.MissingChart -> Icons.SLASH, Colors.grey_2.O1, Colors.white
            | LobbyPlayerStatus.NotReady
            | _ -> "", Colors.cyan, Colors.cyan_accent

        let b = this.Bounds.Expand(Style.PADDING)
        Draw.rect (b.SliceTop Style.PADDING) border.O3
        Draw.rect (b.SliceBottom Style.PADDING) border.O3
        let b2 = this.Bounds.Expand(Style.PADDING, 0.0f)
        Draw.rect (b2.SliceRight Style.PADDING) border.O3
        Draw.rect (b2.SliceLeft Style.PADDING) border.O3

        Draw.rect this.Bounds fill.O3

        Text.fill_b (Style.font, name, this.Bounds.Shrink(10.0f, 0.0f), Colors.text, Alignment.LEFT)
        Text.fill_b (Style.font, icon, this.Bounds.Shrink(10.0f, 0.0f), Colors.text, Alignment.RIGHT)

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        if
            Network.lobby.IsSome
            && Network.lobby.Value.YouAreHost
            && Mouse.hover this.Bounds
            && Mouse.left_click ()
        then
            ConfirmPage([ name ] %> "lobby.confirm_transfer_host", (fun () -> Lobby.transfer_host name))
                .Show()

    member this.Name = name

type PlayerList() =
    inherit Container(NodeType.None)

    let other_players = FlowContainer.Vertical<Widget>(50.0f, Spacing = 5.0f)

    let other_players_scroll =
        ScrollContainer(other_players, Position = Position.TrimTop 60.0f, Margin = Style.PADDING)

    let refresh () =
        other_players.Clear()

        match Network.lobby with
        | None -> Logging.Error "Tried to update player list while not in a lobby"
        | Some l ->
            for username in l.Players.Keys do
                other_players.Add(Player(username, l.Players.[username]))

        other_players.Add(
            Button(sprintf "%s %s" Icons.MAIL (%"lobby.send_invite"), (fun () -> Menu.ShowPage InvitePlayerPage))
        )

    override this.Init(parent) =
        this |* other_players_scroll
        refresh ()

        Network.Events.join_lobby.Add refresh
        Network.Events.lobby_players_updated.Add refresh

        base.Init parent

    override this.Draw() =

        let fill, border = Colors.cyan, Colors.cyan_accent

        let user_bounds = this.Bounds.SliceTop(55.0f)

        let b = user_bounds.Expand(Style.PADDING)
        Draw.rect (b.SliceTop Style.PADDING) border.O3
        Draw.rect (b.SliceBottom Style.PADDING) border.O3
        let b2 = user_bounds.Expand(Style.PADDING, 0.0f)
        Draw.rect (b2.SliceRight Style.PADDING) border.O3
        Draw.rect (b2.SliceLeft Style.PADDING) border.O3

        Draw.rect user_bounds fill.O3

        Text.fill_b (
            Style.font,
            Network.credentials.Username,
            user_bounds.Shrink(10.0f, 0.0f),
            Colors.text,
            Alignment.LEFT
        )

        Text.fill_b (
            Style.font,
            (if
                 (match Network.lobby with
                  | Some l -> l.YouAreHost
                  | None -> false)
             then
                 Icons.STAR + " Host"
             else
                 ""),
            user_bounds.Shrink(10.0f, 0.0f),
            Colors.text,
            Alignment.RIGHT
        )

        base.Draw()
