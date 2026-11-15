# FurnishAR

A vintage furniture store app in AR, made for the CEN451 Mixed Reality course (Fall 2026).
The project was started from the Furnish AR example of the freeCodeCamp AR Foundation course and was extended after that.

## Features

- Furniture is picked from a catalog and placed on the floor with a preview. Accidental spawns during rotate gestures were fixed with a placement mode.
- Realistic PBR models are used instead of the original low-poly ones.
- Real objects hide virtual furniture (environment depth occlusion). A depth bias was added because small items were sinking into the floor and floor shadows were disappearing.
- The scene light is driven by ARCore light estimation (direction, color, spherical harmonics). Light values are compressed so that they match the auto exposure and white balance of the phone camera.
- A product card with name, size and price is shown for the selected item.
- Occlusion and light estimation can be turned off from the options menu to compare the results.

## Tech

- Unity 6.3 LTS, URP, Vulkan, Linear color space
- AR Foundation 6.5 with ARCore, XR Interaction Toolkit
- Tested on Samsung Galaxy S25 FE

## Build

The project is opened with Unity 6.3 LTS and built for Android. The main scene is `Assets/Scenes/SampleScene.unity`.
A device with ARCore Depth API support is needed for occlusion.

## Credits

- Unity AR Mobile template
- 3D models from [Poly Haven](https://polyhaven.com) (CC0)
- Parts of the code were developed with the help of an AI assistant. All features were tested and tuned on device.
