# Full Map Teleport

Full Map Teleport turns Terraria's full-screen map into a practical travel tool. Open the map with `M`, right-click a location to teleport there, and use pylon icons without the usual "stand near a pylon" or team Unity Potion requirements.

The mod also reveals the entire map at maximum brightness on demand and makes Magic Mirror, Ice Mirror, Cell Phone, and Shellphone teleports trigger immediately instead of waiting through their normal use delay.

The mod supports single-player, Host & Play, and dedicated multiplayer. For unrestricted pylon travel on a dedicated server, install the mod on both the server and the participating clients.

---

## Configuration

### Enabled

Enables or disables Full Map Teleport's map teleport, unrestricted pylon, and instant-return-item behavior.

### Reveal Full Map

An F6 Mod Menu action that reveals every map tile at maximum brightness. The action resets itself after use.

### Full Map Teleport Panel

Press `F7` to open the draggable utility panel. Select **Reveal Full Map (Max Brightness)** to reveal the world without opening F6.

---

## Installation

Install [TerrariaModder](https://www.nexusmods.com/terraria/mods/135) first. Full Map Teleport requires Terraria 1.4.5 on Windows and must be launched through TerrariaModder, not by starting `Terraria.exe` directly.

### With TerrariaModder Vault

When Full Map Teleport is available in TerrariaModder Vault, search for **Full Map Teleport** in the Browse Nexus section and install it normally. Launch the game with **Run Modded** after installation.

### Manually

1. Download the latest release from the [Files tab](https://www.nexusmods.com/terraria) or [GitHub](https://github.com/ABKQPO/FullMapTeleport).
2. Extract the downloaded archive.
3. Move the included `full-map-teleport` folder into `Terraria/TerrariaModder/mods/`.
4. Confirm that the folder contains `FullMapTeleport.dll` and `manifest.json`.
5. Start the game through `TerrariaInjector.exe` or TerrariaModder Vault.

For dedicated multiplayer, copy the same mod folder to the server's `Terraria/TerrariaModder/mods/` directory as well. This is required for the server to approve unrestricted pylon requests.

---

## Questions, Suggestions, Bug Reports and Contributing

For questions, suggestions, or bug reports, open an issue on [GitHub](https://github.com/ABKQPO/FullMapTeleport/issues). Please include your Terraria version, TerrariaModder version, whether the issue occurs in single-player, Host & Play, or dedicated multiplayer, and any relevant `terrariamodder.log` entries.

Contributions are welcome through GitHub issues and pull requests. Keep reports focused on one problem or feature request so they can be reproduced and reviewed clearly.

---

## Credits

Thanks to **Re-Logic** for Terraria and the full-screen map, pylon, and return-item systems that make this mod possible.

Thanks to **Inidar1** and the **TerrariaModder** project for the framework, configuration UI, widget library, and runtime patching support.

Thanks to **ConfuzzedCat** for TerrariaInjector, which allows TerrariaModder mods to load without modifying the Terraria game executable.

Thanks to everyone who tested map coordinates, pylon travel, world reveal, and return-item behavior and reported issues.
