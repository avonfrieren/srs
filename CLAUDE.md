# srs — Contexte & architecture

Mod Everest **Speedrun Sheet** (`srs`), dépendant de SpeedrunTool : importe les temps de référence d'une practice sheet communautaire (Google Sheets → CSV local) et colore le temps final d'un segment du room timer selon les paliers de la sheet.
Le `README.md` sert de **changelog + notes d'utilisation** — le mettre à jour à chaque feature.

## Build

- .NET 8, projet unique `srs.csproj`. Sur cette machine : `~/.dotnet/dotnet build -p:CelestePrefix="$HOME/.steam/steam/steamapps/common/Celeste"` (préfixe auto-détecté si le repo est cloné dans `<Celeste>/Mods/xxx/`).
- `SpeedrunTool.dll` extraite automatiquement du `SpeedrunTool.zip` installé (cible `ExtractSpeedrunToolDll`), version épinglée dans `everest.yaml`. Pas de Publicizer : tout passe par le ModInterop et les classes publiques.
- Cible `OutputAsModStructure` : génère `build/` (DLL + PDB + `everest.yaml` + `Dialog/`) → **à zipper** dans `<Celeste>/Mods/`.
- `<Compile Remove="Tests/**" />` dans le csproj : **ne pas retirer** (le SDK globbe `**/*.cs`, sans ça les sources de test partiraient dans `srs.dll`).

## Tests

`cd Tests && ~/.dotnet/dotnet test` (xUnit). Le projet de test `<Compile Include>` directement les fichiers source game-free (`SheetData.cs`, `SegmentAutoDetect.Names.cs`) au lieu de référencer `srs.dll` ⇒ aucune dépendance à Celeste, et les membres `internal` sont visibles sans `InternalsVisibleTo`. **Corollaire : garder ces deux fichiers sans `using` du jeu.**

`SheetConsistencyTests` est le plus important : il croise les trois tables hardcodées (`SheetData.Import`, `CheckpointMap`, `CategoryVariants`) entre elles et avec les CSV réels de `Tests/Fixtures/` (snapshots de **structure**, pas de valeurs — les seuils bougent tout le temps). **Rafraîchir les fixtures est le mécanisme de détection** d'un renommage côté sheet : re-télécharger les onglets et relancer les tests dit exactement quelles lignes ont changé. Panne silencieuse typique : un segment qui n'est plus ancré sur aucun checkpoint jeu ⇒ plus de garde de départ ni de room de fin.

Le code couplé au jeu (hooks, menus, rendu) n'est pas testé unitairement — ses vrais bugs ne se révèlent qu'en jeu ⇒ vérification manuelle.

## Architecture

Le code porte ses propres justifications en commentaires ; ne sont notées ici que celles qui dépassent un fichier.

- `SrsModule.cs` — `EverestModule`. **L'ordre des `Load()` fixe l'ordre des hooks `Level.Update`** : chaque Load enveloppe les précédents, donc après `orig` la frame déroule du plus interne au plus externe — `RunWatcher` capture le run fini, `TierComparison` en calcule le palier, `SegmentAutoDetect` déplace la sélection en dernier.
- `SrsSettings.cs` — settings Everest. Les URLs des deux onglets sont `[SettingIgnore]` : éditables uniquement dans `Saves/modsettings-Speedrun Sheet.celeste`.
- `SheetData.cs` — **game-free** : parseur CSV RFC 4180, parseur de temps `[hh:][mm:]ss[.fff]` en culture invariante, modèle bloc/segment, table `Import` (whitelist + renommage), dérivations `CategoryOf`/`EndConditionOf`, `SegmentCategories`. Ne lève jamais d'exception sur contenu malformé.
- `SheetImporter.cs` — téléchargement des deux onglets + caches `Saves/srs/asides.csv` et `bsides.csv` (écriture atomique), rechargés au `Load()` ⇒ hors-ligne OK. Ces caches **sont** aussi le fallback manuel : des CSV déposés à la main y sont relus tels quels. Réseau uniquement sur le bouton, jamais au lancement, jamais sur le thread de jeu.
- `SegmentSelector.cs` — sliders Chapitre / Checkpoint / Catégorie + toggle Auto dans Mod Options. Le menu est reconstruit à chaque ouverture ⇒ les données fraîchement importées apparaissent à la réouverture.
- `SegmentAutoDetect.cs` (+ `.Names.cs`, game-free) — le checkpoint joué pilote la sélection. Trois tables hardcodées : `CheckpointMap` (nom jeu → nom sheet, **sans normalisation**, décision propriétaire), `StartRoomOverrides` et `UntimedSegmentHead` (rooms extraites des `.bin` de `Content/Maps/`, entités `checkpoint`). Les noms de checkpoints du jeu sont des clés dialog traduites ⇒ `Dialog.Clean` **forcé en anglais** partout.
- `RunWatcher.cs` — décide lui-même de la fin d'un run depuis l'`EndCondition` du segment ; SpeedrunTool n'est plus que le chronomètre (`NumberOfRooms` jamais touché). `StartRoomOf` sert à la fois d'ancre de départ et, via le checkpoint suivant, de room de fin ⇒ un segment finit exactement là où le suivant commence, jamais de recouvrement.
- `TierComparison.cs` — **toutes** les lignes que srs ajoute sous le timer, dessinées après `orig` de `SpeedrunTimerDisplay.Render` par un seul `DrawRow(row, …)` : ligne temps + palier, puis ligne grisée « catégorie - checkpoint » (la sélection armée). Les slots sont **fixes** — la ligne de sélection ne bouge pas quand le palier apparaît au-dessus (repère lu en cours de run), quitte à laisser le slot du palier vide. Le tout **décalé d'une ligne si srta est chargé**. Largeur du fond = la formule de SpeedrunTool (`60 + 18*(len-8)`), surtout pas la largeur mesurée du texte (le dégradé `strawberryCountBG` doit passer *sous* la fin du texte).
- `Dialog/English.txt` + `French.txt` — pas de placeholders `{0}` (Celeste peut interpréter les accolades) : composer en code.

