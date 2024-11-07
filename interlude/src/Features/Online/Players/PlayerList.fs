﻿namespace Interlude.Features.Online.Players

open Percyqaz.Flux.UI
open Interlude.UI

type PlayerListPage() =
    inherit Page()

    override this.Content() =
        NavigationContainer.Row()
        |+ PlayerListSidebar.create
            (
                { Position.DEFAULT with
                    Right = 0.35f %+ 40.0f
                }
                    .Shrink(40.0f)
        )
        |+ Profile(
            Position =
                { Position.DEFAULT with
                    Left = 0.35f %- 0.0f
                }
                    .Shrink(40.0f)
        )
        |+ HotkeyAction("exit", Menu.Back)
        |+ HotkeyAction("player_list", Menu.Back)
        :> Widget

    override this.OnClose() = ()
    override this.Title = ""
    override this.Header() = Dummy()