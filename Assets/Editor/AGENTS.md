#AGENTS.md 
an accessibility plugin for the unity editor version 6.3, written in c#.
##project structure 
unity_access/ (root directory of the project do not edit)
unity_access/Assets/Editor/
unity_access/Assets/Editor/unity_access.cs (will be the main file of the plugin)
unity_access/Assets/Editor/utils/ (where utility files will be placed)
unity_access/Assets/Editor/tests/
unity_access/Assets/Editor/inspector/
unity_access/AssetsEditor/hierarchy/
do not edit files, unless told to do so by the user, in the root directory. 
only use the working directory 
## project language
the project is written in c#
it will use the unity namespaces such as UnityEditor 
all code must be validated and strict typing is to be inforced. 

## testing 
all test files will be placed in unity_access/Editor/test 
the file name and the test will be noted in unity_access/Editor/tests.txt 
the test files are not to be used in the main script 
## error handling 
all code will pass errors into unity_access/Editor/debug.txt 
it should follow the structure of:
filename 
error 
## changes 
all changes will be recorded in unity_access/Editor/changes.txt 
they should follow the format: 
filename 
change description
line number of the start of the change 
line number of the end of change 
## accessibility 
the code will use the NVDA API to send text from the editor 
no feature is completed until the user can navigate it with NVDA 
create a file in this directory that will be responsible for handling the NVDA api. 
it should be callable by all files and it should be able to take data and pass it accurately. 

##best practices 
prefer small direct changes
all changes will be noted in the changes.txt with the correct format 
all code must be clearly commented 


## useful commands 
use UnityEditor

## useful scripts 
use the following scripts located in the /utils folder 
- AccessibleControls.cs for buttons, editable text boxes and tool bars search fields.. 
- 
  - AccessibleEditorStyles.cs
    Defines the shared blue selection highlight and draws it behind selected controls.
- AccessibleTextEdit.cs

    Stores whether a text box is being edited and manages its value and begin/end editing lifecycle.
    - accessibleList.cs
    Handles list selection movement, index clamping, “X of Y” announcements, automatic scrolling, and selected-row
    rendering.
- AccessibleSpeech.cs
    Sends text through the existing NvdaApi. Speech errors are safely recorded through PluginErrorLog.





## startup 
give the unity access plugin a button to start it in the menu bar 