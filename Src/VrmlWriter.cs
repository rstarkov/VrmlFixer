using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
using Graph3D.Vrml.Nodes.Appearance;
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

    private Dictionary<BaseNode, int> _refs;
    private Dictionary<BaseNode, string> _refNames;
    private HashSet<BaseNode> _refsWritten;

    public void WriteScene(VrmlScene scene)
    {
        _refs = [];
        DiscoverRefs(scene.Root);
        _refNames = [];
        foreach (var kvp in _refs)
            if (kvp.Value > 1)
                _refNames.Add(kvp.Key, $"R{_refNames.Count}");

        _refsWritten = [];
        WiL("#VRML V2.0 utf8");
        foreach (var rootchild in scene.Root.Children)
            GraphWriteNode(rootchild);
    }

    private void DiscoverRefs(BaseNode node)
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

    private void GraphWriteNode(BaseNode node)
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
}
