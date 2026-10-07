using System.Windows.Forms;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;

namespace Doom;

public class DoomSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);

    [Menu("Toggle window", "Shows or hides the Doom window. Hiding it pauses the game.")]
    public HotkeyNodeV2 ToggleWindow { get; set; } = new HotkeyNodeV2(Keys.F8);
}
