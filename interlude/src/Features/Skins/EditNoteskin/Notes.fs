namespace Interlude.Features.Skins.EditNoteskin

open Prelude.Charts.Processing
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude
open Prelude.Skins.Noteskins
open Interlude.Content
open Interlude.Features.Gameplay
open Interlude.Options
open Interlude.UI

type RotationPicker(rotation: Setting<float>) as this =
    inherit Container(NodeType.Leaf)

    let sprite = Content.Texture "note"

    let fd () =
        Setting.app (fun x -> (x + 22.5) % 360.0) rotation
        Style.click.Play()

    let bk () =
        Setting.app (fun x -> (x - 22.5) %% 360.0) rotation
        Style.click.Play()

    do
        this
        |+ Text(
            (fun () -> sprintf "%.1f" rotation.Value),
            Position = Position.SliceB(30.0f),
            Align = Alignment.LEFT,
            Color = K Colors.text_subheading
        )
        |* Clickable(
            (fun () ->
                if not this.Selected then
                    this.Select true

                fd ()
            ),
            OnHover =
                (fun b ->
                    if b && not this.Focused then
                        this.Focus true
                    elif not b && this.FocusedByMouse then
                        Selection.up true
                ),
            OnRightClick =
                fun () ->
                    if not this.Selected then
                        this.Select true

                    bk ()
        )

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        if this.Selected then
            Draw.rect this.Bounds Colors.pink_accent.O2
        elif this.Focused then
            Draw.rect this.Bounds Colors.yellow_accent.O2

        Draw.quad
            (this.Bounds.AsQuad |> Quad.rotate rotation.Value)
            Color.White.AsQuad
            (Sprite.pick_texture (3, 0) sprite)

        base.Draw()

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        if this.Selected then
            if (%%"up").Tapped() then
                fd ()
            elif (%%"down").Tapped() then
                bk ()
            elif (%%"left").Tapped() then
                bk ()
            elif (%%"right").Tapped() then
                fd ()

