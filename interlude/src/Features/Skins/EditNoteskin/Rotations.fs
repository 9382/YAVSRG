namespace Interlude.Features.Skins.EditNoteskin

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
open Interlude.UI.Menu

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
            Position = Position.SliceBottom(30.0f),
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

type RotationSettingsPage() =
    inherit Page()

    let data = Content.NoteskinConfig
    let use_rotation = Setting.simple data.UseRotation

    let keymode: Setting<Keymode> = Setting.simple <| SelectedChart.keymode ()

    let receptor_style = Setting.simple data.ReceptorStyle
    let rotations = data.Rotations

    let g keycount i =
        let k = int keycount - 3

        Setting.make (fun v -> rotations.[k].[i] <- v) (fun () -> rotations.[k].[i])
        |> Setting.round 1

    let NOTE_SCALE = PRETTYHEIGHT * 1.5f - Style.PADDING * 2.0f

    let _rotations, refresh_rotations =
        refreshable_row
            (fun () -> int keymode.Value)
            (fun i k ->
                let x = NOTE_SCALE * -0.5f * float32 k
                let n = float32 i

                RotationPicker(
                    g keymode.Value i,
                    Position =
                        { Position.Default with
                            Left = 0.5f %+ (x + NOTE_SCALE * n)
                            Right = 0.5f %+ (x + NOTE_SCALE * n + NOTE_SCALE)
                        }
                )
            )

    override this.Content() =
        page_container()
        |+ PageSetting(%"noteskin.userotation", Checkbox use_rotation)
            .Help(Help.Info("noteskin.userotation"))
            .Pos(0)
        |+ PageSetting(
            %"generic.keymode",
            Selector.FromEnum(keymode |> Setting.trigger (ignore >> refresh_rotations))
        )
            .Pos(2)
        |+ PageSetting(%"noteskin.rotations", _rotations)
            .Pos(5, 3, PageWidth.Full)
        |+ PageSetting(
            %"noteskin.receptorstyle",
            SelectDropdown(
                [|
                    ReceptorStyle.Rotate, %"noteskin.receptorstyle.rotate"
                    ReceptorStyle.Flip, %"noteskin.receptorstyle.flip"
                |],
                receptor_style
            )
        )
            .Help(Help.Info("noteskin.receptorstyle"))
            .Pos(8)
        :> Widget

    override this.Title = %"noteskin.rotations"

    override this.OnClose() =
        Skins.save_noteskin_config
            { Content.NoteskinConfig with
                Rotations = rotations
                UseRotation = use_rotation.Value
                ReceptorStyle = receptor_style.Value
            }
