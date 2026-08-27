---
title: Dialogs and sheets
description: Present alerts, action sheets, and draggable modal sheets.
ms.date: 2026-08-27
---

# Dialogs and sheets

<table>
  <tr>
    <td><img src="../../images/dialog.png" alt="Cupertino alert dialog" width="360" /></td>
    <td><img src="../../images/sheet.png" alt="Cupertino draggable sheet" width="360" /></td>
  </tr>
</table>

## Dialogs

`Dialog.ShowAsync` returns the selected `DialogAction`. Roles drive presentation and preferred focus.

```csharp
var result = await Dialog.ShowAsync(
    this,
    "Delete Photo?",
    "This photo will be deleted from all your devices.",
    new DialogAction("Cancel", DialogActionRole.Cancel),
    new DialogAction("Delete", DialogActionRole.Destructive));

if (result?.Role == DialogActionRole.Destructive)
    DeletePhoto();
```

With more than two actions, the dialog stacks actions automatically. Use `ShowSheetAsync` for an action-sheet presentation.

## Sheets

`CupertinoSheet` presents arbitrary Avalonia content. Medium-and-large sheets float at the medium detent and can expand; large-only sheets open near full height.

```csharp
await CupertinoSheet.ShowAsync(
    this,
    new DetailsView(),
    SheetDetents.MediumAndLarge);
```

Users can drag the grabber, flick down, or tap the scrim to dismiss. Keep primary content away from the grabber area, and ensure the sheet remains usable at every enabled detent.
