﻿namespace Interlude.Features.Noteskins.Edit

open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.UI
open Percyqaz.Flux.Input
open Prelude
open Prelude.Skinning
open Prelude.Skinning.Noteskins
open Interlude.Content
open Interlude.UI
open Interlude.UI.Menu

type private TextureEditGridItem(sprite: Sprite, x: int, y: int, selected: bool array array) =
    inherit
        Container(
            NodeType.Button(fun () ->
                Style.click.Play()
                selected.[x].[y] <- not selected.[x].[y]
            )
        )

    override this.Init(parent) =
        this
        |+ Frame(
            Fill =
                (fun () ->
                    if selected.[x].[y] then Colors.pink_accent.O2
                    elif this.Focused then Colors.yellow_accent.O2
                    else Color.Transparent
                ),
            Border =
                (fun () ->
                    if this.Focused then Colors.white.O3
                    elif selected.[x].[y] then Colors.pink_accent
                    else Color.Transparent
                )
        )
        |* Clickable(
            (fun () -> this.Select true),
            OnHover =
                fun b ->
                    if b then
                        if Mouse.held Mouse.LEFT then
                            this.Select true
                        else
                            this.Focus true
        )

        base.Init parent

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        base.Draw()
        Draw.quad this.Bounds.AsQuad Color.White.AsQuad (Sprite.pick_texture (x, y) sprite)

type private DeleteButton(on_click) =
    inherit Button(K Icons.TRASH, on_click, Floating = true)

    member val VerticalPad = 0.0f with get, set

    override this.Draw() =
        if this.Focused then
            Draw.rect (this.Bounds.Expand(0.0f, this.VerticalPad)) Colors.yellow_accent.O2

        base.Draw()

type TextureEditGrid(texture_id: string, rules: TextureRules) as this =
    inherit Container(NodeType.Container(fun () -> Some this.Items))

    let mutable sprite = Unchecked.defaultof<Sprite>
    let mutable selected: bool array array = [||]
    let mutable items: NavigationContainer.Grid = Unchecked.defaultof<_>

    member this.Refresh() =
        sprite <- Content.Texture texture_id

        if sprite.Columns <> selected.Length || sprite.Rows <> selected.[0].Length then
            selected <- Array.init sprite.Columns (fun _ -> Array.zeroCreate sprite.Rows)

        

        let item_height =
            min
                ((this.Bounds.Width - 10.0f * float32 (sprite.Columns - 1)) / float32 sprite.Columns / sprite.AspectRatio)
                ((this.Bounds.Height - 10.0f * float32 (sprite.Rows - 1)) / float32 sprite.Rows)
        let item_width = item_height * sprite.AspectRatio

        let grid_width =
            item_width * float32 sprite.Columns + 10.0f * float32 (sprite.Columns - 1)

        items <-
            NavigationContainer.Grid(
                WrapNavigation = false,
                Floating = true,
                Position = Position.Box(0.5f, 0.0f, -grid_width * 0.5f, 0.0f, grid_width, this.Bounds.Height)
            )

        let grid = NavigationContainer.Grid(WrapNavigation = false, Floating = true)

        for r = 0 to sprite.Rows - 1 do
            for c = 0 to sprite.Columns - 1 do

                grid.Add(
                    TextureEditGridItem(
                        sprite,
                        c,
                        r,
                        selected,
                        Position =
                            Position.Box(0.0f, 0.0f, float32 c * (item_width + 10.0f), float32 r * (item_height + 10.0f), item_width, item_height)
                    ),
                    c + 2,
                    r + 2
                )

                if r = 0 then
                    grid.Add(
                        Text(
                            K(sprintf "Frame %i" (c + 1)),
                            Color = K Colors.text_subheading,
                            Align = Alignment.CENTER,
                            Position = Position.Box(0.0f, 0.0f, float32 c * (item_width + 10.0f), -90.0f, item_width, 40.0f)
                        ),
                        c + 2,
                        0
                    )

                    if sprite.Columns > 1 then
                        grid.Add(
                            DeleteButton(
                                (fun () ->
                                    ConfirmPage(
                                        sprintf "Really PERMANENTLY delete animation frame %i?" (c + 1),
                                        fun () ->
                                            if Content.Noteskin.DeleteGridTextureColumn(c, texture_id) then
                                                Noteskins.reload_current ()
                                                this.Refresh()
                                    )
                                        .Show()
                                ),
                                Position = Position.Box(0.0f, 0.0f, float32 c * (item_width + 10.0f), -50.0f, item_width, 40.0f)
                            ),
                            c + 2,
                            1
                        )

            grid.Add(
                Text(
                    K(sprintf "Color %i" (r + 1)),
                    Color = K Colors.text_subheading,
                    Align = Alignment.RIGHT,
                    Position =
                        Position
                            .Box(0.0f, 0.0f, -250.0f, float32 r * (item_height + 10.0f), 200.0f, item_height)
                            .Margin(10.0f, item_height * 0.5f - 20.0f)
                ),
                0,
                r + 1
            )

            if sprite.Rows > 1 then
                grid.Add(
                    DeleteButton(
                        (fun () ->
                            ConfirmPage(
                                sprintf "Really PERMANENTLY delete color %i?" (r + 1),
                                fun () ->
                                    if Content.Noteskin.DeleteGridTextureRow(r, texture_id) then
                                        Noteskins.reload_current ()
                                        this.Refresh()
                            )
                                .Show()
                        ),
                        VerticalPad = item_height * 0.5f - 20.0f,
                        Position =
                            Position
                                .Box(0.0f, 0.0f, -50.0f, float32 r * (item_height + 10.0f), 40.0f, item_height)
                                .Margin(0.0f, item_height * 0.5f - 20.0f)
                    ),
                    1,
                    r + 2
                )

        items.Add(grid, 0, 0)

        if sprite.Rows < fst rules.MaxGridSize then
            items.Add(
                { new Button(K Icons.PLUS_CIRCLE,
                             (fun () ->
                                 let src_row =
                                     match Seq.tryHead this.SelectedTextures with
                                     | Some(x, y) -> y
                                     | None -> 0

                                 ConfirmPage(
                                     sprintf
                                         "Add a new color to this texture? (will be a copy of color %i)"
                                         (src_row + 1),
                                     fun () ->
                                         if Content.Noteskin.AddGridTextureRow(src_row, texture_id) then
                                             Noteskins.reload_current ()
                                             this.Refresh()
                                 )
                                     .Show()
                             ),
                             Floating = true,
                             Position = Position.Margin(0.0f, -50.0f).SliceBottom(40.0f)) with
                    override this.Draw() =
                        if this.Focused then
                            Draw.rect this.Bounds Colors.yellow_accent.O2

                        base.Draw()
                },
                0,
                1
            )

        if sprite.Columns < snd rules.MaxGridSize then
            items.Add(
                { new Button(K Icons.PLUS_CIRCLE,
                             (fun () ->
                                 let src_col =
                                     match Seq.tryHead this.SelectedTextures with
                                     | Some(x, y) -> x
                                     | None -> 0

                                 ConfirmPage(
                                     sprintf
                                         "Add a new animation frame to this texture? (will be a copy of frame %i)"
                                         (src_col + 1),
                                     fun () ->
                                         if Content.Noteskin.AddGridTextureColumn(src_col, texture_id) then
                                             Noteskins.reload_current ()
                                             this.Refresh()
                                 )
                                     .Show()
                             ),
                             Floating = true,
                             Position = Position.Margin(-50.0f, 0.0f).SliceRight(40.0f)) with
                    override this.Draw() =
                        if this.Focused then
                            Draw.rect this.Bounds Colors.yellow_accent.O2

                        base.Draw()
                },
                1,
                0
            )

        items.Init this

    member this.SelectedTextures =
        seq {
            for c = 0 to selected.Length - 1 do
                for r = 0 to selected.[c].Length - 1 do
                    if selected.[c].[r] then
                        yield (c, r)
        }

    member private this.Items = items

    override this.Init(parent) =
        base.Init parent
        this.Refresh()

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)
        items.Update(elapsed_ms, moved)

    override this.Draw() =
        base.Draw()
        items.Draw()

