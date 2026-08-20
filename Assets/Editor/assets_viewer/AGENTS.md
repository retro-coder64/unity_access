# AGENTS.md 
this is the file describing the accessible asset viewer 
## features 
in the assets view the user will be able to do the following: 
- view the folders in the folders where the assets are located 
- rename the folders 
- delete the folders 
- move the folders 
-  view the assets inside the folders 
- rename the assets 
- delete the assets 
-move the assets 
- know the type of the assets 


## UI 
the assets viewer window will be a larger window that pops up over the seen. 
the user will be able to navigate it with the arrow keys. 
### layout 
#### folders
the folders will be in a collom down the left hand side 
the window will scrol to allow for all the folders to be shown when the user navigates to them 
if the folder has an icon it should be shown as well but more focus should be given to the names of the folder 
#### files 
the files will have the same layout as the folders. 
the name of the folder will be at the top of the window 
the window will show the file name and the type of asset (this will also be read out by the screen reader)
if the file has an icon it should be shown but more focus should be given to the text. 

## controls 
the user will be able to search for a specific asset by entering it's name 
the user can also navigate the folders and the assets inside them with the arrow keys. 
the user should be able to press f2 to open a text box to rename the folder or file. 
backspace should delete the selected item 
escape should exit the selected folder or if none is selected return the user to the main window. 
enter should allow the user to open the folder or to open the options menu for an asset or a file. 
shift + f10 should open the options menu for a folder. 

## options menu 
### assets/files 
the user will have the following options 
- rename 
- delete 
- move 
### folder 
the user will have the following options 
- rename 
- delete 
- move 
## moving files 
when the user uses the move option. 
Use the editor api to open a folder selection dialog box for the user to navigate. 

## renaming files 
when the user uses the rename option open a accessible text box for the user to edit the name of the selected item 

## deleting items 
when the user deletes an item open a confirmation dialog and ask the user "are you sure you want to delete this item
## editing properties of folders and files 
use the editor api to edit the properties.
avoid using system to perform these actions so as to avoid potential reference errors. 
ensure when moving or deleting the folders or files the .meta file is included in the opperation. 

## searching 
the search box is an editable field at the top of the menu 
it is the first thing that the screen reader reads when entering the assets window 
the search box will be labeled search 
use the accessible text box in the utils folder 
when the user has entered their search the search results will be shown below the search box 


## opening the assets viewer 
add a button into the unity access menu for the user to open the asset viewer. 