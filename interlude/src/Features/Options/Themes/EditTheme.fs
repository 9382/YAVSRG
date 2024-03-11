﻿namespace Interlude.Features.OptionsMenu.Themes

open Percyqaz.Common
open Interlude.Content
open Interlude.UI.Menu

type EditThemePage() as this =
    inherit Page()

    let data = Content.ThemeConfig

    let name = Setting.simple data.Name

    do this.Content(column () |+ PageTextEntry("themes.edittheme.themename", name).Pos(200.0f))

    override this.Title = data.Name

    override this.OnClose() =
        Themes.save_config { data with Name = name.Value }
