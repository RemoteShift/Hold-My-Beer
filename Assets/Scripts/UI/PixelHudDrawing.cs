using UnityEngine;
using UnityEngine.UI;

internal static class PixelHudDrawing
{
    internal static void Box(VertexHelper mesh, float x, float y, float width, float height, Color tint)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(new Vector3(x, y), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x, y + height), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x + width, y + height), tint, Vector2.zero);
        mesh.AddVert(new Vector3(x + width, y), tint, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    internal static void Star(VertexHelper mesh, float x, float y, float pixel, int arms, Color tint)
    {
        x = Mathf.Round(x / pixel) * pixel;
        y = Mathf.Round(y / pixel) * pixel;
        Box(mesh, x, y, pixel, pixel, tint);
        for (int i = 1; i <= arms; i++)
        {
            Box(mesh, x + i * pixel, y, pixel, pixel, tint);
            Box(mesh, x - i * pixel, y, pixel, pixel, tint);
            Box(mesh, x, y + i * pixel, pixel, pixel, tint);
            Box(mesh, x, y - i * pixel, pixel, pixel, tint);
        }
    }
}
