using System.Linq;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.SpeedrunSheet;

// one frame of the input the hotkeys are allowed to look at. Passed in rather
// than read from MInput inside ComboHotkey: handling a hotkey writes settings
// and shows a popup, and every hotkey of a frame must still answer the same
// input state
internal readonly record struct InputSnapshot(KeyboardState Keyboard, GamePadState Pad) {
    // Input.Gamepad is the pad Celeste itself plays on, and the one
    // KeybindConfigUi records a binding from. Watching "the first connected
    // pad" instead binds on one controller and listens on another, with two
    // plugged in
    internal static InputSnapshot Current() => new(
        MInput.Keyboard.CurrentState,
        MInput.GamePads[Input.Gamepad].CurrentState
    );
}

// a ButtonBinding read as a combo: all of its keys held at once, rising edge
// only. Everest's own ButtonBinding.Pressed is an OR over the bound keys —
// the opposite reading — so nothing here goes through the VirtualButton
internal sealed class ComboHotkey(ButtonBinding binding) {
    private bool lastCheck;

    public bool Pressed { get; private set; }

    // All over an empty list is true, hence the Count checks. A state with
    // nothing held already answers false to every IsKeyDown, so there is no
    // need to test it against default on top of that
    private bool IsDown(in InputSnapshot input) {
        if (binding.Keys.Count > 0 && binding.Keys.All(input.Keyboard.IsKeyDown)) {
            return true;
        }

        return binding.Buttons.Count > 0 && binding.Buttons.All(input.Pad.IsButtonDown);
    }

    public void Update(in InputSnapshot input) {
        bool current = IsDown(input);
        Pressed = !lastCheck && current;
        lastCheck = current;
    }

    // swallows the edge of whatever is held right now, so nothing fires until
    // the combo has been released and pressed again
    public void Resync(in InputSnapshot input) {
        lastCheck = IsDown(input);
        Pressed = false;
    }
}
