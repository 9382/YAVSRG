﻿namespace Interlude.Features.Import

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude.Data.Charts
open Prelude.Data.Charts.Tables
open Interlude.Web.Shared.Requests
open Interlude.Content
open Interlude.UI.Components

[<RequireQualifiedAccess>]
type private TableStatus =
    | NotInstalled
    | OutOfDate
    | Installing
    | Installed

type TableCard(online_table: Tables.List.Table) as this =
    inherit
        FrameContainer(
            NodeType.Button(fun () ->
                Style.click.Play()
                this.Install()
            ),
            Fill = (fun () -> if this.Focused then Colors.pink.O2 else Colors.shadow_2.O2),
            Border =
                (fun () ->
                    if this.Focused then
                        Colors.pink_accent
                    else
                        Colors.grey_2.O3
                )
        )

    let mutable existing = None
    let mutable status = TableStatus.NotInstalled

    override this.Init (parent: Widget) =
        this
        |+ Text(online_table.Info.Name, Align = Alignment.CENTER, Position = Position.SliceTop(80.0f).Margin(20.0f, Style.PADDING))
        |+ Text(
            online_table.Info.Description,
            Align = Alignment.CENTER,
            Position = Position.TrimTop(65.0f).SliceTop(60.0f).Margin(20.0f, Style.PADDING)
        )
        |+ Text(
            fun () -> 
                match status with
                | TableStatus.NotInstalled -> "Click to install"
                | TableStatus.OutOfDate -> "Click to update"
                | TableStatus.Installing -> "Installing ..."
                | TableStatus.Installed -> "Click to view"
            ,
            Align = Alignment.CENTER,
            Position = Position.SliceBottom(60.0f).Margin(20.0f, Style.PADDING)
        )
        |+ LoadingIndicator.Border(fun () -> status = TableStatus.Installing)
        |* Clickable.Focus this

        existing <- Tables.by_id online_table.Id
        status <-
            match existing with
            | Some table when table.LastUpdated < online_table.LastUpdated -> TableStatus.OutOfDate
            | Some _ -> TableStatus.Installed
            | None -> TableStatus.NotInstalled

        base.Init parent

    override this.OnFocus (by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    member this.Install() =
        match status with
        | TableStatus.NotInstalled
        | TableStatus.OutOfDate as current_status ->
            status <- TableStatus.Installing
            Tables.Charts.get (online_table.Id,
                function
                | Some charts ->
                    sync(fun () ->
                        let table =
                            {
                                Id = online_table.Id
                                Info = online_table.Info
                                LastUpdated = online_table.LastUpdated
                                Charts = charts.Charts |> Array.map (fun c -> { Hash = c.Hash; Level = c.Level }) |> List.ofArray
                            }
                        Tables.install_or_update table
                        Tables.selected_id.Value <- Some table.Id
                        status <- TableStatus.Installed
                        existing <- Some table
                        TableDownloadMenu.OpenAfterInstall(table, charts)
                    )
                | None -> 
                    Logging.Error("Error getting charts for table")
                    // error toast
                    sync(fun () -> status <- current_status)
            )
        | TableStatus.Installing -> ()
        | TableStatus.Installed -> 
            Tables.selected_id.Value <- Some existing.Value.Id
            TableDownloadMenu.LoadOrOpen(existing.Value)

module Tables =

    type TableList() as this =
        inherit StaticContainer(NodeType.Container(fun _ -> Some this.Items))

        let flow = FlowContainer.Vertical<TableCard>(200.0f, Spacing = 15.0f)
        let scroll = ScrollContainer(flow, Margin = Style.PADDING)

        override this.Init(parent) =
            Tables.List.get (
                function
                | Some tables ->
                    for table in tables.Tables do
                        sync (fun () -> flow.Add(TableCard(table)))
                | None -> Logging.Error("Error getting online tables list")
            )
            this |* scroll
            base.Init parent

        override this.Focusable = flow.Focusable

        member this.Items = flow

    let tab = TableList()
