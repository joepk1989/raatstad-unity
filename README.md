# raatstad-unity
Modulair bouwen in een raster van zeshoekjes met verschillende lagen

Unity-versie van Raatstad. De browserversie staat in `joepk1989/hexstad` en is de referentie voor hoe dingen moeten werken. Afspraken en ontwerpregels staan in `CLAUDE.md`.

## Wat staat erin
- `CLAUDE.md`: vaste afspraken voor Claude Code (techniek, ontwerpregels, werkwijze)
- `.gitignore`: Unity-template (sluit `Library/`, `Temp/`, `Logs/` en `UserSettings/` uit)
- `.gitattributes`: Git LFS voor grote bestanden (textures, modellen, audio, video)
- `Assets/Scripts/UI/HexLoadingScreen.cs`: laadscherm met animerende gele zeshoeken

## Project openen
1. Clone deze repo.
2. Voer eenmalig `git lfs install` uit.
3. Voeg de map in Unity Hub toe via Add > Add project from disk. Editorversie: 6000.6.7f1.
4. De mappen `Library/`, `Logs/` en `UserSettings/` maakt Unity zelf aan; ze staan niet in de repo.

## Werken met Claude Code
1. Installeer Git for Windows, dan Claude Code (PowerShell): `irm https://claude.ai/install.ps1 | iex`
2. Open een terminal in de repo-map en start `claude`. De `CLAUDE.md` wordt automatisch gelezen.
3. Open het project in Unity voordat je Claude Code de editor laat gebruiken.

## Unity-editor koppelen (optioneel, voor Unity 6.0 of hoger)
De Unity CLI is gratis en heeft geen Unity AI-abonnement nodig:

```powershell
winget install Unity.CLI
unity pipeline install
unity mcp configure claude
unity status
```

`unity mcp configure claude` maakt zelf de MCP-configuratie aan. Keur de verbinding daarna goed in Unity onder Edit > Project Settings. Controleer in Claude Code met `/mcp`.

## Vaste werkwijze
- Commit voor elke sessie en werk liefst op een aparte branch.
- Na een scriptwijziging hercompileert Unity. De koppeling valt dan even weg, probeer het daarna opnieuw.
