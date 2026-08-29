# options.md 
this describes the additional options the user will be presented with when they use shift f10 
## rules 
use all accessibility rules defined in the project 
use the accessible UI controls defined in ./utils 
## images 
this applies to all supported image types 
### options 
the user should be given the following options: 
- convert to texture 
- convert to sprite 
### useful API 
use the unity editor api UnityEditor.TextureImporter
import it like this 
using UnityEditor;

### unity api values and options table 
convert to sprite - TextureImporterType.Sprite
convert to texture - TextureImporterType.Default