type NoteColorPicker(color: Setting<byte>, style: ColorScheme, index: int) =
    inherit Container(NodeType.Leaf)

    let sprite = Content.Texture "note"
    let n = byte sprite.Rows

    let fd () =
        Setting.app (fun x -> (x + 1uy) % n) color
        Style.click.Play()

    let bk () =
        Setting.app (fun x -> (x + n - 1uy) % n) color
        Style.click.Play()

    override this.Init(parent: Widget) =
        this
        |+ Help(
            Callout.Normal
                .Title(sprintf "%s: %O" (%"noteskin.notecolors") style)
                .Body(%(sprintf "noteskin.notecolors.%s.%i" (style.ToString().ToLower()) index))
        )
        |* Clickable(
            (fun () ->
                if not this.Selected then
                    this.Select true

                fd ()
            ),
            OnHover =
                (fun b ->
                    if b && not this.Focused then
                        this.Focus true
                    elif not b && this.FocusedByMouse then
                        Selection.up true
                ),
            OnRightClick =
                fun () ->
                    if not this.Selected then
                        this.Select true

                    bk ()
        )

        base.Init parent

    override this.OnFocus(by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        base.Draw()

        if this.Selected then
            Draw.rect this.Bounds Colors.pink_accent.O2
        elif this.Focused then
            Draw.rect this.Bounds Colors.yellow_accent.O2

        Draw.quad this.Bounds.AsQuad Color.White.AsQuad (Sprite.pick_texture (3, int color.Value) sprite)

    override this.Update(elapsed_ms, moved) =
        base.Update(elapsed_ms, moved)

        if this.Selected then
            if (%%"up").Tapped() then
                fd ()
            elif (%%"down").Tapped() then
                bk ()
            elif (%%"left").Tapped() then
                bk ()
            elif (%%"right").Tapped() then
                fd ()

type NotesSettingsPage() =
    inherit Page()

    let data = Content.NoteskinConfig

    let use_rotation = Setting.simple data.UseRotation

    let keymode: Setting<Keymode> = Setting.simple <| SelectedChart.keymode ()
    let note_rotations = data.Rotations
    let mutable note_colors = data.NoteColors

    let rotate_picker keycount i =
        let k = int keycount - 3

        Setting.make (fun v -> note_rotations.[k].[i] <- v) (fun () -> note_rotations.[k].[i])
        |> Setting.round 1


    let color_picker keycount i =
        let k = if note_colors.UseGlobalColors then 0 else int keycount - 2
        Setting.make (fun v -> note_colors.Colors.[k].[i] <- v) (fun () -> note_colors.Colors.[k].[i])

    let NOTE_SCALE = PRETTYHEIGHT * 1.5f - Style.PADDING * 2.0f

    let rotations, refresh_rotations =
        refreshable_row
            (fun () -> int keymode.Value)
            (fun i k ->
                let x = NOTE_SCALE * -0.5f * float32 k
                let n = float32 i

                RotationPicker(
                    rotate_picker keymode.Value i,
                    Position =
                        { Position.DEFAULT with
                            Left = 0.5f %+ (x + NOTE_SCALE * n)
                            Right = 0.5f %+ (x + NOTE_SCALE * n + NOTE_SCALE)
                        }
                )
            )

    let colors, refresh_colors =
        refreshable_row
            (fun () -> ColorScheme.count (int keymode.Value) note_colors.Style)
            (fun i k ->
                let x = NOTE_SCALE * -0.5f * float32 k
                let n = float32 i

                NoteColorPicker(
                    color_picker keymode.Value i,
                    note_colors.Style,
                    i,
                    Position =
                        { Position.DEFAULT with
                            Left = 0.5f %+ (x + NOTE_SCALE * n)
                            Right = 0.5f %+ (x + NOTE_SCALE * n + NOTE_SCALE)
                        }
                )
            )

    override this.Content() =
        page_container()
        |+ PageSetting(
            %"generic.keymode",
            Selector.FromEnum(keymode |> Setting.trigger (fun _ -> refresh_colors(); refresh_rotations()))
        )
            .Pos(0)
        |+ PageSetting(
            %"noteskin.globalcolors",
            Checkbox(
                Setting.make
                    (fun v -> note_colors <- { note_colors with UseGlobalColors = v })
                    (fun () -> note_colors.UseGlobalColors)
                |> Setting.trigger (ignore >> refresh_colors)
            )
        )
            .Help(Help.Info("noteskin.globalcolors"))
            .Pos(3)
        |+ PageSetting(
            %"noteskin.colorstyle",
            SelectDropdown(
                [|
                    ColorScheme.Column, %"noteskin.colorstyle.column"
                    ColorScheme.Chord, %"noteskin.colorstyle.chord"
                    ColorScheme.DDR, %"noteskin.colorstyle.ddr"
                    ColorScheme.Density, %"noteskin.colorstyle.density"
                |],
                Setting.make (fun v -> note_colors <- { note_colors with Style = v }) (fun () -> note_colors.Style)
                |> Setting.trigger (ignore >> refresh_colors)
            )
        )
            .Help(Help.Info("noteskin.colorstyle"))
            .Pos(5)
        |+ PageSetting(%"noteskin.notecolors", colors)
            .Pos(7, 3, PageWidth.Full)
        |+ PageSetting(%"noteskin.userotation", Checkbox use_rotation)
            .Help(Help.Info("noteskin.userotation"))
            .Pos(10)
        |+ PageSetting(%"noteskin.rotations", rotations)
            .Pos(12, 3, PageWidth.Full)
        :> Widget

    override this.Title = %"noteskin.notes"

    override this.OnClose() =
        Skins.save_noteskin_config
            { Content.NoteskinConfig with
                NoteColors = note_colors
                Rotations = note_rotations
                UseRotation = use_rotation.Value
            }