## Données de la sheet

**La sheet n'est pas encore annoncée publiquement : ne pas la nommer dans les fichiers du repo.**

- Export sans compte (sheet « toute personne disposant du lien ») : `https://docs.google.com/spreadsheets/d/<id>/export?format=csv&gid=<gid>`. Id et gids des deux onglets importés (A-sides, B-sides) en dur dans `SrsSettings`, extraits par regex des URLs d'édition.
- Les colonnes **sont** les noms des paliers : `Chapter, Checkpoint, Hidden, WR, Gold, Pink, Purple 1-3, Indigo 1-3, Blue 1-3, Cyan 1-3, Green 1-3, Olive 1-3, Yellow 1-3, Orange 1-3, Red 1-3, Unranked` ⇒ `TierColors` est indexée par nom de colonne **complet**, le suffixe de rang compte (`Purple 1/2/3` = 3 teintes). Les cellules colorées sont cosmétiques.
- Le nom de chapitre n'est présent que sur la 1re ligne d'un groupe (cellules fusionnées) ⇒ reporté sur les suivantes au parsing.
- Blocs « Chapter Times » en bas de chaque onglet : colonne Checkpoint vide ⇒ ignorés (des `#REF!` y traînent).
- Variantes marquées par emoji (`💙` cœur, `📼` cassette, `💎` gem) à l'**espacement irrégulier** (`📼 RTM`, `📼Clear`) ⇒ dérivation par `Contains`. Seules les deux cassettes any% sont importées, la whitelist `Import` filtre le reste.
- `Hidden` vaut `0:00.000` partout, et certains `WR` aussi ⇒ ignorés par la comparaison (`threshold > TimeSpan.Zero`). `Unranked` est une colonne sans valeurs (palier « au-delà de Red 3 »).
- Sémantique : **premier seuil ≥ temps réalisé ⇒ palier atteint**.

## Repères SpeedrunTool

- ModInterop `SpeedrunTool.RoomTimer` : `GetRoomTime()`. Préférer l'interop au Publicizer (stable entre releases ; DemoJameson accepte des ajouts sur demande Discord).
- ModInterop `SpeedrunTool.SaveLoad` : **tout état statique muté en gameplay doit y être enregistré** (`RegisterStaticTypes`) sous peine de desync des save states.
- SpeedrunTool tourne *dans* `orig` de `Level.Update` (il charge en premier). Sa `RoomTimerManager.Timing()` fige l'affichage **puis** ajoute le delta de la frame ⇒ lire `GetRoomTime()` **avant `orig`** pour obtenir la valeur qu'il fige, et pas une frame de plus.
- HUD : dessiner après `orig` de `On.Celeste.SpeedrunTimerDisplay.Render`.

## Checklist « nouvelle fonctionnalité »

1. `Source/<Feature>.cs` statique avec `Load()`/`Unload()`, appelés depuis `SrsModule` (attention à l'ordre des hooks).
2. Option de menu → settings + clés dialog EN **et** FR ; entrées dynamiques dans `CreateModMenuSection`.
3. Réseau → uniquement à la demande explicite de l'utilisateur, timeout, erreurs loggées jamais levées.
4. État statique muté en gameplay → interop `SpeedrunTool.SaveLoad`.
5. Logique game-free → test dans `Tests/` ; toucher à l'une des trois tables hardcodées ⇒ `SheetConsistencyTests` doit toujours passer.
6. Build, zipper `build/` dans `<Celeste>/Mods/`, tester en jeu.
7. Changelog du `README.md` (+ bump `Version` dans `everest.yaml` si release).

## Workflow git

Branche `feature/<nom>` depuis `dev`, PR vers `dev`, release = merge dans `main` + bump de version mineure.

## Reste à faire

- **Import des autres catégories** (cœurs, gems, ILs, C-sides, Farewell). Le marqueur `📼` seul ne suffira plus à `CategoryOf`, et il faudra de nouvelles entrées `CategoryVariants`. Principe acté : **une catégorie ne contient jamais deux segments partant du même checkpoint jeu**, et les catégories plus larges se décrivent par ce qu'elles *ajoutent* (`True Ending` = any% + Core + Farewell ⇒ ne liste que Core et Farewell).
- **Route 5A complète** (Unravelling, Search, Rescue) : présente sur l'onglet, non importée — atteindre ces checkpoints ne bouge pas la sélection.
- **Confettis / PB SpeedrunTool** : plus déclenchés depuis que srs décide lui-même des fins de run (dette assumée, interop à demander à DemoJameson).

## Écarté définitivement

- **Sheets privées (OAuth / service account)** — credentials à stocker chez le joueur, API lourde et fragile. Le fallback est le dépôt manuel de CSV dans `Saves/srs/`.
- **Téléchargement automatique au lancement** — import uniquement à la demande, le cache local fait le reste.
- **Lister à la main les rooms du jeu** (~800) et le comptage de rooms en général — fragile à la route et incapable de distinguer les catégories ; remplacé par les conditions de fin déclaratives.
