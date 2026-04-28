# Custom Resolution Manager Frontend Architecture

## Folder Structure

```text
CustomResolutionManager/
├─ Core/
│  ├─ Interfaces/
│  ├─ Messaging/
│  └─ Services/
├─ Models/
├─ ViewModels/
│  ├─ Base/
│  ├─ AddEditGameDialogViewModel.cs
│  ├─ FailSafeDialogViewModel.cs
│  └─ MainDashboardViewModel.cs
├─ Views/
│  ├─ Dialogs/
│  │  ├─ AddEditGameDialogView.xaml
│  │  └─ FailSafeDialogView.xaml
│  └─ MainDashboardView.xaml
└─ Themes/
   └─ Styles/
```

## Interface Contract

### IGameProfileService
Owns CRUD operations for game profiles and the enabled/disabled state. The frontend uses this to load, add, edit, remove, and toggle profiles.

### IResolutionService
Owns resolution discovery and change/revert operations. The frontend uses this for the resolution dropdown and the fail-safe revert flow.

### IProcessMonitorService
Owns process tracking for configured games. The frontend uses this to start and stop monitoring and to react to game launch/exit events.

### ISystemTrayService
Owns tray icon visibility and minimize/restore behavior. The frontend uses this from the shell window or application controller.

## View Responsibilities

### MainDashboardView
Shows the configured games list or grid with icon, name, executable path, target resolution, edit/delete actions, and enable/disable tracking toggle.

### AddEditGameDialogView
Hosts the game profile editor, including executable browsing and resolution selection.

### FailSafeDialogView
Shows the confirmation dialog after a resolution change, including the 15-second countdown, progress bar, and keep/revert actions.

## ViewModel Responsibilities

### MainDashboardViewModel
Loads and presents the list of game profiles, handles commands for add/edit/delete/toggle, and coordinates monitoring/tray state.

### AddEditGameDialogViewModel
Manages the editable game profile state, validates input, loads compatible resolutions, and submits the final profile data.

### FailSafeDialogViewModel
Runs the countdown state, exposes progress for the bar, and exposes commands for keep/revert resolution decisions.
``` 
