﻿namespace Interlude.Features.Toolbar

open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Interlude
open Interlude.UI

type Updater() as this =
    inherit Container(NodeType.Button(fun () -> this.Click()))

    do this |* Clickable.Focus this

    override this.Draw() =
        let area = this.Bounds.Shrink(0.0f, 7.5f)
        Render.rect area (Colors.shadow_1.O2)

        if Updates.update_complete then
            Text.fill_b (
                Style.font,
                Icons.REFRESH_CCW + " Restart game",
                area.Shrink(20.0f, 5.0f),
                (if this.Focused then
                     Colors.text_yellow_2
                 else
                     Colors.text_green_2),
                Alignment.CENTER
            )
        elif Updates.update_started then
            Text.fill_b (
                Style.font,
                Icons.DOWNLOAD + " Installing update..",
                area.Shrink(20.0f, 5.0f),
                Colors.text_yellow_2,
                Alignment.CENTER
            )
        else
            Text.fill_b (
                Style.font,
                Icons.DOWNLOAD + " Install update",
                area.Shrink(20.0f, 5.0f),
                (if this.Focused then
                     Colors.text_yellow_2
                 else
                     Colors.text_green_2),
                Alignment.CENTER
            )

    member this.Click() =
        if Updates.update_complete then
            Updates.restart_on_exit <- true
            Screen.change Screen.Type.SplashScreen Transitions.UnderLogo |> ignore
        elif Updates.update_started then
            ()
        else
            Notifications.system_feedback (
                Icons.ALERT_OCTAGON,
                %"notification.update_installing.title",
                %"notification.update_installing.body"
            )

            Updates.apply_update (fun () ->
                Notifications.system_feedback (
                    Icons.ALERT_OCTAGON,
                    %"notification.update_installed.title",
                    %"notification.update_installed.body"
                )
            )
