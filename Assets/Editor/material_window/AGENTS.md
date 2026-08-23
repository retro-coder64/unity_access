# AGENTS.md

## overview

this file describes the material window and the accessibility controls.

## accessibility rules

- use the accessible controls defined in ./utils.

- the user must be able to navigate the entire window using the keyboard and NVDA.

- use the object selector utility in ./utils when selecting Unity object references.

- do not use standard Unity controls where an accessible control already exists in ./utils.

## top level menu

the top level menu will have the following options:

- create material

- edit material

## create material

this option will first open a save dialog using EditorUtility.SaveFilePanelInProject.

use mat as the file extension.

the selected location must be inside the Assets folder.

if the user cancels the save dialog, do not create the material.

after the location is selected, open the material editor in create mode.

the user must select a valid Shader before the Material is created.

create the Material using the selected Shader and save it using AssetDatabase.CreateAsset.

## edit material

edit material will use the object selector utility set to Material.

the object selector must return a Material object.

open the material editor with the selected Material.

## material editor

the material editor is the interface the user will use to edit materials.

at the top of the editor the user will be able to edit the name of the material.

when renaming an existing material use AssetDatabase.RenameAsset so the Unity asset is renamed correctly.

the user will then be able to edit the shader.

use the object selector set to Shader.

the selector must return a valid UnityEngine.Shader object.

after the shader changes, refresh the property fields using the properties exposed by the new shader.

## definition of properties

do not use serialised properties to discover the shader properties.

use the public Unity Shader API:

- Shader.GetPropertyCount

- Shader.GetPropertyName

- Shader.GetPropertyDescription

- Shader.GetPropertyType

Shader.GetPropertyType returns UnityEngine.Rendering.ShaderPropertyType.

do not use ShaderUtil.ShaderPropertyType.

create the appropriate accessible control from ./utils for each supported shader property.

## colour properties

when the shader property type is Color, split the colour into:

- red

- green

- blue

each field must accept values from 0 to 255.

convert these values to the Unity colour range before applying them to the Material.

preserve the existing alpha value when editing RGB.

## texture properties

when the shader property type is Texture, use the object selector to select a compatible Texture asset.

user-imported images such as PNG files that Unity has imported as Texture2D assets must be available in the selector.

use Unity asset references and do not scan the file system directly for image files.

allow None where the shader property permits no texture.

## saving changes

apply changes to the Material using the public Material API for the property type.

save asset changes through the Unity AssetDatabase.

## exiting the view

if the user presses esc, close the window.


### other property types

when the shader property type is Float or Range, use the accessible numeric control from ./utils.

when the shader property type is Int, use the accessible integer control from ./utils.

when the shader property type is Vector, use separate accessible numeric fields for each vector value.

for Range properties respect the minimum and maximum values defined by the shader.