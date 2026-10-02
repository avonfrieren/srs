using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// srs's own rebind screen, opened from Mod Options. Everest's key config screen
// is not usable here: it presents a binding as a list of alternatives, which is
// the opposite of what ComboHotkey reads, and it has no way to say "hold these
// together". Rows are added from one list, so a keyboard row cannot exist
// without its controller counterpart
internal sealed class KeybindConfigUi : TextMenu {
    // inserted at the head of a binding so the row reads "Ctrl + S" rather than
    // "S + Ctrl"; the order is cosmetic, the combo itself is a set
    private static readonly HashSet<Keys> ModifierKeys = [
        Keys.LeftShift, Keys.RightShift,
        Keys.LeftControl, Keys.RightControl,
        Keys.LeftAlt, Keys.RightAlt,
    ];

    // Keys.None is FNA's "no XNA key for this one" sentinel, reported held for
    // every unmappable key of the layout — binding it would fire the hotkey on
    // all of them. F1/F2/F3/F5 are Everest's own debug keys, and a mod that
    // steals them takes away the reload the player needs when a mod misbehaves
    private static readonly HashSet<Keys> DisallowedKeys = [
        Keys.None, Keys.F1, Keys.F2, Keys.F3, Keys.F5,
    ];

    private static readonly Buttons[] AllButtons = [
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.Back, Buttons.Start,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    ];

    // which binding each row edits, so clearing acts on the highlighted row
    // without counting menu items by hand
    private readonly Dictionary<Item, (ButtonBinding Binding, bool Keyboard)> rows = new();

    private bool closing;
    private float inputDelay;
    private bool remapping;
    private float remappingEase;
    private ButtonBinding remappingBinding;
    private bool remappingKeyboard;
    private string remappingLabel = "";
    private float timeout;

