namespace Interlude.Features.Skins.Browser

open Percyqaz.Common
open Percyqaz.Flux.Windowing
open Percyqaz.Flux.UI
open Prelude
open Prelude.Skins.Repo
open Prelude.Data
open Interlude.UI

type SkinsBrowserPage() =
    inherit Page()

    let mutable loading = true
    let mutable error = false
    let mutable selected_group = None

    let noteskin_items =
        GridFlowContainer<GroupDisplay>(100.0f, 2, WrapNavigation = false, Spacing = (15.0f, 15.0f))

    let version_items =
        FlowContainer.Vertical<VersionDisplay>(520.0f, Spacing = 15.0f)

    let search_groups = 
        NavigationContainer.Column()
        |+ Dummy(NodeType.Leaf)
        |+ ScrollContainer(noteskin_items, Margin = Style.PADDING, Position = Position.ShrinkT(70.0f))
        |>> (fun nt -> Container(nt, Position = { Position.Shrink(PRETTY_MARGIN_X, PRETTY_MARGIN_Y) with Right = 0.65f %- 10.0f }))
        |+ (SearchBox(
                Setting.simple "",
                (fun (query: string) -> noteskin_items.Filter <- GroupDisplay.Filter query),
                Position = Position.SliceT 60.0f,
                Fill = K Colors.cyan.O3,
                Border = K Colors.cyan_accent,
                TextColor = K Colors.text_cyan
            )
            |+ LoadingIndicator.Border(fun () -> loading))
        |+ EmptyState(Icons.X, %"skins.browser.error").Conditional(fun () -> error)

    let pick_versions =
        ScrollContainer(version_items, Margin = Style.PADDING, Position = Position.ShrinkT(70.0f))
        |>> (fun nt -> Container(nt, Position = { Position.Shrink(PRETTY_MARGIN_X, PRETTY_MARGIN_Y) with Left = 0.65f %+ 10.0f }))
        |+ Text(%"skins.browser.install_hint", Color = K Colors.text_subheading, Align = Alignment.CENTER, Position = Position.SliceT(70.0f).Shrink(10.0f)).Conditional(fun () -> selected_group.IsSome)

    let select_group(group: SkinGroup) =
        selected_group <- Some group
        version_items.Clear()
        for version in group.Versions do
            version_items.Add <| VersionDisplay(group, version)

    member this.Refresh() =
        loading <- true
        error <- false
        noteskin_items.Clear()
        WebServices.download_json (
            sprintf "https://raw.%s.com/YAVSRG/YAVSRG/main/backbeat/skins/skins.json" "githubusercontent",
            fun data ->
                match data with
                | WebResult.Ok(d: SkinRepo) ->
                    GameThread.defer (fun () ->
                        for group in d.Skins do
                            let is_selected = Setting.make (fun _ -> select_group group) (fun _ -> selected_group = Some group)
                            noteskin_items.Add <| GroupDisplay(group, is_selected)
                        loading <- false
                    )
                | _ ->
                    GameThread.defer (fun () ->
                        error <- true
                        loading <- false
                    )
        )

    override this.Content() =

        this.Refresh()

        NavigationContainer.Row()
        |+ search_groups
        |+ pick_versions
        :> Widget

    override this.Title = %"skins.browser"

    override this.OnClose() = ()