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

## Right-click menu

Right-click a file or folder (or press the Menu key) for: **Open**, **Make ERAF**, **Rename** (F2; only the name before the extension is edited), **Delete** (Del; goes to the Recycle Bin, and the question says clearly when Windows cannot use it, for example on a USB stick, a network drive or the Temp folder), **Show in File Explorer**, **Copy path**. Right-click the empty part of the list for **New folder** (Ctrl+Shift+N), **Refresh**, **Select all**, and the **Layout** and **Sort by** lists.

## Screenshots as photos

You can start an ERAF from a screenshot, then add a voice note as usual.
- **Screenshot** on the Home tab: **Region** (the Windows snipping tool opens; drag over what you want), **Whole screen**, or **A window...** (pick from the list). ERAF steps out of the way, takes the picture and comes back with it.
- **Paste** (or Ctrl+V): uses the picture on the clipboard, for example after Print Screen or Win+Shift+S. If you copied photo files in Explorer, those are used instead.
- **Offer:** when you copy a picture while ERAF is showing your files, a **Make ERAF** button appears at the bottom of the window. Turn this off in Settings if you do not want ERAF to notice copied pictures.
- **Shortcut from anywhere:** while ERAF is running, **Ctrl+Shift+F9** takes a region screenshot even if another program is in front, and brings ERAF up with the picture. (If you are already working on a photo, close it first with Back to files.) You can turn the shortcut off in Settings.

## Messages and the status bar

The strip along the bottom of the window is the status bar. Messages (saved, errors, how many photos were found, and so on) appear on its left with a symbol that shows what kind they are; press the small X to clear one. On the right it counts the items in the folder and how many are ticked.

## Choosing how files are shown

On the **View** tab, **Layout** lets you show the list as **Details** (columns you can click to sort), **Tiles**, **Small icons**, **Medium icons**, **Large icons** or a plain **List**. Your choice is remembered. Shortcuts (click in the list first): Ctrl+Shift+2 Large icons, 3 Medium icons, 4 Small icons, 5 List, 6 Details, 7 Tiles. Sorting uses the column headers in Details and stays the same in the other layouts.

## Checkboxes and selecting

Every file and folder has a checkbox. Tick the ones you want, or tick the box at the top of the list to select everything (**Select all** and **Select none** are on the Home tab). Double-click, or press Enter, to open the item you are on. You can turn the checkboxes off in the View tab or in Settings; the list then works like Explorer, where Ctrl and Shift select several.

## Big folders

When a folder takes a moment to read, a thin moving bar appears above the list. When photos are being searched for in a large folder, a box shows how many folders have been looked at and how many photos found so far, with a **Stop** button.

## Opening ERAF files from Explorer

Double-click any `.eraf` file in File Explorer and ERAF starts and opens it, showing the folder it is in. (If Windows asks which app to use the first time, choose ERAF and tick "Always".) Right-click > Open with > ERAF also works.

## Settings, ribbon and themes

Use the ribbon at the top. **Settings** on the Home tab opens the Settings page: theme, whether folders are searched with their subfolders, what to do when a file already exists, and where batch files are saved. Changes take effect immediately. The **Theme** list offers: System (follow Windows), Teal console, Office blue, Slate, Forest green, Violet, Amber and Midnight. It, the checkboxes and subfolder search can also be changed on the **View** tab. Keyboard shortcuts: Ctrl+O open, Ctrl+I import, Ctrl+S save, Ctrl+M Make ERAF, Ctrl+R record, Ctrl+T trim, F5 refresh, Alt+Left/Right/Up for Back, Forward and Up.

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
