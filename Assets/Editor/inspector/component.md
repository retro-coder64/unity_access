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
the component view should use the serialised properties provided for the component from the unity editor api. 
it should use this data to determin the property type 
it should be able to detect: 
bool
integer 
float 
string 
vector 3d 
vector 2d 
enum
object reference 
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
## leaving the view 
if the user presses esc the view will exit back to the inspector. 