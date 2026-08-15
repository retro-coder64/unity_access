#AGENTS.md 
this file describes the accessible hierarchy view for the unity editor 
##function description 
this element of the programme has the following responsibilities: 
1. locates all objects in the seen 
2. exposes there names to the nvda api 
3. allows them to be selected and handles the transition to the inspector automatically. (for now just show a dialogue box saying "inspector opened")
the hierarchy view does not edit objects. It just grabs the information from the editor APIs that unity provides and allows a blind user to read the data. 
## activation 
the hierarchy will be activated if one of the following occurs: 
1. the user starts the unity_access plugin from the menu bar
2. if the user presses i 
3. if the user exits the inspector 

## useful code 
using UnityEditor;
Resources.FindObjectsOfTypeAll<GameObject>();

## rules
1. do not edit files outside of the working directory unless told to do so by the user 
2. no feature is complete until the user can navigate it with nvda. 
3. use the script for passing data to NVDA when the hierarchy needs to pass data to NVDA screen reader. 
4. follow the bug reporting and change noting guidence in the root AGENTS.md located at "./AGENTS.md"