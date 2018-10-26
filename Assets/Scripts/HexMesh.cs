using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour
{

    Mesh hexMesh;
    static List<Vector3> vertices = new List<Vector3>();
    static List<Color> colors = new List<Color>();
    static List<int> triangles = new List<int>();
    MeshCollider meshCollider;

    void Awake()
    {
        GetComponent<MeshFilter>().mesh = hexMesh = new Mesh();
        meshCollider = gameObject.AddComponent<MeshCollider>();

        hexMesh.name = "Hex Mesh";
    }

    public void Triangulate(HexCell[] cells)
    {
        hexMesh.Clear();
        vertices.Clear();
        colors.Clear();
        triangles.Clear();

        for (int i = 0; i < cells.Length; i++)
        {
            Triangulate(cells[i]);
        }
        hexMesh.vertices = vertices.ToArray();
        hexMesh.colors = colors.ToArray();
        hexMesh.triangles = triangles.ToArray();
        hexMesh.RecalculateNormals();
        meshCollider.sharedMesh = hexMesh;

    }

    void Triangulate(HexCell cell)
    {
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            Triangulate(d, cell);
        }
    }

    void Triangulate(HexDirection direction, HexCell cell)
    {
        Vector3 center = cell.Position;
        Vector3 v1 = center + HexMetrics.GetFirstSolidCorner(direction);
        Vector3 v2 = center + HexMetrics.GetSecondSolidCorner(direction);

        AddTriangle(center, v1, v2);
        AddTriangleColor(cell.Color);

        if (direction == HexDirection.NE)
        {
            TriangulateConnection(direction, cell, v1, v2);
        }

        if (direction <= HexDirection.SE)
        {
            TriangulateConnection(direction, cell, v1, v2);
        }
    }

    void TriangulateConnection(HexDirection direction, HexCell cell, Vector3 v1, Vector3 v2)
    {
        HexCell neighbor = cell.GetNeighbor(direction);

        if (neighbor == null)
        {
            return;
        }

        Vector3 bridge = HexMetrics.GetBridge(direction);
        Vector3 v3 = v1 + bridge;
        Vector3 v4 = v2 + bridge;
        v3.y = v4.y = neighbor.Elevation * HexMetrics.elevationStep;

        if (cell.GetEdgeType(direction) == HexEdgeType.Slope)
        {
            TriangulateEdgeTerraces(v1, v2, cell, v3, v4, neighbor);
        }
        else
        {
            AddQuad(v1, v2, v3, v4);
            AddQuadColor(cell.Color, neighbor.Color);
        }

        HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
        if (direction <= HexDirection.E && nextNeighbor != null)
        {
            Vector3 v5 = v2 + HexMetrics.GetBridge(direction.Next());
            v5.y = nextNeighbor.Elevation * HexMetrics.elevationStep;
            if (cell.Elevation <= neighbor.Elevation)
            {
                if (cell.Elevation <= nextNeighbor.Elevation)
                {
                    TriangulateCorner(v2, cell, v4, neighbor, v5, nextNeighbor);
                }
                else
                {
                    TriangulateCorner(v5, nextNeighbor, v2, cell, v4, neighbor);
                }
            }
            else if (neighbor.Elevation <= nextNeighbor.Elevation)
            {
                TriangulateCorner(v4, neighbor, v5, nextNeighbor, v2, cell);
            }
            else
            {
                TriangulateCorner(v5, nextNeighbor, v2, cell, v4, neighbor);
            }
        }
    }

    void TriangulateCorner( Vector3 bottom, HexCell bottomCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        HexEdgeType leftEdgeType = bottomCell.GetEdgeType(leftCell);
        HexEdgeType rightEdgeType = bottomCell.GetEdgeType(rightCell);

        if (leftEdgeType == HexEdgeType.Slope)
        {
            if (rightEdgeType == HexEdgeType.Slope)
            {
                TriangulateCornerTerraces(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
            }
            else if (rightEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(
                    left, leftCell, right, rightCell, bottom, bottomCell
                );
            }
            else
            {
                TriangulateCornerTerracesCliff(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
            }
        }
        else if (rightEdgeType == HexEdgeType.Slope)
        {
            if (leftEdgeType == HexEdgeType.Flat)
            {
                TriangulateCornerTerraces(
                    right, rightCell, bottom, bottomCell, left, leftCell
                );
            }
            else
            {
                TriangulateCornerCliffTerraces(
                    bottom, bottomCell, left, leftCell, right, rightCell
                );
            }
        }
        else if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            if (leftCell.Elevation < rightCell.Elevation)
            {
                TriangulateCornerCliffTerraces(
                    right, rightCell, bottom, bottomCell, left, leftCell
                );
            }
            else
            {
                TriangulateCornerTerracesCliff(
                    left, leftCell, right, rightCell, bottom, bottomCell
                );
            }
        }
        else
        {
            AddTriangle(bottom, left, right);
            AddTriangleColor(bottomCell.Color, leftCell.Color, rightCell.Color);
        }
    }

    void TriangulateCornerTerraces( Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        Vector3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
        Color c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);
        Color c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, 1);

        AddTriangle(begin, v3, v4);
        AddTriangleColor(beginCell.Color, c3, c4);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color c1 = c3;
            Color c2 = c4;
            v3 = HexMetrics.TerraceLerp(begin, left, i);
            v4 = HexMetrics.TerraceLerp(begin, right, i);
            c3 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);
            c4 = HexMetrics.TerraceLerp(beginCell.Color, rightCell.Color, i);
            AddQuad(v1, v2, v3, v4);
            AddQuadColor(c1, c2, c3, c4);
        }

        AddQuad(v3, v4, left, right);
        AddQuadColor(c3, c4, leftCell.Color, rightCell.Color);

    }

    void TriangulateCornerTerracesCliff( Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        float b = 1f / (rightCell.Elevation - beginCell.Elevation);
        if (b < 0)
        {
            b = -b;
        }
        Vector3 boundary = Vector3.Lerp(begin, right, b);
        Color boundaryColor = Color.Lerp(beginCell.Color, rightCell.Color, b);

        TriangulateBoundaryTriangle(begin, beginCell, left, leftCell, boundary, boundaryColor);

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, leftCell, right, rightCell, boundary, boundaryColor
            );
        }
        else
        {
            AddTriangle(left, right, boundary);
            AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
        }
    }

    void TriangulateCornerCliffTerraces(Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 right, HexCell rightCell)
    {
        float b = 1f / (leftCell.Elevation - beginCell.Elevation);
        if (b < 0)
        {
            b = -b;
        }
        Vector3 boundary = Vector3.Lerp(begin, left, b);
        Color boundaryColor = Color.Lerp(beginCell.Color, leftCell.Color, b);

        TriangulateBoundaryTriangle(
            right, rightCell, begin, beginCell, boundary, boundaryColor
        );

        if (leftCell.GetEdgeType(rightCell) == HexEdgeType.Slope)
        {
            TriangulateBoundaryTriangle(
                left, leftCell, right, rightCell, boundary, boundaryColor
            );
        }
        else
        {
            AddTriangle(left, right, boundary);
            AddTriangleColor(leftCell.Color, rightCell.Color, boundaryColor);
        }
    }

    void TriangulateBoundaryTriangle( Vector3 begin, HexCell beginCell, Vector3 left, HexCell leftCell, Vector3 boundary, Color boundaryColor)
    {
        Vector3 v2 = HexMetrics.TerraceLerp(begin, left, 1);
        Color c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, 1);

        AddTriangle(begin, v2, boundary);
        AddTriangleColor(beginCell.Color, c2, boundaryColor);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            Vector3 v1 = v2;
            Color c1 = c2;
            v2 = HexMetrics.TerraceLerp(begin, left, i);
            c2 = HexMetrics.TerraceLerp(beginCell.Color, leftCell.Color, i);
            AddTriangle(v1, v2, boundary);
            AddTriangleColor(c1, c2, boundaryColor);
        }

        AddTriangle(v2, left, boundary);
        AddTriangleColor(c2, leftCell.Color, boundaryColor);
    }

    void TriangulateEdgeTerraces(Vector3 beginLeft, Vector3 beginRight, HexCell beginCell, Vector3 endLeft, Vector3 endRight, HexCell endCell)
    {

        Vector3 v3 = HexMetrics.TerraceLerp(beginLeft, endLeft, 1);
        Vector3 v4 = HexMetrics.TerraceLerp(beginRight, endRight, 1);
        Color c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, 1);

        AddQuad(beginLeft, beginRight, v3, v4);
        AddQuadColor(beginCell.Color, c2);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            Vector3 v1 = v3;
            Vector3 v2 = v4;
            Color c1 = c2;
            v3 = HexMetrics.TerraceLerp(beginLeft, endLeft, i);
            v4 = HexMetrics.TerraceLerp(beginRight, endRight, i);
            c2 = HexMetrics.TerraceLerp(beginCell.Color, endCell.Color, i);
            AddQuad(v1, v2, v3, v4);
            AddQuadColor(c1, c2);
        }

        AddQuad(v3, v4, endLeft, endRight);
        AddQuadColor(c2, endCell.Color);
    }

    void AddTriangleColor(Color Color)
    {
        colors.Add(Color);
        colors.Add(Color);
        colors.Add(Color);
    }

    void AddTriangleColor(Color c1, Color c2, Color c3)
    {
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c3);
    }

    void AddTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int vertexIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
    }

    void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        int vertexIndex = vertices.Count;
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);
        vertices.Add(v4);
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 3);
    }

    void AddQuadColor(Color c1, Color c2, Color c3, Color c4)
    {
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c3);
        colors.Add(c4);
    }

    void AddQuadColor(Color c1, Color c2)
    {
        colors.Add(c1);
        colors.Add(c1);
        colors.Add(c2);
        colors.Add(c2);
    }

}