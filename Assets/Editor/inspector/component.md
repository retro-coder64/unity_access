# component.md 
this file describes the component view in the inspector 
## component view 
the component view is activated when the user selects a component in the inspector 
it will show the properties of the component 
it will get the information through the serialised properties that the unity editor provides. 
it will use the same accessibility structure as the inspector 
the user can edit the properties in the component view. 
## editing properties 
### identifying properties
the component view should use this sequence for locating properties. 
1. read the serialised properties of the component. 
2. then use public c# property reflection to find any others. 
3. filter all the located properties so that only editable properties will be shown. 
4. determin the type of the property such as:
bool
int
float
Vector2
Vector3 
string 
use the representing property types as a guide. 
5. determin which controls from ./utils will be required.
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
and others such as colour 
## leaving the view 
if the user presses esc the view will exit back to the inspector. 
## searching 
allow the user to search for properties of components 
use the methods defined in ./utils 
as the user types the properties that have been located and most closely match what they are searching for are shown first. 
it should also report when there are no results 
when the user is editing a value, the search will not activate 