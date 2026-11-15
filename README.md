# FurnishAR

A vintage furniture store app in AR, made for the CEN451 Mixed Reality course (Fall 2026).
The project was started from the Furnish AR example of the freeCodeCamp AR Foundation course and was extended after that.

## Features

- Furniture is picked from a catalog and placed on the floor with a preview.
- Realistic PBR models are used in a vintage catalog of eight items.
- Real objects hide virtual furniture (environment depth occlusion).
- The scene light is driven by ARCore light estimation (direction, color, spherical harmonics).
- A product card with name, size and price is shown for the selected item.
- Occlusion and light estimation can be turned off from the options menu to compare the results.

## Timeline

1. **First version (October 2026).** The Unity AR Mobile template was set up with Vulkan and AR Foundation 6.5. Five low-poly models from the Kenney Furniture Kit were used. They were scaled to real size (0.2) and turned to face the user. Plane detection was cleaned up and a placement reticle was added.
2. **Risk test.** Before new features were built, depth support, light estimation and the Linear color space were checked on the phone with a debug overlay. All three were supported.
3. **Placement mode.** Placing was moved into a mode that starts from the catalog. A preview was added, and the preview follows the finger.
4. **Realistic models.** The Kenney models were replaced with Poly Haven models (glTF, imported with glTFast). An editor tool was written to center the models and set up their colliders.
5. **Light estimation.** ARCore light estimation was applied to the scene light, the ambient light, the shadow strength and the reflections.
6. **Occlusion.** Environment depth occlusion was enabled and a custom background shader with depth bias was added.
7. **Interface.** A welcome screen, generated catalog icons, a product card and the comparison toggles were added.
8. **Demo (November 2026).** The app was shown on a Samsung Galaxy S25 FE.

## Problems and decisions

| Problem | What was done |
|---|---|
| A second chair was spawned when a placed chair was rotated with two fingers. The first finger was counted as a tap. | Placing was moved into a mode. An item can be placed only after it is picked from the catalog, and only once. Multi-finger touches and touches on the UI or on placed items are ignored. |
| On a quick tap, the item was placed a few cm behind the finger, because the smoothed reticle had not caught up yet. | The reticle is snapped to the release point before the item is placed. |
| In a small room, the phone had to be tilted a lot before the floor was found in the center of the screen. | The default aim point was moved lower (35% of the screen height), and the finger was made the aim point while it is on the screen. |
| The planes were hidden after the first item, so placing the second item was hard. | Planes are shown only when they help: empty scene, open menu or while placing. |
| The Kenney models looked like toys. Few modern models were found on Poly Haven, but many vintage ones were found. | A vintage catalog was chosen. The whole catalog is about 54k triangles. |
| The oil lamp got a 1×1×1 collider, because its mesh is in a child object. | The bounds are calculated from all child meshes in an editor tool. |
| Small items like the candlestick were hard to tap. | Their touch area was made at least 25 cm (Fitts' law). |
| In a bright hallway, the furniture looked white and blue. ARCore reported about 5× more light, but the phone camera had auto exposure and auto white balance. | The total light level is compressed (K^0.4), and only 30% of the light color is used (photometric registration). |
| The wood was still shiny in a dark room, because the default reflection was the Unity sky. | The reflection strength is scaled with the room brightness. |
| After occlusion was enabled, small items sank into the floor and the floor shadows disappeared. The ARCore depth is a few cm off, and the shadow plane is at the same depth as the real floor. | A copy of the ARCore background shader was made with a distance-based depth bias (similar to shadow bias). |
| The floor shadows were rippling. | The depth bias was increased and the light estimation was smoothed more. |
| The generated icons looked tiny and off center for tall or wide items. | The rendered icons are cropped to their visible pixels. |

## Known limitations

- The depth from ARCore has a low resolution, so the edges of hidden parts look jagged, especially on small items.
- Thin and moving objects (for example hanging clothes) can be measured wrong.
- Occlusion gets weaker in low light and closer than about half a meter.
- Removing real furniture from the view (diminished reality) and saving a room layout were left as future work.

## Tech

- Unity 6.3 LTS, URP, Vulkan, Linear color space
- AR Foundation 6.5 with ARCore, XR Interaction Toolkit
- Tested on Samsung Galaxy S25 FE

## Build

The project is opened with Unity 6.3 LTS and built for Android. The main scene is `Assets/Scenes/SampleScene.unity`.
A device with ARCore Depth API support is needed for occlusion.

## Credits

- Unity AR Mobile template
- [Kenney Furniture Kit](https://kenney.nl) (CC0), used in the first version
- 3D models from [Poly Haven](https://polyhaven.com) (CC0)
- Parts of the code were developed with the help of an AI assistant. All features were tested and tuned on device.
