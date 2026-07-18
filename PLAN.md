# srs — Plan d'implémentation

Feuille de route validée (2026-07-18). Projet issu du point 8 (phase 3) du `PLAN.md` de srta : les temps colorés par comparaison deviennent un mod à part entière, connecté à SpeedrunTool (dépendance, comme srta).
But : importer la practice sheet (Google Sheets → CSV local), comparer le temps final d'un segment du room timer de SpeedrunTool aux seuils de la sheet, et afficher la couleur du palier atteint.
Voir `CLAUDE.md` pour l'architecture et les décisions techniques.

## Phase 0 — Prérequis — ✅ faite (2026-07-18)

1. ~~**Accord du créateur de la sheet**~~ — condition héritée du PLAN.md de srta, confirmée par le propriétaire du projet.
2. ~~**Audit de la structure de la sheet**~~ — onglet « Celeste Any% Standars CP'S » (`gid=639470957`) : les paliers sont des **colonnes nommées par couleur** (Hidden, WR, Gold, Pink, Purple 1-3, Indigo 1-3, Blue 1-3, Cyan 1-3, Green 1-3, Olive 1-3, Yellow 1-3, Orange 1-3, Red 1-3, Unranked) → l'export CSV suffit, **pas besoin de xlsx**. Deux blocs : segments (Prologue, 1a…7a, dont `5b` et `6a Tape`) puis « Chapter Times » (+ `Filetime Buffer`, `Sob`). Formats de temps mixtes : `28`, `28.1`, `00:56`, `1:05.5`, `24:06.802`.
3. **Mapping paliers → couleurs RGB** — les noms de colonnes donnent la couleur ; les valeurs RGB exactes seront saisies à la main plus tard (phase 4).

## Phase 1 — Squelette du mod — ✅ faite (v0.1.0)

4. ~~**Repo `srs` sur le modèle de srta**~~ — `srs.csproj` (extraction de `SpeedrunTool.dll` du zip installé, cible `OutputAsModStructure`), `everest.yaml` (dépendances Everest + SpeedrunTool 3.27.16), `SrsModule`, `SrsSettings`, `Dialog/` EN+FR. Pas de Publicizer pour l'instant (aucun internal SpeedrunTool utilisé ; à ajouter en phase 4 si l'interop ne suffit pas).

## Phase 2 — Import & données — ✅ faite (v0.1.0)

5. ~~**Téléchargement CSV**~~ — GET HTTP sur `https://docs.google.com/spreadsheets/d/<id>/export?format=csv&gid=<gid>` (sheet publique « toute personne disposant du lien » : aucun compte, aucune clé, aucun credential stocké). URL configurable (`SheetUrl` dans les settings, `[SettingIgnore]`), id/gid extraits par regex.
6. ~~**Parseur & modèle**~~ — parseur CSV RFC 4180 + parseur de temps `[hh:][mm:]ss[.fff]` ; modèle `SheetData` → blocs → segments → temps alignés sur les colonnes de couleur. Jamais d'exception sur contenu malformé (cellules illisibles ⇒ null).
7. ~~**Cache local & fallback manuel**~~ — CSV cachés dans `Saves/srs/sheet.csv` (écriture atomique), rechargé au démarrage ⇒ le mod marche hors-ligne. Déposer un CSV exporté à la main au même endroit = import manuel (cas sheet privée). Bouton « Mettre à jour » dans Mod Options (téléchargement async, jamais au lancement du jeu), statut (nb de segments + date) affiché sous le bouton, erreurs dans log.txt.

## Phase 3 — Sélection du segment

8. **Menu de sélection** — choisir le segment à comparer parmi les données importées. La sheet étant organisée par chapitre (1a, 2a, … + `6a Tape`), un **sélecteur de segment unique** colle mieux aux données que le triptyque chapitre/side/checkpoint de la maquette — à trancher à ce moment-là. Auto-détection chapitre+side depuis `Session.Area` avec override manuel ; sélection persistée dans les settings. Menu construit dans `CreateModMenuSection` (listes dynamiques issues du CSV).

## Phase 4 — Comparaison & affichage

9. **Comparaison au timer SpeedrunTool** — à `RoomTimerIsCompleted()` (ModInterop `SpeedrunTool.RoomTimer`, plus stable que le Publicizer), comparer `GetRoomTime()` aux seuils du segment sélectionné : premier seuil ≥ temps ⇒ palier atteint ; au-delà de Red 3 ⇒ Unranked.
10. **HUD couleur** — afficher le nom du palier dans sa couleur près du timer (dessin après `orig` de `SpeedrunTimerDisplay.Render`, comme les deltas de srta). Saisie du mapping couleurs RGB. Respecter la checklist srta : état statique enregistré via l'interop `SpeedrunTool.SaveLoad`.

## Phase 5 — Finitions

11. **Hotkey toggle de l'affichage**, textes EN/FR complets, README changelog, release v1.0.0.

## Écarté

- **Sheets privées (OAuth / service account)** — hors périmètre définitif : credentials à stocker chez le joueur, API lourde et fragile. Le fallback = export CSV manuel déposé dans `Saves/srs/`.
- **Téléchargement automatique au lancement** — refusé : import uniquement à la demande (bouton), le cache local fait le reste.
