#AGENTS.md
this is the accessible hierarchy view for the unity_access plugin 

##description 
the hierarchy view shows all objects in the seen 
it does not edit any objects
the user can use the up and down arrow keys to navigate through the list of objects 
the hierarchy will expose the name of the object that the user is on to the NVDA helper scripts. 
## adding objects 
the hierarchy will have a button labeled add object 
when the button is pressed the add object utility will be called 
the parent atribute will be nul 
##startup 
the hierarchy view will activate when the user presses h 
it will also start when the user starts the unity_access plugin. 
## selecting an item 
the user will select an item by pressing enter 
the script will then pass the name to the inspector script (reference "./inspector/")
## removing an item 
an object can be removed from the seen by navigating to the object and pressing backspace. 
## NVDA access 
the hierarchy view will pass the data that is to be spoken allowed to the script located @ "./NvdaApi.cs" 

## rules 
1. do not edit files outside the directory unless given permission to do so by the user 
2. no feature is complete until the user can use Nvda to navigate it and use it successfully. 

## passing data to the inspector 
the programme will opperate on an observer pattern 
the hierarchy will update a shared selection variable which the inspector will watch for 
the hierarchy does not call the inspector explicitly 


## object options menu 
build an menu for the options of each object using object_options_menu.md 
ensure that it meets the accessibility standards of the project 