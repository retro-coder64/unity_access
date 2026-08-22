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
the inspector will display the properties of the object if they exist 
the inspector will check the object's serialised properties to determin what to show. 
the inspector will also show all the components attached to the object 


##editable properties 
the inspector will allow the user to edit the following properties 
- name of the object
- the transform of the object where applicable 
- - x position 
- - y position 
- - z position 
- - x rotation 
- - y rotation 
- - z rotation 
- - x scale 
- - y scale 
- - z scale 
- any other fields the inspector locates on an object 
- component properties [properties.md]component.md)
the user can also remove components by navigating to the component and pressing backspace. 

## adding components 
at the bottom of the inspector the user can use the add  component button to add a component. 
when the button is pressed the user will be shown a list of all the components including scripts. 
they can navigate the list using the arrow keys and select a component using enter 
##accessibility requirements 
the user must be able to use Nvda to navigate the inspector. 

the user should be able to exit the inspector, and return to the hierarchy, by pressing the escape key

## editing object fields 
use the following system to decide the type of edit that can be made 
ensure you use the accessible controls in the utils folder 
### representing property types 
bool value: check box / toggle 
int value: editable field 
float value: editable field 
string value: editable field 
enum: list/combo box 
vector2: 
number edit field x 
number edit field y 
vector3: 
number edit field x
number edit field y
number edit field z 
object reference: use the object selector script located @ (./utils/object_selector.cs)
