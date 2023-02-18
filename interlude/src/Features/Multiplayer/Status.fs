﻿namespace Interlude.Features.Multiplayer

open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Prelude.Common
open Interlude.UI
open Interlude.UI.Components

type Status() =
    inherit StaticWidget(NodeType.None)

    override this.Draw() =
        let area = this.Bounds.Shrink(30.0f, 0.0f).TrimBottom(15.0f)
        let text, color =
            match Network.status with
            | Network.NotConnected -> Icons.notConnected + "  Offline", Color.FromArgb(200, 200, 200)
            | Network.Connecting -> Icons.connecting + "  Connecting..", Color.FromArgb(255, 255, 160)
            | Network.ConnectionFailed -> Icons.connectionFailed + "  Offline", Color.FromArgb(255, 160, 160)
            | Network.Connected -> Icons.connected + "  Guest", Color.FromArgb(160, 255, 160)
            | Network.LoggedIn -> Icons.connected + "  " + Network.username, Color.FromArgb(160, 255, 160)

        Draw.rect area (Color.FromArgb(100, 0, 0, 0))
        Text.drawFillB(Style.baseFont, text, area.Shrink(10.0f, 5.0f), (color, Color.Black), Alignment.CENTER)

        match this.Dropdown with
        | Some d -> d.Draw()
        | None -> ()

    override this.Update(elapsedTime, moved) =
        base.Update(elapsedTime, moved)
        
        match this.Dropdown with
        | Some d -> d.Update(elapsedTime, moved)
        | None -> ()

        if Mouse.hover this.Bounds && Mouse.leftClick() then this.ToggleDropdown()

    member this.MenuItems : (string * (unit -> unit)) seq =
        match Network.status with
        | Network.NotConnected -> [ "Connect", fun () -> Network.connect() ]
        | Network.Connecting -> [ "Please wait..", ignore ]
        | Network.ConnectionFailed -> [ "Reconnect", fun () -> Network.connect() ]
        | Network.Connected -> [ 
                "Log in", fun () -> Network.login("Percyqaz")
                "Disconnect", Network.disconnect
            ]
        | Network.LoggedIn -> [
                "Multiplayer", fun () -> Screen.change Screen.Type.Lobby Transitions.Flags.Default
                //"Log out", Network.disconnect
                "Disconnect", Network.disconnect
            ]

    member this.ToggleDropdown() =
        match this.Dropdown with
        | Some _ -> this.Dropdown <- None
        | _ ->
            let d = Dropdown(this.MenuItems, (fun () -> this.Dropdown <- None))
            d.Position <- Position.SliceTop(d.Height + Screen.Toolbar.HEIGHT).TrimTop(Screen.Toolbar.HEIGHT).Margin(Style.padding, 0.0f)
            d.Init this
            this.Dropdown <- Some d

    member val Dropdown : Dropdown option = None with get, set
