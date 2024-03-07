using System.Xml.Linq;
using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
using Graph3D.Vrml.Nodes.Appearance;
using Graph3D.Vrml.Nodes.Geometry;
using Graph3D.Vrml.Nodes.Grouping;
using Graph3D.Vrml.Parser;
using Graph3D.Vrml.Tokenizer;

namespace VrmlFixer;

internal class Program
{
    static void Main()
    {
        FinalRewriteOfBlenderX3d();
        // InitialRewriteOfPicoWrl();
    }

    static void FinalRewriteOfBlenderX3d()
    {
        var scn = new X3dReader().Read(XElement.Load(@"..\..\..\blender.x3d"));
        using var sw = new StreamWriter(@"..\..\..\blender.wrl");
        var wr = new VrmlWriter(sw) { Indent = 1 };
        wr.AssignNodeIds(scn.Root);
        UnifyShapes(wr, scn.Root);
        Cleanup(wr, scn.Root);
        UnifyIdenticalTransforms(wr, scn.Root);
        wr.WriteScene(scn);
    }

    static void InitialRewriteOfPicoWrl()
    {
        var p = new VrmlParser(new Vrml97Tokenizer(new StringReader(File.ReadAllText(@"..\..\..\Pico-rewrite.wrl"))));
        var scene = new VrmlScene();
        p.Parse(scene);

        using var sw = new StreamWriter(@"..\..\..\Pico.wrl");
        var wr = new VrmlWriter(sw) { Indent = 0 };
        wr.AssignNodeIds(scene.Root);
        DeletePicoWrlJunk(wr, scene.Root);
        UnifyShapes(wr, scene.Root);
        wr.WriteScene(scene);
        //printNodeLengths(wr, scene.Root, 0);
        //printLargeShapes(wr);
    }

    /// <summary>Delete specific nodes from Pico.wrl.</summary>
    private static bool DeletePicoWrlJunk(VrmlWriter wr, BaseNode node)
    {
        if (node == null)
            return false;

        if (node is ShapeNode shn)
        {
            if (shn.Geometry is IndexedLineSetNode)
                return true;
            var id = wr.NodeIds[node];
            return id == 101 || id == 137 || id == 150 || id == 213 || id == 216;
        }

        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                DeletePicoWrlJunk(wr, sfNode.Node);
            else if (field.Value is MFNode mfNode)
                mfNode._values.RemoveAll(n => DeletePicoWrlJunk(wr, n));
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

    /// <summary>
    ///     Within a single group, take all shapes that share the material and unify all the faces into a single shape. This
    ///     helps make the insane Sketchup export usable in Blender. Has a major flaw in that it ignores normals: all shapes
    ///     must be fixed in Blender with Normals / Recalculate Outside.</summary>
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
                    //if (Random.Shared.NextDouble() > 0.5)
                    //    (face[0], face[1]) = (face[1], face[0]);
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

    private static Dictionary<MaterialNode, AppearanceNode> _matApp = [];

    /// <summary>Apply a few tweaks to what Blender outputs.</summary>
    private static BaseNode Cleanup(VrmlWriter wr, BaseNode node)
    {
        if (node == null)
            return null;
        if (node is TransformNode tn)
        {
            // Blender sets unused rotations to 0,0,0,0. Fix them to 0,0,1,0 so that the writer knows that it's actually the default and skips it.
            if (tn.Rotation.Angle == 0)
            {
                tn.Rotation.X = tn.Rotation.Y = 0;
                tn.Rotation.Z = 1;
            }
        }
        else if (node is AppearanceNode an)
        {
            // ignoring texture, unify Appearances that use the same (by ref) Material
            if (_matApp.ContainsKey(an.Material))
                return _matApp[an.Material];
            _matApp.Add(an.Material, an);
        }
        else if (node is MaterialNode mn)
        {
            // reset some props to default - specific to the project this was coded for
            mn.AmbientIntensity = 0.2f;
            mn.Shininess = 0.2f;
            mn.SpecularColor.Red = mn.SpecularColor.Green = mn.SpecularColor.Blue = 0;
        }
        else if (node is IndexedFaceSetNode ifs)
        {
            // reset some props to default - specific to the project this was coded for
            ifs.Solid.Value = true;
        }

        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                sfNode.Node = Cleanup(wr, sfNode.Node);
            else if (field.Value is MFNode mfNode)
                for (int i = 0; i < mfNode.Length; i++)
                    mfNode[i] = Cleanup(wr, mfNode[i]);
        }

        // un-wrap groups that have a single child
        if (node is GroupNode gn && gn.Children.Length == 1)
            return gn.Children[0];
        // un-wrap single-child identity (non-)transforms
        else if (node is TransformNode tn2 && tn2.Children.Length == 1 && tn2.Scale.X == 1 && tn2.Scale.Y == 1 && tn2.Scale.Z == 1 && tn2.Rotation.Angle == 0 && tn2.Translation.X == 0 && tn2.Translation.Y == 0 && tn2.Translation.Z == 0)
            return tn2.Children[0];
        else
            return node;
    }

    /// <summary>
    ///     Find transforms that apply the same transformation (only within a single grouping node) and merge all children
    ///     into a single transform.</summary>
    private static void UnifyIdenticalTransforms(VrmlWriter wr, BaseNode node)
    {
        if (node == null)
            return;

        if (node is GroupingNode gn)
        {
            var transforms = gn.Children.OfType<TransformNode>().ToList();

            string getKey(TransformNode tn) => $"{tn.Center.X},{tn.Center.Y},{tn.Center.Z},{tn.Rotation.X},{tn.Rotation.Y},{tn.Rotation.Z},{tn.Rotation.Angle},{tn.Scale.X},{tn.Scale.Y},{tn.Scale.Z},{tn.ScaleOrientation.X},{tn.ScaleOrientation.Y},{tn.ScaleOrientation.Z},{tn.ScaleOrientation.Angle},{tn.Translation.X},{tn.Translation.Y},{tn.Translation.Z}";

            var grouped = transforms.GroupBy(getKey);
            foreach (var grp in grouped.Where(g => g.Count() > 1))
            {
                var main = grp.First();
                var rest = grp.Skip(1).ToHashSet();
                main.Children._values.AddRange(rest.SelectMany(tn => tn.Children));
                gn.Children._values.RemoveAll(tn => rest.Contains(tn));
            }
        }

        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                UnifyIdenticalTransforms(wr, sfNode.Node);
            else if (field.Value is MFNode mfNode)
                for (int i = 0; i < mfNode.Length; i++)
                    UnifyIdenticalTransforms(wr, mfNode[i]);
        }
    }
}
