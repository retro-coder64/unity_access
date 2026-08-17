# object_selector.md

## Overview
object_selector.cs provides an accessible selector for Unity object references.

It must be generic and contain no component-specific logic.

## Requirements
The selector must receive:
- the required object Type
- the object that owns the reference field
- the current reference
- a way to return the selected reference

Use EditorUtility.IsPersistent(owner) to determine where references may come from.

If the owner is a scene object:
- search compatible objects in the open scene
- search compatible project assets

If the owner is a persistent asset:
- search compatible project assets only
- do not allow scene references

Do not use the current reference to decide the search scope because it may be null.

Only show objects compatible with the required Type.

Use AssetDatabase to search project assets.

Scene results must exclude persistent assets.

The list must:
- include None
- be sorted
- identify whether each result is a scene object or asset
- allow navigation with the Up and Down arrow keys
- announce the focused item through the NVDA API

## Selection
When an item is selected:
- return its UnityEngine.Object reference
- close the selector
- return focus to the previous window

Selecting None returns null.

Pressing Escape cancels the selector without changing the existing value and returns focus to the previous window.

## searching 
at the top of the selection screen there should be a search box to search for what the user is looking for 
the user should be able to edit in the box and the search term should be found using the current logic or as small a change as possible 
if no items are found show no results. 
make it an editable text box with a label that NVDA can read place it above the none option. 
## Important APIs
EditorUtility.IsPersistent(owner)
- true = owner is an asset, so scene references are not allowed
- false = owner is a scene object, so scene references are allowed

AssetDatabase.FindAssets
- use to search compatible project assets

## opening the selector
object_selector.cs must have only one public Open method.

The public Open method must require:
- required type
- owner
- current value
- callback

Do not provide a public overload which allows the owner to be omitted.

Internal helper methods may accept the calculated reference scope but must be private.