# NWSMM - New World Standalone Mini Map
A project started by someone who's constantly getting lost in-game. Started it the same week the game was released, meanwhile a few changes were made for ease-of-use and so that it could be released publicly.

While there are a few mini-maps out there already, they either require extra software like Overwolf or use the newworld-map. This mini-map uses mapgenie instead: https://mapgenie.io/new-world/maps/aeternum

The positioning is not perfect, it uses [OCR](https://en.wikipedia.org/wiki/Optical_character_recognition) to read the location from your screen, this means that for it to work you need to enable the in-game setting "Show FPS" settings in the "Visuals" settings sub-menu.

There's a lot of room for improvement, that being said:
Feel free to improve on it and I will review the submitted changes as quick as possible.

[More information, download, demo and FAQ can be found here.](https://mywk.net/software/newworld-standalone-minimap)

[![NWSMM](https://mywk.net/images/content/newworldstandaloneminimap.png)](https://mywk.net/software/newworld-standalone-minimap)

#### Requirements
[.NET Core 3.1](https://dotnet.microsoft.com/en-us/download/dotnet/3.1/runtime)

[WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)

### Changelog

###### Version 0.6.6
- Slightly better image pre-processing for OCR

###### Version 0.6.4

- Read player position using OCR (several attempts are made using Tesseract OCR with trained data with and without filters)
- Simple positioning self-correction
- Automatic positioning on MapGenie using the mapManager
- Save window position and transparency automatically
- Auto-select everything except animals and quests markers
- Toggle resize mode (window position is not saved while toggled)
- Toggle interactability (map can't be clicked while in this mode)
- Crude attempt at implementing an arrow that points to the right place without reading the middle top position (which I may implement later)