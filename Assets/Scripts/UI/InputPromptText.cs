using TMPro;
using UnityEngine.InputSystem;

/// <summary>Formats a bound input as a prompt-pack icon or the input system's button label.</summary>
public static class InputPromptText
{
    public static string Control(TMP_SpriteAsset atlas, InputHandler.PromptAction action)
    {
        if (!atlas) atlas = PromptAppearance.Icons;
        var input = InputHandler.Instance;
        if (!input) return "[Unbound]";

        var display = input.GetPromptBindingDisplay(action, out var layout, out var controlPath);
        var icon = FindBindingIcon(atlas, layout, controlPath, display);
        if (icon != null) return Icon(atlas, icon);

        return string.IsNullOrEmpty(display) ? "[Unbound]" : $"<noparse>[{display}]</noparse>";
    }

    public static string Icon(TMP_SpriteAsset atlas, string name)
    {
        if (!atlas) atlas = PromptAppearance.Icons;
        return atlas && atlas.GetSpriteIndexFromName(name) >= 0
            ? $"<sprite name=\"{name}\" color=#FFFFFFFF tint=0>"
            : $"[{name.Replace("keyboard_", "").Replace("mouse_", "mouse ")}]";
    }

    public static string MenuControls(TMP_SpriteAsset atlas) =>
        "Move  " + Icon(atlas, "keyboard_w") + Icon(atlas, "keyboard_a") +
        Icon(atlas, "keyboard_s") + Icon(atlas, "keyboard_d") + " / " +
        Icon(atlas, "gamepad_leftstick") + "\n\nPause  " + Icon(atlas, "keyboard_escape");

    private static string FindBindingIcon(TMP_SpriteAsset atlas, string layout, string controlPath, string display)
    {
        if (!atlas || string.IsNullOrEmpty(layout) || string.IsNullOrEmpty(controlPath)) return null;

        string name;
        if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "Keyboard"))
        {
            var key = display.Length == 1 && char.IsLetterOrDigit(display[0])
                ? display.ToLowerInvariant()
                : controlPath.ToLowerInvariant();
            key = key switch
            {
                "uparrow" => "arrow_up",
                "downarrow" => "arrow_down",
                "leftarrow" => "arrow_left",
                "rightarrow" => "arrow_right",
                "leftbracket" => "bracket_open",
                "rightbracket" => "bracket_close",
                "backquote" => "tilde",
                "quote" => "apostrophe",
                "slash" => "slash_forward",
                "backslash" => "slash_back",
                "leftctrl" or "rightctrl" => "ctrl",
                "leftshift" or "rightshift" => "shift",
                "leftalt" or "rightalt" => "alt",
                "leftmeta" or "rightmeta" => "win",
                "pageup" => "page_up",
                "pagedown" => "page_down",
                "scrolllock" => "scroll_lock",
                "numpadenter" => "numpad_enter",
                "numpadplus" => "numpad_plus",
                _ => key
            };
            name = "keyboard_" + key;
        }
        else if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse"))
        {
            name = controlPath switch
            {
                "leftButton" => "mouse_left",
                "rightButton" => "mouse_right",
                "middleButton" => "mouse_scroll",
                "forwardButton" => "mouse_side_forward",
                "backButton" => "mouse_side_back",
                _ => null
            };
        }
        else if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "Gamepad"))
        {
            name = controlPath switch
            {
                "buttonSouth" => "gamepad_south",
                "buttonEast" => "gamepad_east",
                "buttonWest" => "gamepad_west",
                "buttonNorth" => "gamepad_north",
                "leftStickPress" => "gamepad_leftstick",
                "rightStickPress" => "gamepad_rightstick",
                "dpad/up" => "gamepad_dpad_up",
                "dpad/down" => "gamepad_dpad_down",
                "dpad/left" => "gamepad_dpad_left",
                "dpad/right" => "gamepad_dpad_right",
                _ => null
            };
        }
        else
        {
            return null;
        }

        return name != null && atlas.GetSpriteIndexFromName(name) >= 0 ? name : null;
    }

}
