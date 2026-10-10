# Chess3DUnity
A single-player 3D chess game. Play White against a built-in AI. Built with Unity 6 (URP) and low-poly models from the Synty POLYGON Starter Pack; the rules engine is plain C#, separate from Unity, and developed test-first.

## Local setup (paid/licensed assets are not in this repo)

1. Open with Unity 6000.4.9f1.
2. Import **Low Poly Chess Pack** (Broken Vector) from the Asset Store.
3. Convert `Assets/BrokenVector/LowpolyChessPack/Materials/Chess Material.mat` to **Universal Render Pipeline/Lit**
   (the pack ships a Built-in Standard material, which renders magenta in URP). Move `_MainTex` to `_BaseMap`.

## Tests

`dotnet test DotnetTests` (fast) or Unity Test Runner, EditMode.
