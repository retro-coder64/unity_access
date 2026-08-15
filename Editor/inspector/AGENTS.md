#AGENTS.md 
this is the accessible inspector for the selected object from the accessible hierarchy 
##description 
the inspector shows the properties of the selected object 
it also allows the user to edit these properties 
the inspector uses the UnityEditor api and sends text to Nvda using the script @ "./NvdaApi.cs"
##data aquizition 
the inspector watches the shared data from the hierarchy (hierarchy located in "./hierarchy/")
the inspector uses the unity editor api to collect the necessary data about the object. 
the hierarchy and inspector do not call each other. 

##properties to be shown 
the inspector will show the following properties 
1. x position 
2. y position 
3. z position 
4. x rotation 
5. y rotation 
6. z rotation 
7. the child components of the object 

##accessibility requirements 
the user must be able to use Nvda to navigate the inspector. 

the user should be able to exit the inspector, and return to the hierarchy, by pressing the escape key
