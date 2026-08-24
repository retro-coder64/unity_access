# project_settings.md

## overview

this file describes the accessible project settings window.

the window allows the user to view and change supported Unity project settings without relying on the standard inaccessible Project Settings window.

this window only covers project settings. editor preferences, build profiles and other Unity Access tools are outside this file.

## accessibility requirements

- use the accessible controls located in ./utils
- the entire window must be useable with NVDA and the keyboard
- every control must have an accessible name and expose its current value or state
- focus changes and important state changes must be available to NVDA
- do not require the user to identify controls by colour, icons, position or tooltips
- use a predictable tab order
- do not use the standard Unity Project Settings window as the accessible interface

## window layout

the window has two main areas:

- settings categories
- settings for the selected category

when the window opens, focus the settings categories list.

use up and down arrow keys to move through categories.

press enter to open the selected category.

use tab and shift tab to move through the controls for that category.

## project settings backend

use supported public Unity Editor APIs to read and change project settings.

each supported settings category should have its own adapter that maps the Unity setting to the accessible controls.

do not:

- use Unity internal APIs
- use reflection to access internal project settings
- directly edit files in the ProjectSettings folder
- parse or modify Unity project settings YAML files

only show a settings category when Unity Access has a supported adapter for it.

do not attempt to automatically reproduce every settings provider in Unity.

## reading settings

when a category is opened:

- read the current values from Unity
- populate the accessible controls with those values
- preserve the Unity value type, valid range and available choices

refresh the values when the category is reopened.

do not replace a value while the user is currently editing that control.

## changing settings

when the user changes a setting:

- validate the value before applying it
- use the public Unity API responsible for that setting
- keep the previous value if Unity rejects the change
- report an error through the accessible UI if the value cannot be applied

for settings represented by Unity objects, use the object selector located in ./utils where appropriate.

do not silently change related settings unless Unity itself requires the change.

## controls

map Unity settings to the matching accessible control type.

examples:

- bool = checkbox
- enum = combo box
- string = text field
- int or float = numeric field
- object reference = object selector
- list = accessible list with appropriate add, remove and reorder controls

use the existing accessible controls in ./utils rather than creating duplicate control implementations.

## unsupported settings

if a project setting does not have a supported public API or a Unity Access adapter, do not expose an editable control for it.

do not fall back to undocumented Unity APIs simply to make the setting available.

support for additional project settings can be added later by adding another adapter without changing the main window.
