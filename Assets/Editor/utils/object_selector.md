# object_selector.md
## overview 
the object_selector will provide an accessible selection interface for the user to select the object based on the type of reference. 
it should not contain any component specific logic for an animator or any other component. 
the file name is object_selector.cs
## requirements
the selector should accept the type of object 
find only the objects that match that type 
search in seen and accross assets where unity permits it to do so 
display the list as a sorted list that the user can navigate through and select from
it should also give the option of none. 
it should return the object reference that was selected for the script that called it to fill in the required field. 
the selector ui should pass data to the Nvda api script located in the root directory 
the user must be able to navigat the list with the arrow keys 
## exiting the selection 
if the user selects an option the selection should automatically exit back to the window they were last in. 
if they press esc it should perform the same action and not return anything. 