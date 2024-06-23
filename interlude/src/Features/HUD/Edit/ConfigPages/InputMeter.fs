namespace Interlude.Features.Noteskins.Edit

open Percyqaz.Common
open Percyqaz.Flux.UI
open Prelude
open Prelude.Skinning.Noteskins
open Interlude.Content
open Interlude.UI.Menu
open Interlude.Options

type InputMeterPage(on_close: unit -> unit) =
    inherit Page()

    let user_options = options.HUD.Value
    let noteskin_options = Content.NoteskinConfig.HUD

    let scroll_speed = Setting.bounded user_options.InputMeterScrollSpeed 0.5f 3.0f
    let key_fade_time = Setting.bounded noteskin_options.InputMeterKeyFadeTime 0.0f 1000.0f
    let key_color = Setting.simple noteskin_options.InputMeterKeyColor

    let show_inputs = Setting.simple noteskin_options.InputMeterShowInputs
    let input_color = Setting.simple noteskin_options.InputMeterInputColor
    let input_fade_distance = Setting.bounded noteskin_options.InputMeterInputFadeDistance 0.0f 1000.0f
    let scroll_downwards = Setting.simple noteskin_options.InputMeterScrollDownwards
    
    override this.Content() =
        page_container()
        |+ PageSetting(%"hud.inputmeter.scroll_speed", Slider.Percent scroll_speed)
            .Pos(0)
        |+ ([
            PageSetting(%"hud.inputmeter.key_fade_time", Slider(key_fade_time, Step = 5f))
                .Pos(2)
            PageSetting(%"hud.inputmeter.key_color", ColorPicker(key_color, false))
                .Pos(4, 3)
            PageSetting(%"hud.inputmeter.scroll_downwards", Checkbox scroll_downwards)
                .Pos(7)
            PageSetting(%"hud.inputmeter.show_inputs", Checkbox show_inputs)
                .Pos(9)
            PageSetting(%"hud.inputmeter.input_color", ColorPicker(input_color, true))
                .Pos(11, 3)
                .Conditional(show_inputs.Get)
            PageSetting(%"hud.inputmeter.input_fade_distance", Slider(input_fade_distance, Step = 5f))
                .Pos(14)
                .Conditional(show_inputs.Get)
        ] |> or_require_noteskin)
        :> Widget

    override this.Title = %"hud.inputmeter"

    override this.OnClose() =
        options.HUD.Set
            { options.HUD.Value with
                InputMeterScrollSpeed = scroll_speed.Value
            }
        
        Noteskins.save_hud_config 
            { Content.NoteskinConfig.HUD with
                InputMeterKeyFadeTime = key_fade_time.Value
                InputMeterKeyColor = key_color.Value
                InputMeterShowInputs = show_inputs.Value
                InputMeterInputColor = input_color.Value
                InputMeterInputFadeDistance = input_fade_distance.Value
                InputMeterScrollDownwards = scroll_downwards.Value
            }
        on_close ()
