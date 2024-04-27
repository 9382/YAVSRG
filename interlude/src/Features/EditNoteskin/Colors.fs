﻿namespace Interlude.Features.EditNoteskin

open Prelude.Charts.Processing
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude
open Prelude.Skinning.Noteskins
open Interlude.Content
open Interlude.Features
open Interlude.Options
open Interlude.UI
open Interlude.UI.Menu

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
        |+ Tooltip(
            Callout.Normal
                .Title(sprintf "%s: %O" (%"noteskins.edit.notecolors.name") style)
                .Body(%(sprintf "noteskins.edit.notecolors.%s.%i" (style.ToString().ToLower()) index))
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

type ColorSettingsPage() as this =
    inherit Page()

    let data = Content.NoteskinConfig

    let keymode: Setting<Keymode> = Setting.simple <| Gameplay.Chart.keymode ()

    let mutable note_colors = data.NoteColors

    let g keycount i =
        let k = if note_colors.UseGlobalColors then 0 else int keycount - 2
        Setting.make (fun v -> note_colors.Colors.[k].[i] <- v) (fun () -> note_colors.Colors.[k].[i])

    let NOTE_SCALE = PRETTYHEIGHT * 1.5f - Style.PADDING * 2.0f

    let colors, refresh_colors =
        refreshable_row
            (fun () -> ColorScheme.count (int keymode.Value) note_colors.Style)
            (fun i k ->
                let x = NOTE_SCALE * -0.5f * float32 k
                let n = float32 i

                NoteColorPicker(
                    g keymode.Value i,
                    note_colors.Style,
                    i,
                    Position =
                        { Position.Default with
                            Left = 0.5f %+ (x + NOTE_SCALE * n)
                            Right = 0.5f %+ (x + NOTE_SCALE * n + NOTE_SCALE)
                        }
                )
            )

    do
        this.Content(
            page_container()
            |+ PageSetting(
                "noteskins.edit.globalcolors",
                Selector<_>
                    .FromBool(
                        Setting.make
                            (fun v -> note_colors <- { note_colors with UseGlobalColors = v })
                            (fun () -> note_colors.UseGlobalColors)
                        |> Setting.trigger (ignore >> refresh_colors)
                    )
            )
                .Pos(0)
                .Tooltip(Tooltip.Info("noteskins.edit.globalcolors"))
            |+ PageSetting(
                "generic.keymode",
                Selector<Keymode>
                    .FromEnum(keymode |> Setting.trigger (ignore >> refresh_colors))
            )
                .Pos(2)
            |+ PageSetting(
                "noteskins.edit.colorstyle",
                Selector.FromEnum(
                    Setting.make (fun v -> note_colors <- { note_colors with Style = v }) (fun () -> note_colors.Style)
                    |> Setting.trigger (ignore >> refresh_colors)
                )
            )
                .Pos(5)
                .Tooltip(Tooltip.Info("noteskins.edit.colorstyle"))
            |+ PageSetting("noteskins.edit.notecolors", colors)
                .Pos(8, 3, PageWidth.Full)
        )

    override this.Title = %"noteskins.edit.colors.name"

    override this.OnClose() =
        Noteskins.save_config
            { Content.NoteskinConfig with
                NoteColors = note_colors
            }
