using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
using Graph3D.Vrml.Nodes.Appearance;
using Graph3D.Vrml.Nodes.Appearance.Texture;
using Graph3D.Vrml.Nodes.Bindable;
using Graph3D.Vrml.Nodes.Geometry;
using Graph3D.Vrml.Nodes.Grouping;

namespace VrmlFixer;

public class VrmlWriter(StreamWriter wr)
{
    private int indent = 0;
    private void Wi() => wr.Write(new string(' ', indent));
    private void W(string s) => wr.Write(s);
    private void WL(string s) => wr.WriteLine(s);
    private void WiL(string s)
    {
        if (indent > 0)
            wr.Write(new string(' ', indent));
        wr.WriteLine(s);
    }

    #region OLD

    public void Write(VrmlScene scene)
    {
        WiL("#VRML V2.0 utf8");
        WriteSceneGraphNode(scene.Root);
    }

    public void WriteSceneGraphNode(SceneGraphNode node)
    {
        foreach (var child in node.Children)
            WriteNode(child);
    }

    public void WriteNode(BaseNode node)
    {
        switch (node)
        {
            case TransformNode transformNode: Write(transformNode); break;
            case ViewpointNode viewpointNode: Write(viewpointNode); break;
            case GroupNode groupNode: Write(groupNode); break;
            case ShapeNode shapeNode: Write(shapeNode); break;
            //case ShapeNode shapeNode: Write(shapeNode); break;
            //case TransformNode transformNode:
            //    Write(transformNode);
            //    break;
            //case AppearanceNode appearanceNode:
            //    Write(appearanceNode);
            //    break;
            //case MaterialNode materialNode:
            //    Write(materialNode);
            //    break;
            //case GeometryNode geometryNode:
            //    Write(geometryNode);
            //    break;
            //case IndexedFaceSetNode indexedFaceSetNode:
            //    Write(indexedFaceSetNode);
            //    break;
            //case CoordinateNode coordinateNode:
            //    Write(coordinateNode);
            //    break;
            //case NormalNode normalNode:
            //    Write(normalNode);
            //    break;
            //case TextureCoordinateNode textureCoordinateNode:
            //    Write(textureCoordinateNode);
            //    break;
            //case ImageTextureNode imageTextureNode:
            //    Write(imageTextureNode);
            //    break;
            //case TextureTransformNode textureTransformNode:
            //    Write(textureTransformNode);
            //    break;
            default:
                throw new NotImplementedException($"Node type {node.GetType()} not implemented");
        }
    }

    public void Write(TransformNode node)
    {
        WiL("Transform {");
        indent++;
        WiL($"translation {node.Translation.X} {node.Translation.Y} {node.Translation.Z}");
        WiL($"rotation {node.Rotation.X} {node.Rotation.Y} {node.Rotation.Z} {node.Rotation.Angle}");
        WiL($"scale {node.Scale.X} {node.Scale.Y} {node.Scale.Z}");
        WiL("children [");
        indent++;
        foreach (var child in node.Children)
            WriteNode(child);
        indent--;
        WiL("]");
        indent--;
        WiL("}");
    }

    public void Write(ViewpointNode node)
    {
        WiL("Viewpoint {");
        indent++;
        WiL($"position {node.Position.X} {node.Position.Y} {node.Position.Z}");
        WiL($"orientation {node.Orientation.X} {node.Orientation.Y} {node.Orientation.Z} {node.Orientation.Angle}");
        WiL($"fieldOfView {node.FieldOfView}");
        WiL($"description {node.Description}");
        indent--;
        WiL("}");
    }

    public void Write(GroupNode node)
    {
        if (string.IsNullOrEmpty(node.Name)) throw new Exception();
        WiL($"DEF {node.Name} Group {{");
        indent++;
        WiL("children [");
        indent++;
        foreach (var child in node.Children)
            WriteNode(child);
        indent--;
        WiL("]");
        indent--;
        WiL("}");
    }

