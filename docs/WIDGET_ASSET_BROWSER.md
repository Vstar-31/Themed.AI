# Image asset browser

Open **Image assets…** in the widget editor toolbar, or **Browse image assets…** in an image meter's
inspector. Search by filename, select a thumbnail, and choose **Use image** for the image meter selected
when the browser opened. If no image meter was selected, **Add image meter** creates one. Applying the
image or creating the meter is one undo step; Save applies the draft to the running widget.

## Import and reuse

**Import image…** copies a selected file into the managed image library. The inspector's **Import
replacement…** performs the same import and applies it directly to the selected image meter.
Original source files are never moved or modified. Removing the source from Downloads will not break
the imported copy. Reimporting identical content with the same format reuses its stored copy, while
different contents with the same filename are kept separately. JPEG extensions are canonicalized.

The managed directory is `ThemedAI/WidgetAssets/Library` under `AppDataPaths.LocalRoot` (normally local
application data; isolated tests may override it). Hash subfolders retain readable filenames. Imported
package images under `WidgetAssets` also appear. Current draft references are included even when they
point outside the library or are missing. The asset browser never deletes files or changes other widgets.

Library imports remain reusable if the editor draft is discarded. Undo reverses the meter assignment,
not the import. There is no asset deletion/garbage collection in this slice because other widgets,
packages, and undo history may still reference files. Existing package export includes these images.

## Missing and invalid images

Missing/unavailable references appear first with explanatory text. They cannot be applied. Import a
replacement or select another image to repair the current draft. Refresh rescans the library; thumbnail
decode failures display a replacement prompt. Applying a selection rechecks the file and Windows decoder
so a file disappearing while the dialog is open does not silently assign a broken reference.

Supported formats: PNG, JPEG, GIF, BMP, ICO. Files must be nonempty and at most 16 MiB. The core import
service checks the format signature and bounds streamed copying; it does not claim to fully decode images.
The Windows picker/apply path also checks decoder metadata, limiting images to 32 megapixels and 16,384
pixels per side. Preview decoding failures remain possible for damaged image payloads and are shown.

Thumbnails use bounded decode dimensions and virtualized rows. Inventory lists at most 500 images,
prioritizes current draft references, scans at most 5,000 files and four directory levels, and skips
reparse points/inaccessible entries. A limited-inventory message explains truncation; search filters the
loaded inventory. Import is still available for files outside it.

## Verification

The Windows build passes. The full suite passes 660 tests, including 15 asset-library regressions.
A separate harness of the actual editor view-model source passes 37 cases, including five image-assignment
and undo cases, with dispatcher and persistence doubles. GUI interaction is still unverified because
Computer Use could not initialize in the preceding testing turn. Test results do not verify actual file
picker focus, dialog sizing, thumbnails, WinUI bindings, or rendering.
