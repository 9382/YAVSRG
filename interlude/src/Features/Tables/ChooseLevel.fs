namespace Interlude.Features.Tables

open Percyqaz.Common
open Percyqaz.Flux.UI
open Percyqaz.Flux.Graphics
open Prelude
open Prelude.Backbeat
open Interlude.UI
open Interlude.UI.Menu

type SelectTableLevelPage(table: Table, action: int -> unit) =
    inherit Page()

    let container = FlowContainer.Vertical<Widget>(PRETTYHEIGHT)

    let refresh () =
        container.Clear()

        for level in table.Info.LevelDisplayNames.Keys do
            container |* PageButton(table.Info.LevelName level, (fun () -> action level), Icon = Icons.FOLDER)

        if container.Focused then
            container.Focus false

    override this.Content() =
        refresh ()
        ScrollContainer(container, Position = Position.Margin(100.0f, 200.0f))

    override this.Title = %"table"
    override this.OnClose() = ()
    override this.OnReturnTo() = refresh ()
