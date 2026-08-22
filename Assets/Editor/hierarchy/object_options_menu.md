object_options_menu.md
This file describes the object options menu for the currently selected GameObject.
The menu is opened by pressing Shift+F10.
accessibility requirements
• 
Use the accessible controls provided in the utils folder.
• 
The menu must be fully usable with the NVDA screen reader.
• 
Ensure that keyboard shortcuts do not conflict with existing Unity Access shortcuts.
• 
Keyboard focus must move to the menu when it opens.
• 
The user must be able to navigate the options using the keyboard.
• 
Pressing Escape must close the menu and return focus to the previous view.
• 
After an action completes, close the menu unless the action requires the user to make another selection.
selected object
All actions operate on the GameObject that was selected when the menu was opened.
Do not perform an action if there is no valid selected GameObject.
options
The user will be given the following options:
• 
Delete
• 
Duplicate
• 
Create Prefab
• 
Set Parent
• 
Unparent
• 
Add Child
delete
Delete the selected GameObject from the current scene.
Use the Unity Editor API and Unity Undo system so the deletion can be undone.
confirmation
Before deleting the object, show an accessible confirmation dialog.
The confirmation must clearly identify the object that will be deleted.
If the user confirms:
• 
delete the object
• 
register the action with Unity Undo
• 
close the options menu
If the user cancels, return focus to the options menu.
duplicate
Create a copy of the selected GameObject in the current scene.
The duplicate should:
• 
contain the same components and values as the original
• 
use the same parent as the original
• 
be registered with Unity Undo
• 
appear in the hierarchy
Select the duplicated object after it has been created.
create prefab
Create a Prefab from the selected GameObject.
Use the appropriate Unity Editor Prefab API.
The original GameObject must remain in the scene.
set parent
Allow the user to make the selected GameObject a child of another GameObject.
Use the object_selector utility.
The selector must:
• 
only show GameObjects from the current scene
• 
not allow the selected object to be its own parent
• 
not allow parenting that would create a circular hierarchy
Use the Unity Undo system when changing the parent.
unparent
Remove the selected GameObject from its current parent.
The GameObject must become a root object in the current scene hierarchy.
Use the Unity Undo system when changing the parent.
If the object already has no parent, do not perform the action.
add child
Allow the user to create or add another object as a child of the selected GameObject.
Use the existing add_object utility.
Pass the selected GameObject as the parent when calling the utility.
The newly added object must appear beneath the selected GameObject in the hierarchy.
One change I particularly recommend
I changed:
set the parent atribute to the object reference  of the selected object