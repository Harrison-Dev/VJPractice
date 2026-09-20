using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VJPractice;

public static class ProjectSetup {
    [MenuItem("VJ Practice/Create Starter Scene")]
    public static void Create() {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Output Camera").AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=1; camera.transform.position=new Vector3(0,0,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        camera.gameObject.AddComponent<AudioListener>();
        var root=new GameObject("VJ Instrument"); var instrument=root.AddComponent<VJInstrument>();
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name="Procedural Visual"; quad.transform.SetParent(root.transform); quad.transform.localScale=new Vector3(200,2,1);
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        // Oversized quad keeps the surface full-screen; shader derives coordinates from screen position.
        var shader=Shader.Find("VJ/Geometry"); if(shader==null) throw new System.Exception("Missing visual shader");
        var material=new Material(shader); AssetDatabase.CreateAsset(material,"Assets/VJ/Materials/Geometry.mat");
        quad.GetComponent<MeshRenderer>().sharedMaterial=material; instrument.visual=material;
        EditorSceneManager.SaveScene(scene,"Assets/VJ/Scenes/01_GeometryPractice.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
        PlayerSettings.defaultScreenWidth=1920; PlayerSettings.defaultScreenHeight=1080;
        PlayerSettings.runInBackground=true; PlayerSettings.companyName="AV Sketchbook";
        AssetDatabase.SaveAssets(); Debug.Log("VJ_SETUP_OK");
    }
}
