using System.Xml.Linq;
using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
using Graph3D.Vrml.Nodes.Appearance;
using Graph3D.Vrml.Nodes.Appearance.Texture;
using Graph3D.Vrml.Nodes.Bindable;
using Graph3D.Vrml.Nodes.Geometry;
using Graph3D.Vrml.Nodes.Grouping;
using Graph3D.Vrml.Nodes.Interpolation;
using Graph3D.Vrml.Nodes.LightSources;
using Graph3D.Vrml.Nodes.Sensors;

// this reader is quite specific to the one file this was written for and is very far from a complete reader:
// - it skips some types of nodes instead of reading them (see "SKIP")
// - it makes many assumptions about exactly what is present and what isn't
// - it doesn't bother reading anything relating to textures
// - in fact it only supports a very small and specific number of "attributes" that can be read if encoded as XML children (eg Appearance/Material and IFS/Coordinate)

namespace VrmlFixer;

public class X3dReader
{
    public Dictionary<string, BaseNode> Defs = [];

    public VrmlScene Read(XElement file)
    {
        return new VrmlScene
        {
            Root = (SceneGraphNode)ReadNode(file.Element("Scene"))
        };
    }

    public BaseNode ReadNode(XElement xml)
    {
        if (xml.Attribute("USE") != null)
            return Defs[xml.Attribute("USE").Value];
        var node = NameToNode(xml);
        if (xml.Attribute("DEF") != null)
            Defs.Add(xml.Attribute("DEF").Value, node);

        // attributes
        foreach (var attr in xml.Attributes())
        {
            if (!node.AllFields.ContainsKey(attr.Name.LocalName))
            {
                if (attr.Name.LocalName != "DEF")
                    Console.WriteLine($"Ignoring attribute {attr.Name.LocalName} on node {xml.Name.LocalName}");
                continue;
            }
            var field = node.AllFields[attr.Name.LocalName];
            if (field is SFBool sfBool)
                sfBool.Value = bool.Parse(attr.Value);
            else if (field is SFFloat sfFloat)
                sfFloat.Value = float.Parse(attr.Value);
            else if (field is MFInt32 mfInt32)
            {
                mfInt32.ClearValues();
                foreach (var i in attr.Value.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).Select(int.Parse))
                    mfInt32.AppendValue(new SFInt32(i));
            }
            else if (field is MFString mfString)
            {
                mfString.ClearValues();
                foreach (var s in attr.Value.Split(',').Select(s => s.Trim().Trim('"')))
                    mfString.AppendValue(new SFString(s));
            }
            else if (field is SFColor sfColor)
            {
                var p = attr.Value.SplitParseFloats().SelectSFColors().Single();
                sfColor.Red = p.Red;
                sfColor.Green = p.Green;
                sfColor.Blue = p.Blue;
            }
            else if (field is MFColor mfColor)
            {
                mfColor.ClearValues();
                foreach (var c in attr.Value.SplitParseFloats().SelectSFColors())
                    mfColor.AppendValue(c);
            }
            else if (field is SFVec3f vec3f)
            {
                var p = attr.Value.SplitParseFloats().SelectSFVec3fs().Single();
                vec3f.X = p.X;
                vec3f.Y = p.Y;
                vec3f.Z = p.Z;
            }
            else if (field is MFVec3f mfVec3f)
            {
                mfVec3f.ClearValues();
                foreach (var v in attr.Value.SplitParseFloats().SelectSFVec3fs())
                    mfVec3f.AppendValue(v);
            }
            else if (field is SFRotation rot)
            {
                var p = attr.Value.SplitParseFloats().ToList();
                if (p.Count != 4) throw new Exception();
                rot.X = p[0];
                rot.Y = p[1];
                rot.Z = p[2];
                rot.Angle = p[3];
            }
            else
                throw new Exception();
        }

        // children
        if (xml.HasElements)
        {
            if (node is GroupingNode gn)
            {
                foreach (var child in xml.Elements())
                {
                    var childNode = ReadNode(child);
                    if (childNode is NavigationInfoNode || childNode is BackgroundNode || childNode is DirectionalLightNode)
                        continue; // SKIP
                    gn.AppendChild(childNode);
                }
            }
            else if (node is ShapeNode sn)
            {
                sn.Appearance = (AppearanceNode)ReadNode(xml.Element("Appearance"));
                sn.Geometry = (IndexedFaceSetNode)ReadNode(xml.Element("IndexedFaceSet"));
            }
            else if (node is AppearanceNode an)
            {
                an.Material = (MaterialNode)ReadNode(xml.Element("Material"));
            }
            else if (node is IndexedFaceSetNode ifsn)
            {
                ifsn.Coord.Node = (CoordinateNode)ReadNode(xml.Element("Coordinate"));
            }
            else
                throw new Exception();
        }
        return node;
    }

    public static BaseNode NameToNode(XElement xml)
    {
        return xml.Name.LocalName switch
        {
            "Scene" => new SceneGraphNode(),
            "Transform" => new TransformNode(),
            "Shape" => new ShapeNode(),
            "Appearance" => new AppearanceNode(),
            "Material" => new MaterialNode(),
            "IndexedFaceSet" => new IndexedFaceSetNode(),
            "Coordinate" => new CoordinateNode(),
            "TextureCoordinate" => new TextureCoordinateNode(),
            "ImageTexture" => new ImageTextureNode(),
            "Group" => new GroupNode(),
            "Viewpoint" => new ViewpointNode(),
            "Background" => new BackgroundNode(),
            "PointLight" => new PointLightNode(),
            "DirectionalLight" => new DirectionalLightNode(),
            "Cylinder" => new CylinderNode(),
            "Cone" => new ConeNode(),
            "Box" => new BoxNode(),
            "Sphere" => new SphereNode(),
            "Anchor" => new AnchorNode(),
            "Collision" => new CollisionNode(),
            "Switch" => new SwitchNode(),
            "CoordinateInterpolator" => new CoordinateInterpolatorNode(),
            "OrientationInterpolator" => new OrientationInterpolatorNode(),
            "PositionInterpolator" => new PositionInterpolatorNode(),
            "Script" => new ScriptNode(),
            "NavigationInfo" => new NavigationInfoNode(),
            "TimeSensor" => new TimeSensorNode(),
            "WorldInfo" => new WorldInfoNode(),
            "Color" => new ColorNode(),
            "Normal" => new NormalNode(),
            "TextureTransform" => new TextureTransformNode(),
            _ => throw new NotImplementedException($"Unknown node type {xml.Name.LocalName}")
        };
    }
}

public static class Exts
{
    public static IEnumerable<float> SplitParseFloats(this string s)
    {
        return s.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).Select(float.Parse);
    }

    public static IEnumerable<SFColor> SelectSFColors(this IEnumerable<float> floats)
    {
        var cur = new List<float>();
        foreach (var f in floats)
        {
            cur.Add(f);
            if (cur.Count == 3)
            {
                yield return new SFColor(cur[0], cur[1], cur[2]);
                cur.Clear();
            }
        }
        if (cur.Count > 0)
            throw new Exception();
    }

    public static IEnumerable<SFVec3f> SelectSFVec3fs(this IEnumerable<float> floats)
    {
        var cur = new List<float>();
        foreach (var f in floats)
        {
            cur.Add(f);
            if (cur.Count == 3)
            {
                yield return new SFVec3f(cur[0], cur[1], cur[2]);
                cur.Clear();
            }
        }
        if (cur.Count > 0)
            throw new Exception();
    }
}
