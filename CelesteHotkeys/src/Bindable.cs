using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.CelesteHotkeys;

/// <summary>What the remap screen may record, and the rules it records by. No MInput, no Engine.</summary>
internal static class Bindable {
    /// <summary>Whether a key can be bound at all.</summary>
    // ⚠️ Keys.None is not "no key". FNA returns it for any key absent from its SDL→XNA table — most
    // of AZERTY's digit row, and ")" — and reports it held, so a binding carrying it fires on all of
    // them at once.
    //
    // F1, F2, F3 and F5 are Everest's debug keys, which is what a player reaches for when a mod
    // misbehaves; a hotkey on one would fire every time they did.
    internal static bool IsBindable(Keys key) =>
        key != Keys.None && key != Keys.F1 && key != Keys.F2 && key != Keys.F3 && key != Keys.F5;

    internal enum KeyPress {
        /// <summary>No key went down this frame.</summary>
        Nothing,
        /// <summary>A bindable key went down; it is the one to record.</summary>
        Bindable,
        /// <summary>Only keys that cannot be bound went down. Say so rather than stay silent.</summary>
        Refused,
    }

    /// <summary>The key a remap should record this frame.</summary>
    /// <param name="held">Every key held now — <c>KeyboardState.GetPressedKeys()</c>.</param>
    /// <param name="newlyPressed">Whether a key went down this frame — <c>MInput.Keyboard.Pressed</c>.</param>
    // ⚠️ The first NEWLY PRESSED key, never the last HELD one. GetPressedKeys comes back in ascending
    // Keys order and the modifiers are 160..165, above every letter, digit, arrow and F-key, so taking
    // the last held key named a Shift still down from the confirm press rather than the letter just
    // pressed: nothing bound, and the screen timed out five seconds later with no message.
    //
    // The edge test is a parameter so this stays a pure function with a unit test.
    internal static KeyPress ReadKeyPress(Keys[] held, Func<Keys, bool> newlyPressed, out Keys key) {
        key = Keys.None;
        if (held is null) return KeyPress.Nothing;

        bool refused = false;
        foreach (Keys candidate in held) {
            if (!newlyPressed(candidate)) continue;
            if (!IsBindable(candidate)) {
                // A None edge is an unmappable key; saying "refused" for it is still more useful than
                // silence, since the player did press something.
                refused = true;
                continue;
            }
            key = candidate;
            return KeyPress.Bindable;
        }
        return refused ? KeyPress.Refused : KeyPress.Nothing;
    }

    /// <summary>Every button the remap screen listens for, in the order it prefers them.</summary>
    internal static readonly Buttons[] RecordableButtons = {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.LeftTrigger, Buttons.RightTrigger,
        Buttons.Back, Buttons.Start,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    };

    /// <summary>The first button that went down between two pad states, or null.</summary>
    internal static Buttons? NewlyPressedButton(in GamePadState current, in GamePadState previous) {
        foreach (Buttons button in RecordableButtons) {
            if (current.IsButtonDown(button) && !previous.IsButtonDown(button)) return button;
        }
        return null;
    }

    /// <summary>The most inputs one binding may hold. Every one must be held for the combo to fire.</summary>
    // More than a hand holds at once is not a combo anyone can press, and the cap also bounds how wide
    // the remap screen has to draw a binding.
    internal const int MaxComboInputs = 4;

    /// <summary>
    ///     Adds an input to a binding, or removes it if it is already there. False when the binding
    ///     already holds <see cref="MaxComboInputs"/> and nothing changed.
    /// </summary>
    // Pressing a bound input again is how a single input is unbound; clearing a whole row is the
    // Journal action or Delete on the screen. A full binding refuses a new input rather than dropping
    // its oldest, as vanilla does: dropping one would silently turn the combo into a different one.
    internal static bool Toggle<T>(List<T> inputs, T input) {
        if (inputs.Remove(input)) return true;
        if (inputs.Count >= MaxComboInputs) return false;
        inputs.Add(input);
        return true;
    }

    /// <summary>
    ///     Strips Keys.None from every ButtonBinding property of a settings object. Call once, after
    ///     the settings load.
    /// </summary>
    /// <returns>How many entries were removed.</returns>
    // Reflected, not driven by the keybind table: a property the table forgot is exactly the one
    // that would keep Keys.None. The enumeration matches Everest's own OnInputInitialize —
    // GetProperties(), CanRead, IsAssignableFrom — so the two cannot disagree about which properties
    // are bindings.
    //
    // A null binding is left null. Everest creates it later, in OnInputInitialize, and only reads
    // [DefaultButtonBinding] when it is still null. (That attribute never seeds Keys.None itself:
    // Everest skips a default key of 0. A settings file can carry it anyway, from an older build or a
    // hand edit.)
    internal static int Sanitize(object settings) {
        if (settings is null) return 0;

        int removed = 0;
        foreach (PropertyInfo property in settings.GetType().GetProperties()) {
            if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
            if (!typeof(ButtonBinding).IsAssignableFrom(property.PropertyType)) continue;
            if (property.GetValue(settings) is not ButtonBinding binding) continue;

            binding.Keys ??= new List<Keys>();
            binding.Buttons ??= new List<Buttons>();
            removed += binding.Keys.RemoveAll(key => key == Keys.None);
        }
        return removed;
    }
}