    public void Write(ShapeNode node)
    {
        WiL("Shape {");
        indent++;
        Write(node.Appearance);
        WriteGeometry(node.Geometry);
        indent--;
        WiL("}");
    }

    public void Write(AppearanceNode node)
    {
        WiL("appearance Appearance {");
        indent++;
        Write(node.Material);
        if (node.Texture != null)
            Write(node.Texture);
        indent--;
        WiL("}");
    }

    public void Write(MaterialNode node)
    {
        WiL("material Material {");
        indent++;
        WiL($"ambientIntensity {node.AmbientIntensity}");
        WiL($"diffuseColor {node.DiffuseColor.Red} {node.DiffuseColor.Green} {node.DiffuseColor.Blue}");
        WiL($"emissiveColor {node.EmissiveColor.Red} {node.EmissiveColor.Green} {node.EmissiveColor.Blue}");
        WiL($"shininess {node.Shininess}");
        WiL($"specularColor {node.SpecularColor.Red} {node.SpecularColor.Green} {node.SpecularColor.Blue}");
        indent--;
        WiL("}");
    }

    public void Write(TextureNode node) => throw new NotImplementedException();

    public void WriteGeometry(GeometryNode node)
    {
        switch (node)
        {
            case IndexedFaceSetNode indexedFaceSetNode: Write(indexedFaceSetNode); break;
            case IndexedLineSetNode indexedLineSetNode: Write(indexedLineSetNode); break;
            default: throw new NotImplementedException();
        }
    }

    public void Write(IndexedFaceSetNode node)
    {
        WiL("geometry IndexedFaceSet {");
        indent++;
        if (node.Convex != null)
            WiL($"convex {node.Convex.Value.ToString().ToUpper()}");
        if (node.Solid != null)
            WiL($"solid {node.Solid.Value.ToString().ToUpper()}");
        //Write(node.Coord);
        //Write(node.Normal);
        //Write(node.TexCoord);
        //WL("coordIndex [");
        //indent++;
        //foreach (var index in node.CoordIndex)
        //    WL(index.ToString());
        //indent--;
        //WL("]");
        //if (node.NormalIndex != null)
        //{
        //    WL("normalIndex [");
        //    indent++;
        //    foreach (var index in node.NormalIndex)
        //        WL(index.ToString());
        //    indent--;
        //    WL("]");
        //}
        //if (node.TextureCoordinateIndex != null)
        //{
        //    WL("texCoordIndex [");
        //    indent++;
        //    foreach (var index in node.TextureCoordinateIndex)
        //        WL(index.ToString());
        //    indent--;
        //    WL("]");
        //}
        indent--;
        WiL("}");
    }

    public void Write(IndexedLineSetNode node)
    {
        WiL("geometry IndexedLineSet {");
        indent++;
        //if (node.Color != null)
        //    Write(node.Color);
        //if (node.Coord != null)
        //    Write(node.Coord);
        //if (node.ColorIndex != null)
        //{
        //    WL("colorIndex [");
        //    indent++;
        //    foreach (var index in node.ColorIndex)
        //        WL(index.ToString());
        //    indent--;
        //    WL("]");
        //}
        //if (node.CoordIndex != null)
        //{
        //    WL("coordIndex [");
        //    indent++;
        //    foreach (var index in node.CoordIndex)
        //        WL(index.ToString());
        //    indent--;
        //    WL("]");
        //}
        indent--;
        WiL("}");
    }

    #endregion

    private Dictionary<BaseNode, int> _refs = new();
    private Dictionary<BaseNode, string> _refNames = new();
    private HashSet<BaseNode> _refsWritten = new();

