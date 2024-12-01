﻿namespace Interlude.Features.Import.Etterna

open System.IO
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude
open Prelude.Data.Library
open Prelude.Data.Library.Sorting
open Prelude.Data
open Interlude.Content
open Interlude.UI

type private PackDownloadStatus =
    | NotDownloaded
    | Downloading
    | Installed
    | DownloadFailed

type EtternaPackCard(data: EtternaOnlinePack) as this =
    inherit
        FrameContainer(
            NodeType.Button(fun () ->
                Style.click.Play()
                this.Download()
            ),
            Fill = !%Palette.MAIN_100,
            Border =
                (fun () ->
                    if this.Focused then
                        !*Palette.LIGHT
                    else
                        Palette.color (200, 0.7f, 0.2f)
                )
        )

    let mutable progress = 0.0f

    let mutable status =
        let path = Path.Combine(get_game_folder "Songs", data.name)
        if Directory.Exists path then Installed else NotDownloaded

    let download () =
        if status <> Downloading then
            let target =
                Path.Combine(get_game_folder "Downloads", System.Guid.NewGuid().ToString() + ".zip")

            progress <- 0.0f

            WebServices.download_file.Request(
                (data.download, target, (fun p -> progress <- p)),
                fun completed ->
                    if completed then
                        Imports.convert_stepmania_pack_zip.Request(
                            (target, data.name, Content.Library),
                            function
                            | Some result ->
                                Notifications.task_feedback (
                                    Icons.DOWNLOAD, 
                                    %"notification.install_pack",
                                    [data.name; result.ConvertedCharts.ToString(); result.SkippedCharts.Length.ToString()] %> "notification.install_pack.body"
                                )
                                Content.TriggerChartAdded()
                                progress <- 1.0f
                                status <- Installed
                                File.Delete target
                            | None ->
                                Notifications.error (%"notification.install_pack_failed", data.name)
                                status <- DownloadFailed
                                File.Delete target
                        )
                    else
                        status <- DownloadFailed
            )

            status <- Downloading

    override this.Init(parent: Widget) =
        this
        |+ Text(
            data.name,
            Align = Alignment.LEFT,
            Position = Position.SliceT(45.0f).Shrink(10.0f, 0.0f)
        )
        |+ Text(
            data.size,
            Align = Alignment.RIGHT,
            Position = Position.SliceT(45.0f).ShrinkR(165.0f).Shrink(10.0f, 0.0f)
        )
        |+ Text(
            (fun () ->
                if status = Installed then "Downloaded!"
                elif status = DownloadFailed then "Download failed!"
                else ""
            ),
            Color = (fun () -> if status = DownloadFailed then Colors.text_red else Colors.text_green),
            Align = Alignment.RIGHT,
            Position = Position.SliceB(45.0f).ShrinkR(165.0f).Shrink(10.0f, 5.0f)
        )
        |+ Text(
            (sprintf "Average difficulty (MSD): %.2f" data.overall),
            Color = K Colors.text_subheading,
            Align = Alignment.LEFT,
            Position = Position.SliceB(45.0f).Shrink(10.0f, 5.0f)
        )
        |+ Button(
            Icons.EXTERNAL_LINK
            , fun () -> open_url (sprintf "https://beta.etternaonline.com/packs/%i" data.id)
            , Position = Position.SliceR(160.0f).ShrinkR(80.0f).Shrink(10.0f, 10.0f)
        )
        |+ Button(Icons.DOWNLOAD, download, Position = Position.SliceR(80.0f).Shrink(10.0f, 10.0f))
        |* Clickable.Focus this

        if data.contains_nsfw then
            this
            |* Text(
                Icons.ALERT_TRIANGLE + " NFSW content",
                Color = K Colors.text_red,
                Align = Alignment.LEFT,
                Position = Position.SliceT(45.0f).Shrink(10.0f, 5.0f).Translate(15.0f + Text.measure (Style.font, data.name) * 45.0f * 0.6f, 0.0f)
            )

        base.Init parent

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        base.Draw()

        if progress > 0.0f then
            Render.rect (this.Bounds.SliceL(this.Bounds.Width * progress)) (Color.FromArgb(64, 255, 255, 255))

    member this.Data = data

    member private this.Download() = download ()

    static member Filter(filter: FilterPart list) =
        fun (c: EtternaPackCard) ->
            List.forall
                (function
                | Impossible -> false
                | String str -> c.Data.name.ToLower().Contains(str)
                | _ -> true)
                filter