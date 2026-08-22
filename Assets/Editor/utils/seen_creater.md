# seen_creater.md

## Overview

`seen_creater` provides an accessible UI for creating a new Unity scene from any available scene template, including custom templates.

## Required imports

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.SceneTemplate;
using UnityEngine;
```

Required APIs:

* `AssetDatabase.FindAssets` — find available `SceneTemplateAsset` assets.
* `SceneTemplateAsset` — represents a scene template and provides `templateName`.
* `SceneTemplateService.Instantiate` — create a scene from the selected template.
* `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo` — protect unsaved scene changes.
* `EditorSceneManager.NewScene` — fallback for creating a basic or empty scene.
* `EditorSceneManager.GetActiveScene` — access the current scene when needed.
* `EditorSceneManager.sceneOpened` — detect when a scene has been opened.

## UI

Use the accessible UI utilities.

Display all available scene templates, including custom templates, in a navigable list.

## Navigation

* Arrow keys move through the template list.
* Enter selects the current template and creates the scene.
* Escape closes the window.

## Logic

When a template is selected:

1. Check for unsaved changes.
2. If changes exist, use the Editor API to ask whether they should be saved.
3. If the operation continues, instantiate the selected template.
4. Open the resulting scene.

## Accessibility

Use the existing accessible control utilities for all UI elements and navigation.


## shortcut 
the user must press ctrl n 
if it already has a action attached to it replace that action with the seen creater 