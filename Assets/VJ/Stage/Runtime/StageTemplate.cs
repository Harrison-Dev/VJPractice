using UnityEngine;
namespace VJPractice.Stage {
[CreateAssetMenu(menuName="VJ Practice/Stage Template")]
public sealed class StageTemplate : ScriptableObject {
    public string title, subtitle;
    [Range(0,5)] public int visualMode;
    public Color background=new Color(.025f,.025f,.06f), primary=Color.cyan, accent=Color.magenta;
    [Range(0,1)] public float energy=.45f, density=.6f, flow=.5f, echo=.65f;
    public int lyricLayout;
}
}
