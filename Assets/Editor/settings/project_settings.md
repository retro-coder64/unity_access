# project_settings.md

## overview

this file describes the first phase of the accessible Project Settings window.

this phase only contains the project settings needed for normal early game development.

do not add other Project Settings categories unless they are added to the scope later.

## unity version

target Unity 6.3 LTS.

verify Unity behaviour and APIs against the Unity 6.3 manual before implementing a setting.

## accessibility requirements

- use the accessible controls located in ./utils
- the entire window must be useable with NVDA and the keyboard
- every control must have an accessible name
- every control must expose its current value or state to NVDA
- use a predictable tab order
- use arrow keys for lists
- use enter to activate the selected item
- do not require the user to identify controls by colour, icons, position or tooltips
- when a control is disabled, NVDA must be able to identify that it is disabled
- do not open the standard Unity Project Settings window as a replacement for the accessible interface

## categories

this phase contains:

- Player
- Tags and Layers

when the window opens, show these categories in an accessible list.

press enter on a category to open its settings.

## player

Player Settings control how Unity builds and displays the final application.

this phase is targeted at Windows standalone development.

use the public PlayerSettings API wherever Unity provides the required setting.

### general settings

provide:

- Company Name
- Product Name
- Version
- Default Icon

use the object selector located in ./utils when selecting the Default Icon.

Default Cursor and Cursor Hotspot are outside the current scope.

### windows settings

provide an accessible Windows settings section.

### resolution and presentation

provide the settings needed to control the initial game window:

- Run In Background
- Fullscreen Mode
- Default Is Native Resolution
- Default Screen Width
- Default Screen Height
- Resizable Window

only show settings when Unity considers them valid for the selected Fullscreen Mode.

for example, Default Screen Width and Default Screen Height are relevant when the game is Windowed.

when changing Fullscreen Mode, refresh dependent controls without unexpectedly moving keyboard focus.

### other player settings

the remaining Windows Player Settings are outside this phase.

use Unity defaults for settings that are not exposed by this window.

do not implement Rendering, Vulkan, D3D12, Configuration, Shader, Optimization, Stack Trace or other advanced Player Settings until they are added to the scope.

## tags and layers

provide an accessible replacement for Project Settings > Tags and Layers.

this section contains:

- Tags
- Sorting Layers
- Layers

### tags

show all existing tags in an accessible list.

allow the user to:

- add a tag
- remove a custom tag

Unity does not allow an existing tag to be renamed.

do not provide a rename action for tags.

show a confirmation before removing a tag.

### sorting layers

show all Sorting Layers in an accessible list.

allow the user to:

- add a Sorting Layer
- rename a Sorting Layer where Unity permits it
- remove a Sorting Layer where Unity permits it
- move a Sorting Layer up
- move a Sorting Layer down

do not rely on drag and drop for reordering.

### layers

show all Unity layers.

Unity has 32 layer slots.

preserve Unity's built-in layers.

allow the user to name and edit the user layer slots that Unity permits.

do not allow protected built-in layer names to be modified.

the Layer property on an individual GameObject belongs in the Unity Access Inspector.

do not put layers into the hierarchy tree.

## changing settings

validate a value before applying it.

after a successful change:

- update the accessible control
- ensure Unity records the project setting change
- refresh any dependent controls

if Unity rejects a value:

- keep the previous valid value
- report the error through the accessible UI

do not directly edit ProjectSettings YAML files when Unity provides a supported editor API.

do not invent Unity API methods.

if a setting does not have a documented public editing API, keep any Unity-version-specific implementation isolated from the accessible UI and verify it specifically against Unity 6.3 before use.

## outside current scope

do not implement these Project Settings in this phase:

- Audio
- Editor
- Graphics
- Input Manager
- Package Manager
- Physics
- Physics 2D
- Preset Manager
- Quality
- Scene Template
- Script Execution Order
- Services
- Time
- UI Toolkit
- Version Control
- full Windows Player Settings
- non-Windows Player Settings

Physics layer collision rules will be added later when the accessible Physics settings are implemented.

## unity manual references

Player:
https://docs.unity3d.com/6000.3/Documentation/Manual/class-PlayerSettings.html

Windows Player settings:
https://docs.unity3d.com/6000.3/Documentation/Manual/playersettings-windows.html

Tags and Layers:
https://docs.unity3d.com/6000.3/Documentation/Manual/class-TagManager.html

PlayerSettings API:
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.html
