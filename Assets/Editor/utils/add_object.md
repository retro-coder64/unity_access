## add_objects.md

this file describes the add objects window and the way the add objects function will work.

## add object backend

### atribute

takes an atribute of parent

if parent is nul then the object is added to the main seen tree

if parent != nul then add the object as a child of the object that is referenced  in the parent atribute.

### selecting objects

use the unity editor api to choose which assets can be added to the scene as a game object.

use:

AssetDatabase.FindAssets("t:Model")

and:

AssetDatabase.FindAssets("t:Prefab")

FindAssets returns GUIDs. Ensure that each GUID is converted to a file path using AssetDatabase.GUIDToAssetPath.

load the asset from the path and confirm that it can be loaded as a GameObject before adding it to the list.

do not add duplicate paths to the list.

add each valid result to an enum with a name, type and path.

the path and type are backend information and are not shown on the ui.

### adding the model

use the enum catagory of type to decide the correct way to instantiate the asset

use the prefab utility and the ObjectFactory utility

also use the undo utility that is part of the unity editor api

use this choice

if the item selected is a game object use the ObjectFactory

if parent is not nul parent the object to the object that is

if the object is an asset

use the prefab utility

if parent is not nul parent the new object to the object referenced by the parent atribute

## ui

the ui will be the same as the object selector ui

the user will be presented with a list of the potential game objects

pressing enter will instantiate these objects and handle parenting automatically

for now there will be no search box for the user to use.

pressing escape will return them to the previously opened window

## referencing
the add object takes a atribute of parent which is an object reference 
