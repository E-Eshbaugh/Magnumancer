# Horde models

The Cinder Crucible horde uses two original low-poly mesh sets on the existing KayKit skeletons:

- **Ashwalker** replaces the visuals in `Assets/Prefabs/Goblin.prefab`: an angular undead goblin with hollow amber eyes, exposed ribs, hooked fingers, torn burgundy cloth, a damaged pauldron, and mismatched footwear. 3,048 triangles.
- **Cinderbound** replaces the visuals in `Assets/Prefabs/LargeBoss.prefab`: an armored undead brute with a broken crown, broad spiked shoulders, furnace-like ribs, and plated boots. 3,510 triangles.

Both use `Assets/Art/Horde/Horde.mat`, a shared URP Lit material with a small point-filtered color palette and an emission mask. Six skinned mesh parts per character preserve the source rig's bone names and order. The old rigid helmets, shields, capes, and weapons are disabled through prefab overrides. The original imported models remain available.

Health, spawn weights, collision shapes, navigation, prefab scale, animation controllers, and scene references are unchanged. The brute retains its existing larger in-game scale. These are visual replacements, not new gameplay enemy types.

To edit their geometry or palette, change `Assets/Editor/Horde/HordeModelBuilder.cs`, then choose **Magnumancer → Art → Rebuild Horde Models** in Unity. This rebuilds the saved mesh assets while preserving their GUIDs and applies the visuals to the two existing enemy prefabs. The builder samples Idle, Walking_B, and Running_A into padded visibility bounds so moving limbs remain visible. No meshes are generated at runtime.

Validation uses Unity 6000.6.3f1 with the original FBX import settings and URP 17.6.0. Check the in-game pixel camera and arena lighting when reviewing the final look; the supplied preview uses neutral studio lighting.

The studio preview presents both models at comparable scale for inspection. The arena retains their original size difference. Prefab validation confirmed all six replacement parts per character, matching bind poses, valid bone weights, disabled stock accessories, emissive URP materials, and movement at five sample times for each of the three animations.
