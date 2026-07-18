# srs — Contexte & architecture

Mod Everest **compagnon** de [DemoJameson/Celeste.SpeedrunTool](https://github.com/DemoJameson/Celeste.SpeedrunTool) (MIT), frère de `srta` (même philosophie : dépendance + hooks, jamais de fork). But : importer la practice sheet communautaire (Google Sheets → CSV local) et colorer le temps final d'un segment du room timer selon les paliers de la sheet.
Roadmap dans `PLAN.md`. Le `README.md` sert de **changelog + instructions d'installation** — le mettre à jour à chaque feature.

## Build

- .NET 8 (`net8.0`), projet unique `srs.csproj`. Sur cette machine : `~/.dotnet/dotnet build -p:CelestePrefix="$HOME/.steam/steam/steamapps/common/Celeste"`.
- Références jeu résolues via `CelestePrefix` (auto-détecté si le repo est cloné dans `<Celeste>/Mods/xxx/`).
- **`SpeedrunTool.dll` extraite automatiquement du `SpeedrunTool.zip` officiel installé** (cible `ExtractSpeedrunToolDll`), épinglé sur **v3.27.16** dans `everest.yaml`. Pas de Publicizer pour l'instant (rien d'interne utilisé) — à ajouter comme dans srta si la phase 4 l'exige.
- Cible `OutputAsModStructure` : génère `build/` (DLL + PDB + `everest.yaml` + `Dialog/`) → à zipper en `srs.zip` dans `<Celeste>/Mods/` (comme `srta.zip`) ou copier en dossier `Mods/srs/`.

## Architecture

- `Source/SrsModule.cs` — `EverestModule` ; `Load()`/`Unload()` délèguent aux classes statiques ; `CreateModMenuSection` ajoute les entrées manuelles (bouton de mise à jour, plus tard les listes de sélection de segment — le menu auto-généré d'Everest ne sait pas faire de listes dynamiques).
- `Source/SrsSettings.cs` — settings Everest. `SheetUrl` (`[SettingIgnore]` : pas dans le menu, éditable dans `Saves/modsettings-srs.celeste`) : URL d'édition complète de la sheet, id/gid extraits par regex.
- `Source/SheetData.cs` — **pur, sans dépendance au jeu** (parseur testable) : parseur CSV RFC 4180 (guillemets, `""`, `\r\n`), parseur de temps `[hh:][mm:]ss[.fff]` (culture invariante), modèle `SheetData` → `SheetBlock` (une ligne d'en-tête = un bloc, détectée par `Hidden`+`WR` en colonnes 2-3 ; colonnes = noms de couleurs) → `SheetSegment` (temps `TimeSpan?` alignés sur les colonnes, null si cellule vide/illisible). Ne lève jamais d'exception sur contenu malformé.
- `Source/SheetImporter.cs` — téléchargement (HttpClient statique, timeout 20 s, **async via `Task.Run`, jamais sur le thread de jeu, jamais au lancement**), détection sheet privée (réponse HTML au lieu de CSV), cache `Saves/srs/sheet.csv` (`UserIO.SavePath`, écriture atomique via `.tmp` + `File.Move`), rechargé dans `Load()` ⇒ hors-ligne OK. Le cache **est** aussi le fallback manuel : un CSV déposé à la main y est relu tel quel. Entrées de menu : bouton (label muté depuis le thread worker — sûr, les items relisent leur label chaque frame) + `SubHeader` de statut. Logs sous le tag `srs`.
- `Dialog/English.txt` + `French.txt` — `MODOPTIONS_SRS_TITLE` (titre du menu auto) et clés `SRS_*` (entrées manuelles). Pas de placeholders `{0}` dans les dialogs (Celeste peut interpréter les accolades) : composer en code.

## Données de la sheet (audit 2026-07-18)

- Onglet « Celeste Any% Standars CP'S », `gid=639470957`. Export sans compte : `https://docs.google.com/spreadsheets/d/<id>/export?format=csv&gid=<gid>` (sheet « toute personne disposant du lien »).
- En-têtes : `Chapter/Chapter Times, Hidden, WR, Gold, Pink, Purple 1-3, Indigo 1-3, Blue 1-3, Cyan 1-3, Green 1-3, Olive 1-3, Yellow 1-3, Orange 1-3, Red 1-3, Unranked` — **les couleurs sont les noms de colonnes**, les cellules colorées de la sheet ne sont que cosmétiques pour nous.
- Bloc 1 (segments) : Prologue, 1a, 2a, 3a, 4a, 5a, 5b, 6a, `6a Tape`, 6b, 7a. Bloc 2 (« Chapter Times ») : idem + `5a+b`, `6a/6a+b`, `Filetime Buffer`, `Sob`. Lignes vides entre les deux.
- `Unranked` : colonne sans valeurs (palier « au-delà de Red 3 »). `Hidden` : `0:00.000` partout.
- Sémantique de comparaison (phase 4) : premier seuil ≥ temps réalisé ⇒ palier atteint.

## Repères SpeedrunTool (pour les phases 3-4)

- ModInterop `SpeedrunTool.RoomTimer` : `RoomTimerIsCompleted()`, `GetRoomTime()` — suffisant pour comparer un temps final ; préférer l'interop au Publicizer (stable entre releases ; DemoJameson accepte des ajouts sur demande Discord).
- ModInterop `SpeedrunTool.SaveLoad` : tout état statique muté en gameplay doit y être enregistré (`RegisterStaticTypes`) sous peine de desync des save states.
- HUD : dessiner après `orig` de `On.Celeste.SpeedrunTimerDisplay.Render` (srs charge après SpeedrunTool ⇒ ses hooks enveloppent les siens) — voir `RoomDeltas.cs` de srta pour le modèle.

## Checklist « nouvelle fonctionnalité »

1. Créer `Source/<Feature>.cs` statique avec `Load()`/`Unload()`, appelés depuis `SrsModule`.
2. Option de menu → settings + clés dialog EN **et** FR ; entrées dynamiques dans `CreateModMenuSection`.
3. Réseau → uniquement à la demande explicite de l'utilisateur, timeout, erreurs loggées jamais levées.
4. État statique muté en gameplay → interop `SpeedrunTool.SaveLoad`.
5. Build, zipper `build/` en `srs.zip` dans `<Celeste>/Mods/` ; tester en jeu.
6. Documenter dans le changelog du `README.md` (+ bump `Version` dans `everest.yaml` si release).

## Workflow git

Comme srta : branche `feature/<nom>` depuis `dev`, PR vers `dev`, release = merge dans `main` + bump de version mineure.
