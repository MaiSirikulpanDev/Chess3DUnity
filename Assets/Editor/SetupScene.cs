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
    const float StumpHeightScale = 7f, StumpWidthScale = 34f;
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

        // Stump: scaled wide and squat, shifted so its flat top is centred under the board.
        float groundY = BoardBottom - (StumpTopY - StumpBottomY) * StumpHeightScale;
        var stump = Put(root, "Trees/PT_Fruit_Tree_01_stump", new Vector3(-StumpTopCentre.x * StumpWidthScale, BoardBottom - StumpTopY * StumpHeightScale, -StumpTopCentre.y * StumpWidthScale),
            0, new Vector3(StumpWidthScale, StumpHeightScale, StumpWidthScale), "Stump");
        stump.transform.SetParent(null); // the stump stands on its own in the hierarchy

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.transform.position = new Vector3(0, groundY + 0.3f, 0); // the roots sink slightly into the soil
        ground.transform.localScale = Vector3.one * 80;
        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

        Vector3 Ring(float inner, float outer)
        {
            float angle = Rand(0, 2 * Mathf.PI), radius = Rand(inner, outer);
            return new Vector3(Mathf.Cos(angle) * radius, groundY, Mathf.Sin(angle) * radius);
        }
        void Scatter(string path, int count, float inner, float outer, float smallest, float largest)
        {
            for (int i = 0; i < count; i++) Put(root, path, Ring(inner, outer), Rand(0, 360), Vector3.one * Rand(smallest, largest));
        }

        // Trees: mostly pines, a few broadleaf and dead ones. Nothing closer than 40 units.
        Scatter("Trees/PT_Pine_Tree_03_green", 55, 40, 130, 4.5f, 6.5f);
        Scatter("Trees/PT_Fruit_Tree_01_green", 14, 40, 110, 4f, 5.5f);
        Scatter("Trees/PT_Pine_Tree_03_dead", 8, 45, 120, 4.5f, 6f);
        Scatter("Trees/PT_Fruit_Tree_01_dead", 4, 50, 100, 4f, 5f);
        // Undergrowth around the stump, scaled up to match the stump's size.
        Scatter("Shrubs/PT_Generic_Shrub_01_green", 14, 24, 55, 4f, 6.5f);
        Scatter("Shrubs/PT_Generic_Shrub_01_dead", 4, 30, 60, 4f, 6f);
        Scatter("Rocks/PT_Generic_Rock_01", 10, 23, 50, 14f, 26f);
        Scatter("Rocks/PT_River_Rock_Pile_02", 5, 24, 45, 6f, 10f);
        Scatter("Rocks/PT_Menhir_Rock_02", 2, 55, 75, 4f, 5f);
        Scatter("Mushrooms/PT_Caesars_Mushroom_01", 12, 30, 48, 6f, 10f);
        Scatter("Plants/PT_Grass_02", 80, 22, 65, 5f, 8f);
        Scatter("Flowers/PT_Poppy_02", 40, 22, 55, 6f, 10f);
        for (int i = 0; i < 2; i++) Put(root, "Trees/PT_Pine_Tree_03_logs", Ring(32, 42), Rand(0, 360), Vector3.one * Rand(5f, 6f), "Logs");

        var sun = Object.FindFirstObjectByType<Light>();
        sun.color = new Color(1f, 0.95f, 0.82f);
        sun.intensity = 1.3f;
        sun.transform.rotation = Quaternion.Euler(48, -40, 0);
        sun.shadows = LightShadows.Soft;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.007f;
        RenderSettings.fogColor = mist;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.45f);
    }

    static GameObject Put(Transform parent, string path, Vector3 position, float yaw, Vector3 scale, string name = null)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Env + "Prefabs/" + path + ".prefab");
        if (asset == null) throw new System.IO.FileNotFoundException("Missing prefab " + path + ": import the Polytope Studio pack and its URP 17 package");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = scale;
        if (name != null) go.name = name;
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
        mat.SetTextureScale("_BaseMap", new Vector2(70, 70));
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
