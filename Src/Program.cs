using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
using Graph3D.Vrml.Nodes.Geometry;
using Graph3D.Vrml.Nodes.Grouping;
using Graph3D.Vrml.Parser;
using Graph3D.Vrml.Tokenizer;

namespace VrmlFixer;

internal class Program
{
    static void Main(string[] args)
    {
        var p = new VrmlParser(new Vrml97Tokenizer(new StringReader(File.ReadAllText(@"..\..\..\Pico.wrl"))));
        var scene = new VrmlScene();
        p.Parse(scene);

        using (var sw = new StreamWriter(@"..\..\..\Pico-rewrite.wrl"))
        {
            var wr = new VrmlWriter(sw) { Indent = 0 };
            wr.AssignNodeIds(scene.Root);
            DeleteJunk(wr, scene.Root);
            UnifyShapes(wr, scene.Root);
            wr.WriteScene(scene);
            //printNodeLengths(wr, scene.Root, 0);
            //printLargeShapes(wr);
        }
    }

    private static bool DeleteJunk(VrmlWriter wr, BaseNode node)
    {
        if (node == null)
            return false;

        // groups 18862 and 18863 are empty
        if (node is GroupNode && wr.NodeIds[node] != 14607)
            return true; // select specific group only
        if (node is ShapeNode shn)
        {
            if (shn.Geometry is IndexedLineSetNode)
                return true;
            //if (shn.Geometry is IndexedFaceSetNode)
            //    return true; // remove EVERYTHING
            //if (shn.Geometry is IndexedFaceSetNode ifs && ifs.CoordIndex.Length <= 4)
            //    return true; // some large planes defined with these
            //return new[] { 8680, 8685, 8708, 8711, 8714, 8717, 8696 }.Contains(wr.NodeIds[node]);
            //if (shn.Geometry is IndexedFaceSetNode ifs && ifs.CoordIndex.Length > 5)
            //    return true;
            //return shn.Geometry is IndexedFaceSetNode ifs && ifs.CoordIndex.Length < 10;
            //if (shn.Geometry is IndexedFaceSetNode)
            //    return Random.Shared.NextDouble() > 0.1;
        }

        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                DeleteJunk(wr, sfNode.Node);
            else if (field.Value is MFNode mfNode)
                mfNode._values.RemoveAll(n => DeleteJunk(wr, n));
        }

        if (node is GroupNode gn && gn.Children.Length == 0)
            return true;
        else if (node is TransformNode tn2 && tn2.Children.Length == 0)
            return true;

        return false;
    }

    private static void printNodeLengths(VrmlWriter wr, BaseNode node, int depth)
    {
        if (node == null)
            return;
        if (depth > 0)
        {
            //if (!wr.NodeLengths.ContainsKey(node) || wr.NodeLengths[node] < 5000)
            //    return;
            if (node is GroupNode)
                Console.WriteLine($"{new string(' ', depth * 2)}[#{wr.NodeIds[node]}] {node.GetType().Name}: {wr.NodeLengths[node]:#,0}");
        }
        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                printNodeLengths(wr, sfNode.Node, depth + 1);
            else if (field.Value is MFNode mfNode)
                foreach (var n in mfNode)
                    printNodeLengths(wr, n, depth + 1);
        }
    }

    private static void printLargeShapes(VrmlWriter wr)
    {
        wr.NodeLengths.Where(kvp => kvp.Key is ShapeNode)
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .ToList()
            .ForEach(kvp => Console.WriteLine($"[#{wr.NodeIds[kvp.Key]}] {kvp.Key.GetType().Name}: {kvp.Value:0,0}"));
    }

    record class Point(float X, float Y, float Z)
    {
        public static implicit operator Point(SFVec3f p) => new(p.X, p.Y, p.Z);
        public static implicit operator SFVec3f(Point p) => new(p.X, p.Y, p.Z);
    }

    private static List<List<Point>> GetFaces(IndexedFaceSetNode ifs)
    {
        var faces = new List<List<Point>>();
        var coord = (CoordinateNode)ifs.Coord.Node;
        var current = new List<Point>();
        foreach (var i in ifs.CoordIndex)
        {
            if (i == -1)
            {
                faces.Add(current);
                current = new List<Point>();
            }
            else
            {
                current.Add(coord.Point[i]);
            }
        }
        return faces;
    }

    private static void UnifyShapes(VrmlWriter wr, BaseNode node)
    {
        if (node == null)
            return;

        if (node is GroupNode gn)
        {
            // unify all the shapes by appearance, by contactenating their faces together and deduplicating the points
            var ifsShapes = gn.Children.OfType<ShapeNode>().Where(sn => sn.Geometry is IndexedFaceSetNode).ToHashSet();
            var otherChildren = gn.Children.Where(c => !ifsShapes.Contains(c)).ToList();
            gn.Children._values.Clear();
            gn.Children._values.AddRange(otherChildren);
            var groups = ifsShapes.GroupBy(s => s.Appearance);
            foreach (var group in groups)
            {
                var faces = group.Select(s => GetFaces((IndexedFaceSetNode)s.Geometry)).SelectMany(f => f).ToList();
                // deduplicate the faces - primitive; ignores normal direction
                faces = faces.DistinctBy(pts => string.Join("|", pts.Select(pt => $"{pt.X},{pt.Y},{pt.Z}").Order())).ToList();
                var newShape = new ShapeNode();
                newShape.Appearance = group.Key;
                var newifs = new IndexedFaceSetNode();
                newifs.Solid.Value = false;
                newShape.Geometry = newifs;
                var points = faces.SelectMany(f => f).Distinct().ToList();
                var cn = new CoordinateNode();
                foreach (var pt in points)
                    cn.Point.AppendValue(pt);
                newifs.Coord.Node = cn;
                newifs.CoordIndex.ClearValues();
                foreach (var face in faces)
                {
                    foreach (var pt in face)
                        newifs.CoordIndex.AppendValue(new SFInt32(points.IndexOf(pt)));
                    newifs.CoordIndex.AppendValue(new SFInt32(-1));
                }
                gn.Children._values.Add(newShape);
            }
        }

        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                UnifyShapes(wr, sfNode.Node);
            else if (field.Value is MFNode mfNode)
                foreach (var n in mfNode)
                    UnifyShapes(wr, n);
        }
    }
}