type TextureEditPage(texture_id: string) =
    inherit Page()

    let texture_rules = NoteskinTextureRules.get Content.NoteskinConfig texture_id

    let texture_editor =
        TextureEditGrid(
            texture_id,
            texture_rules,
            Position = Position.Box(0.5f, 0.0f, -375.0f, 200.0f, 750.0f, 750.0f)
        )

    override this.Content() =
        Content.Noteskin.SplitTexture(texture_id)

        NavigationContainer.Column()
        |+ texture_editor
        |+ (FlowContainer.Vertical(45.0f, Spacing = 15.0f, Position = Position.SliceRight(400.0f).Margin(50.0f))
            |+ Button(
                Icons.ROTATE_CW + " Rotate clockwise"
                , fun () ->
                    for (col, row) in texture_editor.SelectedTextures do
                        Content.Noteskin.RotateClockwise((col, row), texture_id) |> ignore

                    Noteskins.reload_current ()
                    texture_editor.Refresh()
                , Disabled = fun () -> texture_editor.SelectedTextures |> Seq.isEmpty
            )
            |+ Button(
                Icons.ROTATE_CCW + " Rotate anticlockwise"
                , fun () ->
                    for (col, row) in texture_editor.SelectedTextures do
                        Content.Noteskin.RotateAnticlockwise((col, row), texture_id) |> ignore

                    Noteskins.reload_current ()
                    texture_editor.Refresh()
                , Disabled = fun () -> texture_editor.SelectedTextures |> Seq.isEmpty
            )
            |+ Button(
                Icons.CORNER_LEFT_UP + " Vertical flip"
                , fun () ->
                    for (col, row) in texture_editor.SelectedTextures do
                        Content.Noteskin.VerticalFlipTexture((col, row), texture_id) |> ignore

                    Noteskins.reload_current ()
                    texture_editor.Refresh()
                , Disabled = fun () -> texture_editor.SelectedTextures |> Seq.isEmpty
            )
            |+ Button(
                Icons.CORNER_DOWN_LEFT + " Horizontal flip"
                , fun () ->
                    for (col, row) in texture_editor.SelectedTextures do
                        Content.Noteskin.HorizontalFlipTexture((col, row), texture_id) |> ignore

                    Noteskins.reload_current ()
                    texture_editor.Refresh()
                , Disabled = fun () -> texture_editor.SelectedTextures |> Seq.isEmpty
            ))
        :> Widget

    override this.Title = "Texture: " + texture_id
    override this.OnClose() = ()