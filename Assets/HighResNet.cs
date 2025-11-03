using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HighResNet : MonoBehaviour
{
    [Header("网格细分")]
    public int widthSegments = 25;
    public int heightSegments = 25;
    public float width = 5f;
    public float height = 5f;

    void Start()
    {
        // 生成高分辨率平面
        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(widthSegments + 1) * (heightSegments + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[widthSegments * heightSegments * 6];

        for (int y = 0; y <= heightSegments; y++)
        {
            for (int x = 0; x <= widthSegments; x++)
            {
                vertices[y * (widthSegments + 1) + x] =
                    new Vector3((x - widthSegments / 2f) * (width / widthSegments),
                                0,
                                (y - heightSegments / 2f) * (height / heightSegments));
                uv[y * (widthSegments + 1) + x] = new Vector2((float)x / widthSegments, (float)y / heightSegments);
            }
        }

        int t = 0;
        for (int y = 0; y < heightSegments; y++)
        {
            for (int x = 0; x < widthSegments; x++)
            {
                int i = y * (widthSegments + 1) + x;
                triangles[t++] = i;
                triangles[t++] = i + widthSegments + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + 1;
                triangles[t++] = i + widthSegments + 1;
                triangles[t++] = i + widthSegments + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        // 添加碰撞体
        MeshCollider mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = false;

        // 添加 Cloth
        Cloth cloth = gameObject.AddComponent<Cloth>();
        cloth.useGravity = true;
        cloth.damping = 0.2f;
        cloth.stretchingStiffness = 0.4f;
        cloth.bendingStiffness = 0.2f;

        // 添加 Rigidbody
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 0.3f;
    }
}