    public KeybindConfigUi() {
        Reload();
        OnESC = OnCancel = () => {
            Focused = false;
            closing = true;
        };
        MinWidth = 600f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    // the four hotkeys, in the order they are listed in both sections
    private static IEnumerable<(string LabelId, ButtonBinding Binding)> Slots() {
        SrsSettings settings = SrsModule.Settings;
        yield return ("MODOPTIONS_SRS_CYCLECATEGORY", settings.CycleCategory);
        yield return ("MODOPTIONS_SRS_TOGGLESHOWTIER", settings.ToggleShowTier);
        yield return ("MODOPTIONS_SRS_TOGGLESHOWSELECTION", settings.ToggleShowSelection);
        yield return ("MODOPTIONS_SRS_OPENEXPORTMENU", settings.OpenExportMenu);
    }

    private void Reload(int index = -1) {
        Clear();
        rows.Clear();

        Add(new Header(Dialog.Clean("SRS_KEYBINDS")));
        // what this screen does that Everest's does not, said here and not only
        // in the remap overlay: a player reading a two-key row has no way to
        // tell "both at once" from "either one" otherwise
        Add(new SubHeader(Dialog.Clean("SRS_KEYBIND_COMBO_SUB")));
        Add(new SubHeader(Dialog.Clean("SRS_KEYBIND_CLEAR_HINT"), topPadding: false));

        Add(new SubHeader(Dialog.Clean("SRS_KEYBIND_KEYBOARD")));
        foreach ((string labelId, ButtonBinding binding) in Slots()) {
            AddRow(labelId, binding, keyboard: true);
        }

        Add(new SubHeader(Dialog.Clean("SRS_KEYBIND_CONTROLLER")));
        foreach ((string labelId, ButtonBinding binding) in Slots()) {
            AddRow(labelId, binding, keyboard: false);
        }

        Add(new SubHeader(""));
        Add(new Button(Dialog.Clean("SRS_KEYBIND_RESET")) {
            IncludeWidthInMeasurement = false,
            AlwaysCenter = true,
            OnPressed = () => {
                foreach ((string _, ButtonBinding binding) in Slots()) {
                    binding.Keys.Clear();
                    binding.Buttons.Clear();
                }

                Save();
                Reload(Selection);
            },
        });

        if (index >= 0) {
            Selection = index;
        }
    }

    private void AddRow(string labelId, ButtonBinding binding, bool keyboard) {
        string label = Dialog.Clean(labelId);
        Setting row = keyboard ? new Setting(label, binding.Keys) : new Setting(label, binding.Buttons);
        row.Pressed(() => {
            remapping = true;
            remappingBinding = binding;
            remappingKeyboard = keyboard;
            remappingLabel = label;
            timeout = 5f;
            Focused = false;
        });

        Add(row);
        rows[row] = (binding, keyboard);
    }

    // a bound key pressed again is removed rather than added a second time,
    // which is also how a combo is taken apart one key at a time
    private void ApplyRemap<T>(T input, List<T> bound) {
        remapping = false;
        inputDelay = 0.25f;
        if (!bound.Remove(input)) {
            if (input is Keys key && ModifierKeys.Contains(key)) {
                bound.Insert(0, input);
            } else {
                bound.Add(input);
            }
        }

        Save();
        Reload(Selection);
    }

    private void ClearSelectedRow() {
        if (Selection < 0 || Selection >= Items.Count || !rows.TryGetValue(Items[Selection], out var row)) {
            return;
        }

        if (row.Keyboard) {
            row.Binding.Keys.Clear();
        } else {
            row.Binding.Buttons.Clear();
        }

        Save();
        Reload(Selection);
    }

    // the mod menu leaves saving to Everest, which writes when the menu closes;
    // nothing closes on a rebind's behalf. Resync goes with it — the key that
    // was just bound is still held when focus comes back
    private static void Save() {
        SrsModule.Instance.SaveSettings();
        Hotkeys.Resync();
    }

    public override void Update() {
        base.Update();

        if (inputDelay > 0f && !remapping) {
            inputDelay -= Engine.DeltaTime;
            if (inputDelay <= 0f) {
                Focused = true;
            }
        }

        remappingEase = Calc.Approach(remappingEase, remapping ? 1f : 0f, Engine.DeltaTime * 4f);

        if (remappingEase > 0.5f && remapping) {
            UpdateRemapping();
        } else if (!remapping && Focused
                   && (Input.MenuJournal.Pressed || MInput.Keyboard.Pressed(Keys.Delete) || MInput.Keyboard.Pressed(Keys.Back))) {
            // Focused is the whole guard: it is false while the overlay eases
            // in, through the input delay that follows a successful bind, and
            // while the screen closes. Without it, Delete pressed just after
            // binding a key wipes the row that was just set
            ClearSelectedRow();
        }

        Alpha = Calc.Approach(Alpha, closing ? 0f : 1f, Engine.DeltaTime * 8f);
        if (!closing || Alpha > 0f) {
            return;
        }

        // Close() invokes OnClose itself
        Close();
    }

    private void UpdateRemapping() {
        // ESC and the timeout only, never MenuCancel: it is bound to X and
        // Backspace on the keyboard and to B and X on the pad, all four of them
        // plausible hotkeys, and testing it here means they cancel instead of
        // binding. Vanilla's KeyboardConfigUI cancels on ESC alone for the same
        // reason; a pad-only player waits the five seconds out
        if (Input.ESC.Pressed || timeout <= 0f) {
            Input.ESC.ConsumePress();
            remapping = false;
            Focused = true;
            return;
        }

        if (remappingKeyboard) {
            Keys key = MInput.Keyboard.CurrentState.GetPressedKeys()
                .LastOrDefault(k => !DisallowedKeys.Contains(k));
            if (key != Keys.None && MInput.Keyboard.Pressed(key)) {
                ApplyRemap(key, remappingBinding.Keys);
            }
        } else {
            GamePadState current = MInput.GamePads[Input.Gamepad].CurrentState;
            GamePadState previous = MInput.GamePads[Input.Gamepad].PreviousState;
            foreach (Buttons button in AllButtons) {
                if (current.IsButtonDown(button) && !previous.IsButtonDown(button)) {
                    ApplyRemap(button, remappingBinding.Buttons);
                    break;
                }
            }
        }

        timeout -= Engine.DeltaTime;
    }

    public override void Render() {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
        if (remappingEase <= 0f) {
            return;
        }

        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * 0.95f * Ease.CubeInOut(remappingEase));
        Vector2 pos = new Vector2(1920f, 1080f) * 0.5f;

        if (!remappingKeyboard && !Input.GuiInputController()) {
            ActiveFont.Draw(Dialog.Clean("SRS_KEYBIND_NO_CONTROLLER"),
                pos, new Vector2(0.5f, 0.5f), Vector2.One,
                Color.White * Ease.CubeIn(remappingEase));
            return;
        }

        ActiveFont.Draw(Dialog.Clean("SRS_KEYBIND_COMBO_HINT"),
            pos + new Vector2(0f, -32f),
            new Vector2(0.5f, 2f), Vector2.One * 0.7f,
            Color.LightGray * Ease.CubeIn(remappingEase));
        ActiveFont.Draw(Dialog.Clean(remappingKeyboard ? "SRS_KEYBIND_PRESS_KEY" : "SRS_KEYBIND_PRESS_BUTTON"),
            pos + new Vector2(0f, -8f),
            new Vector2(0.5f, 1f), Vector2.One * 0.7f,
            Color.LightGray * Ease.CubeIn(remappingEase));
        ActiveFont.Draw(remappingLabel,
            pos + new Vector2(0f, 8f),
            new Vector2(0.5f, 0f), Vector2.One * 2f,
            Color.White * Ease.CubeIn(remappingEase));
    }
}
