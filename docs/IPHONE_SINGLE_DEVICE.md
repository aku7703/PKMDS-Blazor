# PKMDS: iPhone single-device fork

This fork of [PKMDS](https://github.com/codemonkey85/PKMDS-Blazor) is for playing every pre-Switch Pokémon game (Gen 1–7, Colosseum/XD, Battle Revolution) in emulators on **one iPhone**. It covers the content that normally needs a second console or player, with no computer needed after setup.

It runs as a Home Screen web app in iOS Safari and works offline after the first load. All save editing happens on the phone.

Live build: **https://pkhex.aniketc.tech** (deployed to Netlify with [`deploy-netlify.ps1`](../deploy-netlify.ps1)).

## What this fork adds

| Area | Feature | Where |
|---|---|---|
| All gens | **Trade evolutions in the cross-save Trade tab.**<br>• Plain trade, held-item trade, and Karrablast ↔ Shelmet.<br>• An Everstone blocks evolution, and FRLG respects its National Dex gate.<br>• The held item is consumed, and the result must still pass legality. | `Pkmds.Core/Utilities/TradeEvolutionHelper.cs`, `TradeTab.razor.cs` |
| Gen 5 | Entralink levels and EXP, Pass Powers, Funfest missions, Entrée Forest areas and slots, Liberty Pass, B2W2 Key System, Join Avenue, Medals | `Trainer/TrainerInfoSav5Section` |
| Gen 4 | HGSS Pokéwalker steps, watts and courses | `Trainer/TrainerInfoSav4HgssSection` |
| Gen 6 | XY Friend Safari slot reveal (only covers friends already in the save) | `Trainer/TrainerInfoSav6XySection` |
| Gen 7 | Festival Plaza rank | `Trainer/TrainerInfoSav7FestaSection` |
| Gen 3–7 | Searchable event flag / event work editor, with filters for tickets, travel and key events (Eon, Mystic, Aurora, Old Sea Map…) | `Dialogs/EventFlagsDialog` |

**Known gaps:**
- Black City / White Forest resident rosters can't be edited; only the level can.
- Friend Safari friend entries can't be created.
- BW2 Memory Link isn't covered.

## The one-phone setup

| Need | App |
|---|---|
| Gen 1–3 play plus same-phone link cable (GB↔GBC, GBA↔GBA) | Afterplay (App Store, free link) |
| GB/GBA/DS/N64 with saves visible in Files | RetroArch (App Store) |
| DS online: GTS / Mystery Gift through Pokémon Classic Network / Kaeru WFC | Delta (App Store) |
| 3DS, GameCube, Wii | Manic EMU (App Store); DolphiniOS (sideloaded) as a fallback |
| JIT without a PC | StikDebug + LocalDevVPN, installed through SideStore (iloader) |
| Save editing, trading between your own saves, events, conversions | This app |

**Workflow:** export the save from the emulator → back it up → open it in this app → edit or trade → export → import it back into the emulator.

**Self-trading by generation:**
- **Gen 1–3:** Afterplay's link cable.
- **Gen 4–5:** GTS between your own games, or this app's Trade tab.
- **Gen 6–7:** this app's Trade tab only, since no iOS 3DS emulator links two instances.

## Building and deploying

Requirements: .NET SDK 10.0.401 (see `global.json`) and the `wasm-tools` workload.

```powershell
dotnet build Pkmds.Web/Pkmds.Web.csproj -c Debug     # warnings are errors
./deploy-netlify.ps1 -SiteId <netlify-site-id>         # draft deploy
./deploy-netlify.ps1 -SiteId <netlify-site-id> -Prod   # production
```

The script publishes the app, stamps the service-worker cache version (CI does this upstream), removes the precompressed `.br`/`.gz` copies (Netlify compresses on the fly), and writes `_headers` and `_redirects`.

## License

GPL-3.0, the same as upstream PKMDS and PKHeX.Core. Upstream credit goes to codemonkey85 (PKMDS) and kwsch (PKHeX).
