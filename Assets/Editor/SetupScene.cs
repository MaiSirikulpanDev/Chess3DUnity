using Chess.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Chess > Build Scene: assembles Assets/Scenes/Main.unity from the Low Poly Chess Pack and the Polytope Studio
// "Nature Free" pack (import its URP 17 package first, see the README).
public static class SetupScene
{
    const string Prefabs = "Assets/BrokenVector/LowpolyChessPack/Prefabs/";
    const string Env = "Assets/Polytope Studio/Lowpoly_Environments/";

    // The board's underside is at y = -0.277 (see ChessView.FieldY); the stump's top goes exactly there.
    const float BoardBottom = -0.277f;
    const float StumpScale = 34f; // the same on every axis, so the stump keeps its natural shape
    // Where the stump mesh's flat top sits in its own model space (measured from its vertices).
    static readonly Vector2 StumpTopCentre = new Vector2(-0.078f, -0.1245f);
    const float StumpTopY = 0.6239f, StumpBottomY = -0.2922f;

    [MenuItem("Chess/Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var board = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Board.prefab"));
        board.transform.rotation = Quaternion.Euler(0, 90, 0); // puts a dark square on a1, bottom-left for White

        const string matPath = "Assets/Chess/Highlight.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        var view = new GameObject("Chess").AddComponent<ChessView>();
        view.whitePrefabs = Load("White ");
        view.blackPrefabs = Load("Black ");
        view.highlight = mat;

        var mist = new Color(0.62f, 0.72f, 0.7f);
        var cam = Camera.main;
        cam.transform.SetPositionAndRotation(new Vector3(0, 14, -12), Quaternion.Euler(50, 0, 0));
        cam.fieldOfView = 46;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = mist;
        cam.farClipPlane = 1500;

        BuildForest(mist);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
    }

    // The board stands on a wide stump in a clearing; trees stand well outside the circle the camera swings through.
    static void BuildForest(Color mist)
    {
        var rng = new System.Random(42); // fixed seed: rebuilding the scene gives the same forest
        float Rand(float low, float high) => low + (float)rng.NextDouble() * (high - low);
        var root = new GameObject("Forest").transform;

        // Stump: a tall natural stump, shifted so its flat top is centred under the board.
        float groundY = BoardBottom - (StumpTopY - StumpBottomY) * StumpScale;
        var stump = Put(root, "Trees/PT_Fruit_Tree_01_stump", new Vector3(-StumpTopCentre.x * StumpScale, BoardBottom - StumpTopY * StumpScale, -StumpTopCentre.y * StumpScale),
            0, Vector3.one * StumpScale, "Stump");
        stump.transform.SetParent(null); // the stump stands on its own in the hierarchy

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.position = new Vector3(0, groundY + 1f, 0); // the roots sink slightly into the soil
        ground.transform.localScale = Vector3.one * 150;
        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

        Vector3 Ring(float inner, float outer)
        {
            float angle = Rand(0, 2 * Mathf.PI), radius = Rand(inner, outer);
            return new Vector3(Mathf.Cos(angle) * radius, groundY, Mathf.Sin(angle) * radius);
        }
        void Scatter(string path, int count, float inner, float outer, float smallest, float largest, bool castShadows = true)
        {
            for (int i = 0; i < count; i++) Put(root, path, Ring(inner, outer), Rand(0, 360), Vector3.one * Rand(smallest, largest), null, castShadows);
        }

        // The world is built to the stump's scale (a giant stump, giant trees): the board is a tabletop on it.
        // Trees: mostly pines, a few broadleaf and dead ones, all well outside the camera's orbit.
        Scatter("Trees/PT_Pine_Tree_03_green", 90, 90, 400, 22f, 32f, false);
        Scatter("Trees/PT_Fruit_Tree_01_green", 22, 90, 380, 20f, 28f, false);
        Scatter("Trees/PT_Pine_Tree_03_dead", 12, 100, 380, 22f, 30f, false);
        Scatter("Trees/PT_Fruit_Tree_01_dead", 6, 100, 350, 20f, 26f, false);
        // Undergrowth around the foot of the stump.
        Scatter("Shrubs/PT_Generic_Shrub_01_green", 16, 26, 120, 8f, 13f);
        Scatter("Shrubs/PT_Generic_Shrub_01_dead", 5, 30, 140, 8f, 12f);
        Scatter("Rocks/PT_Generic_Rock_01", 12, 24, 110, 50f, 90f);
        Scatter("Rocks/PT_River_Rock_Pile_02", 6, 26, 100, 30f, 50f);
        Scatter("Rocks/PT_Menhir_Rock_02", 2, 150, 250, 20f, 25f, false);
        Scatter("Mushrooms/PT_Caesars_Mushroom_01", 12, 45, 110, 22f, 32f);
        Scatter("Plants/PT_Grass_02", 100, 24, 180, 12f, 18f);
        Scatter("Flowers/PT_Poppy_02", 50, 24, 150, 14f, 22f);
        for (int i = 0; i < 2; i++) Put(root, "Trees/PT_Pine_Tree_03_logs", Ring(70, 110), Rand(0, 360), Vector3.one * Rand(24f, 30f), "Logs");

        var sun = Object.FindFirstObjectByType<Light>();
        sun.color = new Color(1f, 0.95f, 0.82f);
        sun.intensity = 1.3f;
        sun.transform.rotation = Quaternion.Euler(48, -40, 0);
        sun.shadows = LightShadows.Soft;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0018f;
        RenderSettings.fogColor = mist;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.45f);
    }

    static GameObject Put(Transform parent, string path, Vector3 position, float yaw, Vector3 scale, string name = null, bool castShadows = true)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Env + "Prefabs/" + path + ".prefab");
        if (asset == null) throw new System.IO.FileNotFoundException("Missing prefab " + path + ": import the Polytope Studio pack and its URP 17 package");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = scale;
        if (name != null) go.name = name;
        if (!castShadows)
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    static Material GroundMaterial()
    {
        const string path = "Assets/Chess/Ground.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        var grass = AssetDatabase.LoadAssetAtPath<Texture2D>(Env + "Sources/Textures/PT_Ground_Grass_Green_01.png");
        mat.SetTexture("_BaseMap", grass);
        mat.SetTextureScale("_BaseMap", new Vector2(100, 100));
        mat.SetColor("_BaseColor", new Color(0.75f, 0.85f, 0.7f));
        mat.SetFloat("_Smoothness", 0);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject[] Load(string colour)
    {
        var names = new[] { "King", "Queen", "Bishop", "Knight", "Rook", "Pawn" };
        var result = new GameObject[6];
        for (int i = 0; i < 6; i++) result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + colour + names[i] + ".prefab");
        return result;
    }
}
