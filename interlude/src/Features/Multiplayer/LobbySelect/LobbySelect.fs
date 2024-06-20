﻿namespace Interlude.Features.Multiplayer

open Percyqaz.Flux.UI
open Prelude
open Interlude.UI
open Interlude.UI.Menu
open Interlude.Features.Online

type LobbySelectPage() =
    inherit Page()

    let lobby_list = 
        LobbyList(
            Position =
                { Position.Margin(PRETTY_MARGIN_X, PRETTY_MARGIN_Y) with
                    Right = 0.7f %- (PRETTY_MARGIN_X * 0.5f)
                }
        )

    let invite_list =
        InviteList(
            Position =
                { Position.Margin(PRETTY_MARGIN_X, PRETTY_MARGIN_Y) with
                    Left = 0.7f %+ (PRETTY_MARGIN_X * 0.5f)
                }
        )

    let subscribed_events =
        NetworkEvents.receive_lobby_list.Subscribe (fun lobbies -> lobby_list.UpdateList lobbies),
        NetworkEvents.receive_invite.Subscribe (fun _ -> invite_list.UpdateList()),
        NetworkEvents.join_lobby.Subscribe (fun lobby -> Menu.Exit(); Screen.change Screen.Type.Lobby Transitions.Default |> ignore)

    override this.Content() =
        Container(NodeType.Leaf)
        |+ lobby_list
        |+ invite_list
        :> Widget

    override this.Title = %"select_lobby"
    override this.OnClose() =
        let a, b, c = subscribed_events
        a.Dispose()
        b.Dispose()
        c.Dispose()