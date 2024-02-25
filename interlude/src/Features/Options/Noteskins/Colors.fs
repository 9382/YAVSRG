﻿namespace Interlude.Features.OptionsMenu.Noteskins

open Prelude.Charts.Tools.NoteColors
open Percyqaz.Common
open Percyqaz.Flux.Graphics
open Percyqaz.Flux.Input
open Percyqaz.Flux.UI
open Prelude.Common
open Prelude.Data.Content
open Interlude.Content
open Interlude.Features
open Interlude.Options
open Interlude.Utils
open Interlude.UI
open Interlude.UI.Menu

type NoteColorPicker(color: Setting<byte>, style: ColorScheme, index: int) =
    inherit StaticContainer(NodeType.Leaf)

    let sprite = Content.Texture "note"
    let n = byte sprite.Rows

    let fd () =
        Setting.app (fun x -> (x + n - 1uy) % n) color
        Style.click.Play()

    let bk () =
        Setting.app (fun x -> (x + 1uy) % n) color
        Style.click.Play()

    override this.Init(parent: Widget) =
        this
        |+ Tooltip(
            Callout.Normal
                .Title(sprintf "%s: %O" (%"noteskins.edit.notecolors.name") style)
                .Body(%(sprintf "noteskins.edit.notecolors.%s.%i" (style.ToString().ToLower()) index))
        )
        |* Clickable.Focus this
        base.Init parent

    override this.OnSelected (by_mouse: bool) =
        base.OnSelected by_mouse
        if by_mouse then fd()

    override this.OnFocus (by_mouse: bool) =
        base.OnFocus by_mouse
        Style.hover.Play()

    override this.Draw() =
        base.Draw()

        if this.Selected then
            Draw.rect this.Bounds Colors.pink_accent.O2
        elif this.Focused then
            Draw.rect this.Bounds Colors.yellow_accent.O2

        Draw.quad this.Bounds.AsQuad (Quad.color Color.White) (Sprite.pick_texture (3, int color.Value) sprite)

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
    
    let keymode: Setting<Keymode> =
        Setting.simple <| Gameplay.Chart.keymode()

    let mutable note_colors = data.NoteColors

    let g keycount i =
        let k = if note_colors.UseGlobalColors then 0 else int keycount - 2
        Setting.make (fun v -> note_colors.Colors.[k].[i] <- v) (fun () -> note_colors.Colors.[k].[i])

    let NOTE_WIDTH = 120.0f

    let colors, refresh_colors =
        refreshable_row
            (fun () -> ColorScheme.count (int keymode.Value) note_colors.Style)
            (fun i k ->
                let x = -60.0f * float32 k
                let n = float32 i

                NoteColorPicker(
                    g keymode.Value i,
                    note_colors.Style,
                    i,
                    Position =
                        { Position.Default with
                            Left = 0.5f %+ (x + NOTE_WIDTH * n)
                            Right = 0.5f %+ (x + NOTE_WIDTH * n + NOTE_WIDTH)
                        }
                )
            )

    do
        this.Content(
            column ()
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
                .Pos(200.0f)
                .Tooltip(Tooltip.Info("noteskins.edit.globalcolors"))
            |+ PageSetting(
                "generic.keymode",
                Selector<Keymode>
                    .FromEnum(keymode |> Setting.trigger (ignore >> refresh_colors))
            )
                .Pos(270.0f)
            |+ PageSetting(
                "noteskins.edit.colorstyle",
                Selector.FromEnum(
                    Setting.make (fun v -> note_colors <- { note_colors with Style = v }) (fun () -> note_colors.Style)
                    |> Setting.trigger (ignore >> refresh_colors)
                )
            )
                .Pos(370.0f)
                .Tooltip(Tooltip.Info("noteskins.edit.colorstyle"))
            |+ PageSetting("noteskins.edit.notecolors", colors)
                .Pos(470.0f, Viewport.vwidth - 200.0f, NOTE_WIDTH)
        )

    override this.Title = %"noteskins.edit.colors.name"

    override this.OnClose() =
        Noteskins.save_config
            { Content.NoteskinConfig with
                NoteColors = note_colors
            }
