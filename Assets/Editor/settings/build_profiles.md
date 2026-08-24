# build_profiles.md

## overview

this file describes the accessible build profiles window.

the window allows the user to view and manage supported Unity Build Profiles without relying on the standard inaccessible Build Profiles window.

## required imports

use the current public Unity Editor APIs, including:

- UnityEditor
- UnityEditor.Build.Profile
- UnityEditor.Build.Reporting
- UnityEngine

## accessibility requirements

- use the accessible controls located in ./utils
- the entire window must be useable with NVDA and the keyboard
- every control must have an accessible name and expose its current value or state
- focus changes and important state changes must be available to NVDA
- do not require the user to identify controls by colour, icons, position or tooltips
- use a predictable tab order
- do not use the standard Unity Build Profiles window as the accessible interface

## window layout

the main window contains:

- build profile list
- actions for the selected profile

when the window opens, focus the build profile list.

use up and down arrow keys to move through profiles.

press enter to open the selected profile.

## finding build profiles

find BuildProfile assets with:

AssetDatabase.FindAssets("t:BuildProfile")

convert each returned GUID to a path with:

AssetDatabase.GUIDToAssetPath

load the profile with:

AssetDatabase.LoadAssetAtPath<BuildProfile>

store the profile name, asset path and BuildProfile reference.

sort the list by profile name.

use:

BuildProfile.GetActiveBuildProfile()

to identify the active custom build profile.

if it returns null, the current platform profile is active. use EditorUserBuildSettings.activeBuildTarget to identify the current build target.

## profile actions

a selected custom build profile can provide these actions:

- activate profile
- edit scenes
- edit scripting defines
- duplicate profile
- rename profile
- delete profile
- build
- build and run

only expose an action when it can be completed with a supported public Unity API.

## activate profile

activate a custom profile with:

BuildProfile.SetActiveBuildProfile(profile)

use:

BuildProfile.SetActiveBuildProfile(null)

to return to the current platform profile.

changing profile can cause Unity to reimport assets and recompile scripts.

after Unity finishes the change, refresh the profile list and active state.

## scenes

use BuildProfile.overrideGlobalScenes to determine whether the profile uses its own scene list.

if overrideGlobalScenes is true, edit BuildProfile.scenes.

if overrideGlobalScenes is false, use EditorBuildSettings.globalScenes.

scene entries use EditorBuildSettingsScene and must preserve:

- scene path
- enabled state

provide actions to:

- add scene
- remove scene
- enable or disable scene
- move scene up
- move scene down

use the object selector located in ./utils when selecting a SceneAsset.

when changing a custom profile asset, use Undo.RecordObject where appropriate, mark the profile dirty and save the asset.

## scripting defines

use BuildProfile.scriptingDefines for the selected custom profile.

present the defines through an accessible editable list.

allow the user to:

- add define
- remove define

do not add duplicate define values.

when the list changes, mark the profile dirty and save the asset.

## duplicate profile

duplicate an existing custom profile with AssetDatabase.CopyAsset.

create a unique destination path with AssetDatabase.GenerateUniqueAssetPath.

the duplicated profile must keep the settings and target platform of the source profile.

refresh the profile list after the asset is created.

## rename profile

rename the selected custom profile with AssetDatabase.RenameAsset.

validate that the new name is not empty.

report any AssetDatabase error through the accessible UI.

refresh the profile list after the rename succeeds.

## delete profile

show a confirmation before deleting a profile.

if the profile being deleted is active, call:

BuildProfile.SetActiveBuildProfile(null)

before deleting the asset.

delete the profile with AssetDatabase.DeleteAsset.

refresh the profile list after deletion.

## build

for a custom BuildProfile, build with BuildPipeline.BuildPlayer using BuildPlayerWithProfileOptions.

set:

- buildProfile to the selected BuildProfile
- locationPathName to the path selected by the user
- options to the required BuildOptions

for build and run, include BuildOptions.AutoRunPlayer.

use the returned BuildReport to report whether the build succeeded, failed or was cancelled.

build errors must remain available in the Unity Console and should also be summarised through the accessible UI.

## creating new profiles

the public BuildProfile API does not provide a general method for creating a new profile for an arbitrary target platform.

do not use internal Unity APIs, reflection or undocumented serialized fields to work around this.

within this window, creating another custom profile is supported by duplicating an existing BuildProfile.

adding a first custom profile for a platform that has no existing profile is outside the scope of this file until Unity provides a supported public API.
