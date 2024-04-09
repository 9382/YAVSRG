﻿namespace Interlude.Features.Import

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Prelude.Gameplay
open Prelude.Data
open Prelude.Data.Library.Sorting
open Interlude.UI
open Interlude.Content

type private RulesetStatus =
    | NotInstalled
    | UpdateAvailable
    | UpToDate

type RulesetCard(id: string, ruleset: Ruleset) as this =
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

    let mutable status =
        if Rulesets.list () |> Seq.map fst |> Seq.contains id then
            if Ruleset.hash (Rulesets.by_id id) <> Ruleset.hash ruleset then
                UpdateAvailable
            else
                UpToDate
        else
            NotInstalled

    do
        this
        |+ Text(ruleset.Name, Align = Alignment.LEFT, Position = Position.SliceTop(50.0f).Margin(10.0f, Style.PADDING))
        |+ Text(
            ruleset.Description,
            Align = Alignment.LEFT,
            Position = Position.TrimTop(40.0f).Margin(10.0f, Style.PADDING)
        )
        |* Clickable.Focus this

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    member this.Install() =
        match status with
        | UpToDate -> ()
        | UpdateAvailable ->
            Menu
                .ConfirmPage(
                    "Update this ruleset? (If you made changes yourself, they will be lost)",
                    fun () ->
                        Rulesets.install_or_update (id, ruleset)
                        status <- UpToDate
                )
                .Show()
        | NotInstalled ->
            Rulesets.install_or_update (id, ruleset)
            status <- UpToDate

    override this.Draw() =
        base.Draw()
        Draw.rect (this.Bounds.SliceTop(40.0f).SliceRight(300.0f).Shrink(20.0f, 0.0f)) Colors.shadow_2.O2

        Text.fill_b (
            Style.font
            , match status with
              | NotInstalled -> Icons.DOWNLOAD + " Install"
              | UpdateAvailable -> Icons.DOWNLOAD + " Update available"
              | UpToDate -> Icons.CHECK + " Installed"
            , this.Bounds.SliceTop(40.0f).SliceRight(300.0f).Shrink(25.0f, Style.PADDING)
            , match status with
              | NotInstalled -> if this.Focused then Colors.text_yellow_2 else Colors.text
              | UpdateAvailable -> Colors.text_yellow_2
              | UpToDate -> Colors.text_green
            , Alignment.CENTER
        )

    member this.Name = ruleset.Name

    static member Filter(filter: Filter) =
        fun (c: RulesetCard) ->
            List.forall
                (function
                | Impossible -> false
                | String str -> c.Name.ToLower().Contains(str)
                | _ -> true)
                filter

module Rulesets =

    type RulesetSearch() as this =
        inherit Container(NodeType.Container(fun _ -> Some this.Items))

        let grid =
            GridFlowContainer<RulesetCard>(80.0f, 2, Spacing = (15.0f, 15.0f), WrapNavigation = false)

        let scroll =
            ScrollContainer(grid, Margin = Style.PADDING, Position = Position.TrimTop(70.0f))

        let mutable loading = true
        let mutable failed = false

        override this.Init(parent) =
            WebServices.download_json (
                sprintf "https://raw.%s.com/YAVSRG/YAVSRG/main/backbeat/rulesets/rulesets.json" "githubusercontent",
                fun data ->
                    match data with
                    | Some(d: PrefabRulesets.Repo) ->
                        sync (fun () ->
                            for id in d.Rulesets.Keys do
                                grid.Add(RulesetCard(id, d.Rulesets.[id]))

                            loading <- false
                        )
                    | None ->
                        sync (fun () ->
                            failed <- true
                            loading <- false
                        )
            )

            this
            |+ (SearchBox(
                    Setting.simple "",
                    (fun (f: Filter) -> grid.Filter <- RulesetCard.Filter f),
                    Position = Position.SliceTop 60.0f
                )
                |+ LoadingIndicator.Border(fun () -> loading))
            |+ Conditional((fun () -> failed), EmptyState(Icons.X, "Couldn't connect to rulesets repository"))
            |* scroll

            base.Init parent

        override this.Focusable = grid.Focusable

        member this.Items = grid

    let tab = RulesetSearch()
