# Humankind BepInEx Mods

A collection of quality-of-life and UI mods for HUMANKIND™, built using the BepInEx framework.

## Included Mods

### 1. [Wartime Popup Remover](https://www.nexusmods.com/humankind/mods/11)
**Source folder:** `WarTurnSilenceMod`  
Disables the forced automatic pop-ups for war notifications at the start of every turn. You can now start your turn in peace and get straight to playing without having to manually close useless windows.

### 2. [City Rename Mod](https://www.nexusmods.com/humankind/mods/10)
**Source folder:** `CityRenameMod`  
Allows you to dynamically update your city names to match your empire's cultural progression throughout the eras.

### 3. [Colored Emblematic Quarters](https://www.nexusmods.com/humankind/mods/9)
**Source folder:** `ColoredEmblematicQuaters`  
Visually highlights emblematic quarters on the map with colored overlays, making them easily distinguishable from standard districts.

## Requirements

To play or develop these mods, you need **[BepInEx (x64) 5.4.x](https://github.com/BepInEx/BepInEx/releases)** installed in your Humankind root folder.

## Building from Source

Game binaries and BepInEx libraries are not included in this repository to avoid copyright issues. To build these mods yourself:

1. Clone this repository.
2. Open the `.csproj` file of the mod you want to build in Visual Studio.
3. Fix the missing assembly references. You will need to point them to your local game installation, typically found in:
   * `[Humankind_Folder]\Humankind_Data\Managed\` (for Assembly-CSharp.dll, UnityEngine.dll, etc.)
   * `[Humankind_Folder]\BepInEx\core\` (for BepInEx.dll)
4. Build the project (Release mode is recommended).
5. Drop the compiled `.dll` into `[Humankind_Folder]\BepInEx\plugins\`.

## License

This project is licensed under the MIT License - see the LICENSE file for details.