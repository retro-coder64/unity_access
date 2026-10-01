# AGENTS.md

this is the md file for the animation controller for unity_access  
this controls the creation of the animation controller.

## rules

use the accessible controls from ./utils.  
ensure that all parts of the animation system is screen reader compatable.  
this means: no unlabeled buttons, fields or text boxes.  
Use the APIs provided in this document.  
if you need another API ensure that it is compatable with unity 6.3 and the most uptodate version.

## APIs

use the following Unity APIs for the animation controller.

### namespaces

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
```

### animator controller

use:

```csharp
AnimatorController.CreateAnimatorControllerAtPath(path)
```

to create a new animator controller.

use:

```csharp
AssetDatabase.LoadAssetAtPath(path, typeof(AnimatorController))
```

to load an existing animator controller.

use:

```csharp
AnimatorController.AddMotion(animationClip, layerIndex)
```

to add an AnimationClip to the selected layer of the animator controller.

this creates an AnimatorState containing the selected animation.

use:

```csharp
controller.layers
```

to access the layers in the animator controller.

use:

```csharp
AnimatorControllerLayer[] layers = controller.layers;
```

to get the current layers.

when modifying properties of the layers array, assign the array back to:

```csharp
controller.layers = layers;
```

use:

```csharp
controller.layers[selectedLayerIndex].stateMachine
```

to access the state machine for the selected layer.

use:

```csharp
stateMachine.states
```

to get the states contained in the state machine.

use:

```csharp
AnimatorState.motion
```

to access the AnimationClip or other Motion assigned to a state.

use:

```csharp
AnimatorStateMachine.RemoveState(state)
```

to remove an animation state from the animator controller.

use:

```csharp
controller.AddLayer(layerName)
```

to add a new layer to the animator controller.

### animation clips

animation files are represented by:

```csharp
AnimationClip
```

when an animation is selected, ensure that the selected object is an `AnimationClip` before adding it to the animator controller.

### saving

use:

```csharp
EditorUtility.SaveFilePanelInProject()
```

when a save location for a new animator controller is required.

the extension must be:

```text
controller
```

use:

```csharp
EditorUtility.SetDirty(controller)
```

when changes have been made to an existing animator controller.

use:

```csharp
AssetDatabase.SaveAssets()
```

to write unsaved asset changes to disk.

### asset paths

Unity AssetDatabase paths must be project-relative paths beginning with `Assets/`.

use:

```csharp
AssetDatabase.GetAssetPath()
```

when the project-relative path for a Unity asset is required.

## unity_access menu control

in the unity_access menu there will be a button for animator  
this will open the animator window  
ensure that it is labeled and screen reader friendly.

# creating or opening an animator controller

in the main window there will be 2 buttons  
create animator controller  
open animator controller

if the user selects create, then the code will open a save as box and create a blank animator controller with no animations using:

```csharp
EditorUtility.SaveFilePanelInProject()
AnimatorController.CreateAnimatorControllerAtPath()
```

if the user presses open, then the code will open a file dialog and allow the user to select the controller.

load the selected controller using:

```csharp
AssetDatabase.LoadAssetAtPath(path, typeof(AnimatorController))
```

## animator controller window

regardless of what the user selected the animator controller window will open.  
it will first show a list of layers using:

```csharp
AnimatorControllerLayer[] layers = controller.layers;
```

when the user selects a layer it will then show the animations in the layer.

store the selected layer using its index in the layers array.

### showing animations

this will show all the animations for the user  
the list of animations should update in real time.

the animation list should be generated from the states in the animator controller's selected layer state machine.

use:

```csharp
controller.layers[selectedLayerIndex].stateMachine.states
```

and read each state's:

```csharp
AnimatorState.motion
```

only states containing an `AnimationClip` should be shown as animations in this list.

### buttons

there will be the following buttons:

- add animation
- add layer
- save changes

## add animation

the add animation window will open with a text box for the name of the animation and an option to select a clip.

the add animation will use the object searching functionality from .utils to search for AnimationClip assets.

this must support standalone .anim files and AnimationClip sub-assets contained inside imported models such as .fbx files.

for imported model files use:

```csharp
AssetDatabase.LoadAllAssetsAtPath(assetPath)
```

filter the returned assets for:

```csharp
AnimationClip
```

the selected object must be loaded or returned as an:

```csharp
AnimationClip
```

once the user selects the file it is added to the selected layer using:

```csharp
controller.AddMotion(animationClip, selectedLayerIndex)
```

and this updates the animations list.

## add layer

the add layer button will open a window with a text box for the layer name.

the layer name must not be empty.

add the new layer using:

```csharp
controller.AddLayer(layerName)
```

after adding the layer update the layers list.

## save changes

this will save the animation controller.

the save location for a new animation controller is selected when the controller is created.

if the animator controller has already been created or opened, save the changes using:

```csharp
EditorUtility.SetDirty(controller)
AssetDatabase.SaveAssets()
```

## keyboard commands

arrow keys to navigate the interface  
backspace to remove an animation from the list this will remove it from the selected layer using:

```csharp
AnimatorStateMachine.RemoveState(state)
```
in addition, when used on a layer it will remove the entire layer 
enter selects items.  
esc closes the window