    public void DiscoverRefs(BaseNode node)
    {
        if (node == null)
            return;
        if (_refs.ContainsKey(node))
        {
            _refs[node]++;
            return;
        }
        _refs.Add(node, 1);
        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                DiscoverRefs(sfNode.Node);
            else if (field.Value is MFNode mfNode)
                foreach (var n in mfNode)
                    DiscoverRefs(n);
        }
    }

    public void GraphWriteNode(BaseNode node)
    {
        // write DEF/USE
        if (_refNames.ContainsKey(node))
        {
            if (_refsWritten.Add(node))
                W($"DEF {_refNames[node]} ");
            else
            {
                WL($"USE {_refNames[node]}");
                return;
            }
        }
        // write node type name
        switch (node)
        {
            case SceneGraphNode: break;
            case TransformNode: WL("Transform {"); break;
            case ViewpointNode: WL("Viewpoint {"); break;
            case GroupNode: WL("Group {"); break;
            case ShapeNode: WL("Shape {"); break;
            case AppearanceNode: WL("Appearance {"); break;
            case MaterialNode: WL("Material {"); break;
            case IndexedFaceSetNode: WL("IndexedFaceSet {"); break;
            case IndexedLineSetNode: WL("IndexedLineSet {"); break;
            case CoordinateNode: WL("Coordinate {"); break;
            default: throw new NotImplementedException();
        }
        // write fields
        indent++;
        foreach (var field in node.AllFields.OrderBy(f => f.Value is MField ? 2 : f.Value is SFNode ? 1 : 0).ThenBy(f => f.Key))
        {
            if (!node.ExposedFieldNames.Contains(field.Key))
                continue;
            switch (field.Value)
            {
                case SFBool sfBool: WiL($"{field.Key} {sfBool.Value.ToString().ToUpper()}"); break;
                case SFInt32 sfInt32: WiL($"{field.Key} {sfInt32.Value}"); break;
                case SFFloat sfFloat: WiL($"{field.Key} {sfFloat.Value}"); break;
                case SFString sfString: WiL($"{field.Key} {sfString.Value}"); break;
                case SFColor sfColor: WiL($"{field.Key} {sfColor.Red} {sfColor.Green} {sfColor.Blue}"); break;
                case SFVec3f sfVec3f: WiL($"{field.Key} {sfVec3f.X} {sfVec3f.Y} {sfVec3f.Z}"); break;
                case SFRotation sfRotation: WiL($"{field.Key} {sfRotation.X} {sfRotation.Y} {sfRotation.Z} {sfRotation.Angle}"); break;
                case MFInt32 mfInt32:
                    WiL($"{field.Key} [");
                    indent++;
                    foreach (var i in mfInt32)
                        WiL(i.Value.ToString());
                    indent--;
                    WiL("]");
                    break;
                case MFVec3f mfVec3f:
                    WiL($"{field.Key} [");
                    indent++;
                    foreach (var vec in mfVec3f)
                        WiL($"{vec.X} {vec.Y} {vec.Z}");
                    indent--;
                    WiL("]");
                    break;
                case SFNode sfNode:
                    if (sfNode.Node == null)
                        break;
                    Wi();
                    W($"{field.Key} ");
                    GraphWriteNode(sfNode.Node);
                    break;
                case MFNode mfNode:
                    WiL($"{field.Key} [");
                    indent++;
                    foreach (var n in mfNode)
                    {
                        Wi();
                        GraphWriteNode(n);
                    }
                    indent--;
                    WiL("]");
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        indent--;
        WiL("}");
    }

    public void GraphWriteScene(VrmlScene scene)
    {
        DiscoverRefs(scene.Root);
        _refNames = new();
        foreach (var kvp in _refs)
            if (kvp.Value > 1)
                _refNames.Add(kvp.Key, $"ref_{Random.Shared.NextDouble().ToString().Replace("0.", "")}");
        _refsWritten = new();
        WiL("#VRML V2.0 utf8");
        foreach (var rootchild in scene.Root.Children)
            GraphWriteNode(rootchild);
    }
}
