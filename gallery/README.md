# AfterThemed community gallery

The app's **Gallery** reads `gallery/index.json` from the `main` branch of this repository.

Each entry in `themes` is an `.afterthemed` document (`format`, `version`, `name`, `author`, the seven
`roles` as `#RRGGBB`, and optional `mapping`). Entries that are incomplete or invalid are skipped by the app.
Text replacements in gallery entries are ignored, and thumbnails are not needed here.

To submit a theme, use **Gallery → Submit current theme** in AfterThemed. It opens a GitHub issue with the theme
attached. Maintainers add accepted themes to `index.json`.
