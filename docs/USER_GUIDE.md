# ERAF User Guide

An **ERAF file** (`.eraf`) keeps one photo and one spoken voice note together in a single file, so the picture and what you said about it travel as one thing. This app (Windows) makes, opens and exports them.

## Make an ERAF file

1. Start the app. It opens maximized as a file manager. At the top is the **ribbon** with three tabs: **Home** (camera, import, open, **Make ERAF**, select all or none, settings, about), **Voice note** (record, stop, play, attach an audio file, trim, discard, choose the microphone) and **View** (checkboxes, subfolder search, theme). The Save, Export and **Back to files** buttons stay visible on every tab. Under the ribbon are Back, Forward, Up, Refresh, the folder path and a name filter. The left blade lists your quick-access folders and drives, and the right side lists the folders, photos and ERAF files in the current folder. Tick a photo's checkbox (or double-click it) and press **Make ERAF**. You can also use the camera button, or the import button to choose a photo from anywhere.
2. If you used the camera, press **Take photo** (or **Cancel camera**, or the Esc key, to go back).
3. Open the **Voice note** tab (it opens by itself once a photo is shown). If you have more than one microphone, pick the one to use from the microphone list (it starts on "Default microphone" and remembers your choice). Then press **Record** and speak. Press **Stop** when you are done. A note can be up to 10 minutes.
Instead of recording, you can press **Attach audio** and choose a saved sound file (M4A, MP3, WAV, WMA, AAC or FLAC, up to 10 minutes). The app converts it to the right format if needed; an M4A that is already AAC is used as it is. If the file cannot be used, you get a plain message and any voice note you already had stays.
4. Press **Play voice note** to listen. **Record again** or **Attach audio** replaces it; **Trim** lets you keep only part of it (drag the Start and End sliders, press **Play selection** to listen, then **Trim**); **Discard voice note** removes it.
5. Press **Save ERAF file**, choose where to save it and a name, and confirm. The line under the photo changes from "Not saved yet" to "Saved: name".

You can only save when the photo and a voice note are both there.

## Start menu

The app appears in the Start menu as **ERAF**. To keep it handy, right-click it and choose **Pin to Start** or **Pin to taskbar**.

## One voice note for many photos

Select several photos (Ctrl or Shift while clicking, or Ctrl+A), or select one or more folders, then press **Make ERAF** (or Ctrl+M). The app finds the photos, shows the first one, and lets you record or attach **one** voice note (and trim it). Then press **Make N files**: it asks what to do if an ERAF file with the same name already exists (skip, replace, or keep both with a number), shows progress with a Stop button, and finishes with a summary. Each file is saved next to its photo with the photo's name, or in one folder you choose in Settings. By default only photos directly inside a selected folder are used; turn on **Search subfolders too** in Settings to include the folders below it.

## Drag and drop

Drag photos, folders or an ERAF file from File Explorer onto the app window while you are looking at the file list. One photo starts a new ERAF, several photos or any folder start the "one voice note for many photos" flow, and an ERAF file opens. Other files in the drop are left out, and the app tells you how many.

## Checkboxes and selecting

Every file and folder has a checkbox. Tick the ones you want, or tick the box at the top of the list to select everything (**Select all** and **Select none** are on the Home tab). Double-click, or press Enter, to open the item you are on. You can turn the checkboxes off in the View tab or in Settings; the list then works like Explorer, where Ctrl and Shift select several.

## Big folders

When a folder takes a moment to read, a thin moving bar appears above the list. When photos are being searched for in a large folder, a box shows how many folders have been looked at and how many photos found so far, with a **Stop** button.

## Settings, ribbon and themes

Use the ribbon at the top. **Settings** on the Home tab opens the Settings page: theme (Windows setting, Light or Dark), whether folders are searched with their subfolders, what to do when a file already exists, and where batch files are saved. Changes take effect immediately. The theme, checkboxes and subfolder search can also be switched on the **View** tab. Keyboard shortcuts: Ctrl+O open, Ctrl+I import, Ctrl+S save, Ctrl+M Make ERAF, Ctrl+R record, Ctrl+T trim, F5 refresh, Alt+Left/Right/Up for Back, Forward and Up.

## Open an ERAF file

Double-click an ERAF file in the list (or select it and press Enter), or use the folder icon in the top bar to choose one from anywhere. The photo is shown and you can play its voice note. If the file is damaged, incomplete, or not an ERAF file, the app says so in plain words and nothing is changed.

## Export the photo or the voice note

With a photo and voice note on screen, **Export photo** saves the photo alone (a `.jpg`), and **Export voice note** saves the voice note alone (an `.m4a`). They are exact copies of what is inside the file.

## Back to files and unsaved work

**Back to files** closes the photo and returns to the file list. If you have not saved, the app asks first: **Keep working** (the default) or **Discard**. Closing the window asks the same.

## Camera and microphone permissions

If the app says the camera or microphone is turned off, open Windows **Settings > Privacy & security > Camera** (or **Microphone**) and allow access for the app, then try again. Without the camera you can still import a photo.

## If saving fails

Your photo and voice note stay on the screen, and any file you were replacing is left exactly as it was. Choose another folder or name and save again.

## Keyboard

Tab and Shift+Tab move between buttons, Enter or Space presses one, Esc cancels the camera or a question box. After each step the keyboard focus moves to the next likely button.

## Good to know

- The photo's own details (for example, where it was taken, if the camera recorded that) are kept exactly as in the original image file.
- A checksum inside the file detects damage; it does not prove who made the file.
- The app does not use the internet.
