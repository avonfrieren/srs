using System;
using FMOD.Studio;

namespace Celeste.Mod.SpeedrunSheet;

public class SrsModule : EverestModule {
    public static SrsModule Instance { get; private set; }

    public override Type SettingsType => typeof(SrsSettings);
    public static SrsSettings Settings => (SrsSettings)Instance._Settings;

    public SrsModule() {
        Instance = this;
    }

    public override void Load() {
        SheetImporter.Load();
    }

    public override void Unload() {
        SheetImporter.Unload();
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, EventInstance snapshot) {
        base.CreateModMenuSection(menu, inGame, snapshot);
        SheetImporter.CreateMenuEntries(menu);
    }
}
