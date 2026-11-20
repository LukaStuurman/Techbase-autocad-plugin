# Overzicht aangeleverde Techbase 1.19.2-installatie

De aangeleverde ZIP is gecontroleerd als **binaire installatie**, niet als broncode.

| Bestand | SHA-256 |
| --- | --- |
| Techbase Blocks 1.19.2.zip | 324e4868211a1549ad03232b64f0c4c4ade583c634d024f38835d85613eb4226 |
| R25-1.19.2/Techbase.Blocks.dll | 6811cba78164af537e285ba54561b88973558134c829819662eaea25c60ab57c |
| R26-1.19.2/Techbase.Blocks.dll | 3633eaa8c8c2a28b57f3250eb865568191b207426a5af5813c376904a8b48f56 |
| Techbase.Kabelgeul.lsp | 948c25d1f986985037524e16ef49f3b7181757f965e1fec064cddc6ba6e138ec |

Manifest: `AppVersion="1.19.2"`, AutoCAD R25 en R26. Bronbestanden met extensie .cs, .csproj en .sln ontbreken in de ZIP.

De PDB documentlijst benoemt onder andere de ontbrekende bronbestanden
`BlockTemplate.cs`, `CabinetService.cs`, `EnexisCurrentLoadPanel.cs`,
`KabelgeulPlacement.cs`, `LsRackFuses.cs`, `OvRackChoice.cs`,
`UiTheme.cs`, `UiDiagnostics.cs` en `WorkspacePanel.cs`.
Die komen niet in de huidige GitHub 1.13.0-bronstructuur voor.

De 1.19.2-manifestcommands bevatten onder andere
`TBGEULAFSTAND`, `TBGEULVERPLAATS`, `TBLSREK`, `TBOVREK` en `TBTEMPLATE`.
Dit zijn belangrijke regressiecontroles bij een latere rebase.

De native Stationslocaties-integratie is eerst op de bestaande GitHub-bron uitgewerkt
en als draft-PR beschikbaar. Een complete 1.19.2-upgrade vereist originele
C#-bronnen of toegang tot de lokale werkmap waarin deze release is gebouwd.
