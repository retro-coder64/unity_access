# AGENTS.md
this is the md file describing the accessible consol of the unity editor
## function overview 
the consol is responsible for providing the user information about their project. 
it will show the messages from the unity consol in an accessible format. 
the consol will not be editable
the user can coppy messages out of it 
it will use the Nvda API script [path](./NvdaApi.cs)


## ui and representation 
the consol will be a bar at the bottom of the screen 
it will show the text from the normal unity consol 
when displaying the consol use the following colours for text: 
- normal log: white 
- warning: orange 
- error: red 
- assertion failier: red 
- exception: red 
## opening consol 
the consol will be opened when the user presses the consol button in the unity access menu or when they enter ctrl + shift + c. 
the consol will also open automatically if there is an error that unity is showing. 
## closing the consol 
the consol will close when the user presses escape 
it will return the focus back to the main window 

## navigation 
the user will be able to navigate the consol with the arrow keys. 
pressing ctrl + c on a message will coppy the message to the clip board. 
at the top of the consol window there will be a button to clear the consol. 

## useful commands 
Application.logMessageReceived (subscribes to the consol messages)
Application.logMessageReceivedThreaded (for threaded logs. )


## determining the type of log 
unity will pass a LogType enum to the call back.
- LogType.Log (normal message)
- LogType.Warning (warning)
- LogType.Error (error)
- LogType.Assert (assertion failier) 
- LogType.Exception (exceptions)

## rules
no feature is complete until the user can use it with a screen reader. 
comment all code clearly 
remember to add the changes to the changes.txt file located in (./changes.txt)