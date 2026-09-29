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
## Models

This applies to all models.

### Configure Rig

The user will be able to configure the rig type.

The available options will be:

- Generic
- Humanoid

These options will be presented in a **Configure Rig** submenu in the model's Options menu.

### API

Use the `ModelImporter` API.

Set the rig type using the `ModelImporter.animationType` property and the `ModelImporterAnimationType` enum.

### Table of Values

| Rig type | API value |
| --- | --- |
| None | `ModelImporterAnimationType.None` |
| Humanoid | `ModelImporterAnimationType.Human` |
| Generic | `ModelImporterAnimationType.Generic` |

For the initial Unity Access implementation, only **Generic** and **Humanoid** need to be presented to the user.

### Avatar Setup

If the user chooses **Humanoid**, configure the model's Avatar.

For now, Unity Access will create the Avatar from the current model:

`importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;`

The rig type should also be set to Humanoid:

`importer.animationType = ModelImporterAnimationType.Human;`

### Generic Rig

If the user chooses **Generic**, set:

`importer.animationType = ModelImporterAnimationType.Generic;`

### Saving and Reimporting

After changing the rig configuration, save the importer settings and reimport the model:

`importer.SaveAndReimport();`

This applies the new import settings to the model.