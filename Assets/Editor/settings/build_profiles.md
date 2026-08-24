# build_profiles.md

## overview

this file describes the first phase of the accessible Build Profiles window.

the purpose of this phase is to allow the user to configure a Windows standalone build, choose the scenes to include and build or run the game without using Unity's inaccessible Build Profiles window.

this phase uses Unity's platform profile.

custom Build Profile creation and profile-specific overrides are outside the current scope.

## unity version

target Unity 6.3 LTS.

verify Unity behaviour and APIs against the Unity 6.3 manual before implementing build functionality.

## required imports

use the current Unity 6.3 APIs where appropriate, including:

- UnityEditor
- UnityEditor.Build
- UnityEditor.Build.Reporting
- UnityEngine

## accessibility requirements

- use the accessible controls located in ./utils
- the entire window must be useable with NVDA and the keyboard
- every control must have an accessible name
- controls must expose their current value, selected state, checked state and enabled state
- use a predictable tab order
- use arrow keys for lists
- use enter to activate the selected item
- do not require the user to identify controls by colour, icons, position or tooltips
- announce important changes such as active platform changes and build results
- do not use drag and drop as the only way to manage scenes
- do not open Unity's standard Build Profiles window as the accessible interface

## main window

the window contains:

- target platform
- scene list
- Windows platform settings
- development build
- compression method
- Build
- Clean Build
- Build and Run

when the window opens, announce the current active build target.

## target platform

this phase supports Windows standalone.

show Windows as the available target.

identify whether Windows Build Support is installed.

if Windows Build Support is not installed:

- tell the user that Windows Build Support is missing
- do not attempt to perform a Windows build
- provide the supported route to install the module with Unity Hub where possible

### switch target

use the current supported EditorUserBuildSettings.SwitchActiveBuildTarget overload.

for 64-bit Windows standalone use the appropriate Standalone build target and StandaloneWindows64 target.

do not use the obsolete overload that accepts only BuildTarget.

changing build target can cause Unity to reimport assets and recompile scripts.

only announce the switch as successful if Unity reports success.

## platform profile

this phase builds with the active Windows platform profile.

a custom BuildProfile asset is not required for this phase.

if a custom BuildProfile is currently active, show its name and active state, but custom profile editing is outside this phase.

the user must be able to return to the platform profile where appropriate.

use BuildProfile.GetActiveBuildProfile to determine whether a custom profile is active.

use BuildProfile.SetActiveBuildProfile(null) to return to the current platform profile.

## scene list

the Scene List controls which scenes are included in the build and their build order.

for the platform profile use the global scene list.

use EditorBuildSettings.globalScenes for the global scene list.

scene entries use EditorBuildSettingsScene.

show each scene with:

- scene name
- scene path
- included or excluded state

### scene actions

allow the user to:

- Add Open Scenes
- add a scene
- remove a scene
- include a scene
- exclude a scene
- move a scene up
- move a scene down

use the object selector located in ./utils to select a SceneAsset when adding a scene.

do not allow the same scene to be added twice.

the order of included scenes is the build order.

do not require drag and drop for reordering.

## windows platform settings

provide the basic Windows build settings needed for normal local development.

### architecture

provide:

- Intel 64-bit
- Intel 32-bit
- ARM 64-bit

use Intel 64-bit as the normal Windows development target unless the project already has another valid setting.

do not silently change an existing architecture.

### build and run on

provide:

- Local Machine
- Remote Device when Unity makes it available

Local Machine is the normal option for this phase.

remote deployment configuration is outside the current scope.

## development build

provide an accessible Development Build checkbox.

when enabled Unity includes development/debug information in the build.

the following dependent debugging options are outside this phase:

- Autoconnect Profiler
- Deep Profiling
- Script Debugging
- Wait for Managed Debugger

use their Unity defaults.

## compression method

provide:

- Default
- LZ4
- LZ4HC

preserve the project's current value.

do not silently change compression when opening the window.

## build

Build creates the Windows player without automatically starting it.

before building:

- ensure Windows is the active build target
- ensure at least one enabled scene is available for the build
- ask the user for the build output location
- preserve Unity's normal validation behaviour

use Unity's supported BuildPipeline.BuildPlayer API.

when building the platform profile, use the current build target and scene list rather than inventing a custom BuildProfile.

use the returned BuildReport to determine the result.

## clean build

provide a Clean Build action.

use Unity's supported clean-build behaviour.

do not manually delete arbitrary folders from Library or Unity's build cache.

## build and run

Build and Run must:

- build the active Windows platform profile
- use the configured scene list
- use the selected Windows platform settings
- launch the resulting player on the selected Build and Run target

for this phase the normal Build and Run target is Local Machine.

use the appropriate current BuildOptions including AutoRunPlayer.

## build result

after a build finishes, announce:

- succeeded
- failed
- cancelled

when available also provide:

- output path
- build duration
- build size

build errors must remain available in the Unity Console.

the user must not be required to inspect a visual Unity notification to know whether the build succeeded.

## existing custom build profiles

if custom BuildProfile assets exist, they may be listed read-only so the user knows they exist.

use:

AssetDatabase.FindAssets("t:BuildProfile")

and:

AssetDatabase.LoadAssetAtPath<BuildProfile>

where needed.

editing, creating, copying, renaming and deleting custom profiles are outside this phase.

## outside current scope

do not implement these Build Profiles features in this phase:

- Add Build Profile
- custom profile creation
- copy profile
- rename profile
- delete profile
- Asset Import Overrides
- Diagnostics
- Scripting Defines
- profile-specific Scene List overrides
- Player Settings overrides
- Graphics Settings overrides
- Quality Settings overrides
- Adaptive Performance Settings
- package-provided profile settings
- Cloud Build
- Force Skip Data Build
- remote device configuration
- Android, iOS, macOS, Linux, Web or other target-specific build settings

these can be added after Unity Access can successfully create and run a Windows build.

## unity manual references

Create and manage build profiles:
https://docs.unity3d.com/6000.3/Documentation/Manual/create-build-profile.html

Build Profiles reference:
https://docs.unity3d.com/6000.3/Documentation/Manual/build-profiles-reference.html

Scene List:
https://docs.unity3d.com/6000.3/Documentation/Manual/build-profile-scene-list.html

Windows build settings:
https://docs.unity3d.com/6000.3/Documentation/Manual/WindowsStandaloneBinaries.html

BuildProfile API:
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.html

EditorUserBuildSettings.SwitchActiveBuildTarget:
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html

EditorBuildSettings:
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorBuildSettings.html
