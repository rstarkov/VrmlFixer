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
    public int Indent { get; set; } = 2;

    private int indent = 0;
    private void Wi() => wr.Write(new string(' ', indent * Indent));
    private void W(string s) => wr.Write(s);
    private void WL(string s) => wr.WriteLine(s);
    private void WiL(string s)
    {
        if (indent > 0)
            wr.Write(new string(' ', indent * Indent));
        wr.WriteLine(s);
    }

    private Dictionary<BaseNode, int> _refs;
    private Dictionary<BaseNode, string> _refNames;
    private HashSet<BaseNode> _refsWritten;
    public Dictionary<BaseNode, int> NodeIds = [];
    public Dictionary<BaseNode, int> NodeLengths;

    public void AssignNodeIds(BaseNode node)
    {
        if (node == null)
            return;
        if (NodeIds.ContainsKey(node))
            return;
        NodeIds.Add(node, NodeIds.Count);
        foreach (var field in node.AllFields)
        {
            if (field.Value is SFNode sfNode)
                AssignNodeIds(sfNode.Node);
            else if (field.Value is MFNode mfNode)
                foreach (var n in mfNode)
                    AssignNodeIds(n);
        }
    }

    public void WriteScene(VrmlScene scene)
    {
        _refs = [];
        DiscoverRefs(scene.Root);
        _refNames = [];
        foreach (var kvp in _refs)
            if (kvp.Value > 1)
                _refNames.Add(kvp.Key, $"R{_refNames.Count}");

        _refsWritten = [];
        NodeLengths = [];
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
        var start = wr.BaseStream.Position;
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
        // get default fields
        var defaultNode = (BaseNode)node.GetType().GetConstructor([]).Invoke([]);
        // write fields
        indent++;
        foreach (var field in node.AllFields.OrderBy(f => f.Value is MField ? 2 : f.Value is SFNode ? 1 : 0).ThenBy(f => f.Key))
        {
            var defaultField = defaultNode.GetField(field.Key);
            switch (field.Value)
            {
                case SFBool bl:
                    if (bl.Value != ((SFBool)defaultField).Value)
                        WiL($"{field.Key} {bl.Value.ToString().ToUpper()}");
                    break;
                case SFInt32 int32:
                    if (int32.Value != ((SFInt32)defaultField).Value)
                        WiL($"{field.Key} {int32.Value}");
                    break;
                case SFString sfString:
                    if (sfString.Value != ((SFString)defaultField).Value)
                        WiL($"{field.Key} {sfString.Value}");
                    break;
                case SFFloat flt:
                    if (flt.Value != ((SFFloat)defaultField).Value)
                        WiL($"{field.Key} {flt.Value}");
                    break;
                case SFColor clr:
                    if (clr.Red != ((SFColor)defaultField).Red || clr.Green != ((SFColor)defaultField).Green || clr.Blue != ((SFColor)defaultField).Blue)
                        WiL($"{field.Key} {clr.Red} {clr.Green} {clr.Blue}");
                    break;
                case SFVec3f vec3f:
                    if (vec3f.X != ((SFVec3f)defaultField).X || vec3f.Y != ((SFVec3f)defaultField).Y || vec3f.Z != ((SFVec3f)defaultField).Z)
                        WiL($"{field.Key} {vec3f.X} {vec3f.Y} {vec3f.Z}");
                    break;
                case SFRotation rot:
                    if (rot.X != ((SFRotation)defaultField).X || rot.Y != ((SFRotation)defaultField).Y || rot.Z != ((SFRotation)defaultField).Z || rot.Angle != ((SFRotation)defaultField).Angle)
                        WiL($"{field.Key} {rot.X} {rot.Y} {rot.Z} {rot.Angle}");
                    break;
                case MFInt32 mfInt32:
                    if (mfInt32.Length == 0 && ((MFInt32)defaultField).Length == 0)
                        break;
                    WiL($"{field.Key} [");
                    indent++;
                    foreach (var i in mfInt32)
                        WiL(i.Value.ToString());
                    indent--;
                    WiL("]");
                    break;
                case MFVec3f mfVec3f:
                    if (mfVec3f.Length == 0 && ((MFInt32)defaultField).Length == 0)
                        break;
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
                    if (mfNode.Length == 0 && ((MFInt32)defaultField).Length == 0)
                        break;
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

        wr.Flush();
        NodeLengths.Add(node, (int)(wr.BaseStream.Position - start));
    }
}
