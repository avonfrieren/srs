using System.Collections.Generic;

namespace Celeste.Mod.SpeedrunSheet;

// Mod Options selection of the checkpoint whose sheet tiers the room timer
// will be compared against (phase 4). Checkpoints come from the imported
// sheet's checkpoint block, grouped by chapter; the selection is persisted by
// (chapter, checkpoint) name — checkpoint names alone repeat across chapters
// ("Start" in nearly all of them).
public static class SegmentSelector {
    // the currently selected checkpoint, or null if the selection does not
    // match the imported data (no data yet, or the sheet changed)
    public static SheetSegment Current {
        get {
            SheetBlock block = SheetImporter.Data?.CheckpointBlock;
            if (block == null) {
                return null;
            }

            SrsSettings settings = SrsModule.Settings;
            foreach (SheetSegment segment in block.Segments) {
                if (segment.Chapter == settings.SelectedChapter && segment.Name == settings.SelectedCheckpoint) {
                    return segment;
                }
            }

            return null;
        }
    }

    // two dependent sliders: picking a chapter rebuilds the checkpoint list.
    // the menu is rebuilt every time it is opened, so the entries pick up
    // freshly imported data on reopen
    public static void CreateMenuEntries(TextMenu menu) {
        SheetBlock block = SheetImporter.Data?.CheckpointBlock;
        if (block == null || block.Segments.Count == 0) {
            return;
        }

        SrsSettings settings = SrsModule.Settings;
        List<string> chapters = block.Chapters();
        int chapterIndex = chapters.IndexOf(settings.SelectedChapter);
        if (chapterIndex < 0) {
            chapterIndex = 0;
        }

        List<SheetSegment> checkpoints = block.Checkpoints(chapters[chapterIndex]);
        int checkpointIndex = 0;
        for (int i = 0; i < checkpoints.Count; i++) {
            if (checkpoints[i].Name == settings.SelectedCheckpoint) {
                checkpointIndex = i;
            }
        }

        // sync the settings with what the menu displays, so a stale selection
        // (sheet changed, first run) becomes valid as soon as the menu opens.
        // the room count is only pushed when the selection actually moved:
        // reopening the menu must not stomp a manual Number of Rooms override
        bool moved = settings.SelectedChapter != chapters[chapterIndex]
            || settings.SelectedCheckpoint != checkpoints[checkpointIndex].Name;
        settings.SelectedChapter = chapters[chapterIndex];
        settings.SelectedCheckpoint = checkpoints[checkpointIndex].Name;
        if (moved) {
            RoomCounts.Apply(checkpoints[checkpointIndex]);
        }

        TextMenu.Slider checkpointSlider = new(Dialog.Clean("SRS_CHECKPOINT"),
            i => checkpoints[i].Name, 0, checkpoints.Count - 1, checkpointIndex);
        checkpointSlider.Change(i => {
            settings.SelectedCheckpoint = checkpoints[i].Name;
            RoomCounts.Apply(checkpoints[i]);
        });

        TextMenu.Slider chapterSlider = new(Dialog.Clean("SRS_CHAPTER"),
            i => chapters[i], 0, chapters.Count - 1, chapterIndex);
        chapterSlider.Change(i => {
            settings.SelectedChapter = chapters[i];
            checkpoints = block.Checkpoints(chapters[i]);
            checkpointSlider.Values.Clear();
            for (int j = 0; j < checkpoints.Count; j++) {
                checkpointSlider.Add(checkpoints[j].Name, j, j == 0);
            }

            settings.SelectedCheckpoint = checkpoints[0].Name;
            RoomCounts.Apply(checkpoints[0]);
        });

        menu.Add(chapterSlider);
        menu.Add(checkpointSlider);
    }
}
