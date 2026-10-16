# Portable widgets

On the Widgets page, choose **Import widget…** to open a `.themedwidget` package. Imported
widgets are saved as independent, disabled copies. Edit the widget before enabling it, and
configure any weather, music or Web/JSON connections it needs.

In the widget editor, **+ Image** adds artwork. Choose a PNG, JPEG, GIF, BMP or ICO file;
the image keeps its proportions inside the meter. The desktop and editor share image loading
and fallback behavior. The file is read locally, without downloading remote images.

Use a widget's **More → Export widget…** action to save a package. Referenced images are
included automatically, so the recipient does not need your original files. Saving a package
never modifies the widget you are editing. Weather, VibeFinder, Web/JSON and plugin connection
targets are omitted. Review custom text, links and variables before sharing: those fields are
preserved and may contain information you entered.

## Package contract

A `.themedwidget` file is a ZIP with:

```
skin.json
assets/001.png
assets/002.jpg
```

`skin.json` is a serialized `SkinDefinition`. Image meters use `Kind: "Image"` and an
`ImagePath` such as `assets/001.png`. The same asset can be referenced by multiple meters.
Groups, meter identities within a widget, transforms and metadata survive a round trip.
Supported legacy schema versions are upgraded on import; future schemas are rejected.
Imported widgets receive new identities and a reachable initial position `(40, 40)`.

For this version, asset entries use flat ASCII filenames under `assets/`; arbitrary files,
subdirectories, scripts, SVGs and plugin binaries are not supported. Limits are 128 entries,
1 MiB of manifest JSON, 16 MiB per image, 64 MiB expanded total and 65 MiB compressed input.
The importer rejects duplicate names case-insensitively, symbolic links, missing image
references, absolute paths and traversal paths. Stream copying enforces size limits in addition
to checking ZIP metadata. Images still need to be decodable by Windows; a missing or corrupt
image shows an unavailable-image placeholder rather than preventing the editor from opening.

The exporter uses a sibling temporary file and replaces the destination only after completion.
Imports validate before writing, extract to a staging directory using generated filenames and
then rename into `%LOCALAPPDATA%\ThemedAI\WidgetAssets\<new-widget-id>`. Stored widget
image paths point to those installed files. Imports through the Widgets page report success
only after `skins.json` has been saved.

Asset files are retained when widgets are deleted because saved worlds can still refer to them.
Reference-aware garbage collection and world-level packaging are future work. Failed library
persistence after asset installation can also leave an unused asset directory; it is safe to
remove only after confirming no saved widget or world uses it.

## Verification

The core suite covers package round-trips, source-image deletion, re-export, independent repeated
imports, credential-target omission, legacy schemas, invalid JSON/collections, unknown meter
kinds, duplicate entries, symlinks, unsafe image references, entry/size limits and atomic export
failure/cancellation. Repository tests verify strict persistence failure reporting and reloading
image/group data.

Windows acceptance checks still required:

1. Build the solution with the pinned SDK, and run the complete test suite.
2. Create a widget, add an image, resize/group it, save it and compare preview with the desktop.
3. Export through **More**, delete or move the source image, and import the package twice.
4. Confirm both imports remain off, have independent images and survive an application restart.
5. Enable one; verify rendering at 100%, 150% and 200% DPI. Confirm missing/corrupt images show
   the placeholder and cancelling either file picker leaves the widget collection unchanged.
6. Attempt an invalid package and a storage failure; verify the error is visible without a crash.

Windows UI execution cannot be established by Linux core tests or XML parsing.
