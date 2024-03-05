using Graph3D.Vrml;
using Graph3D.Vrml.Fields;
using Graph3D.Vrml.Nodes;
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
            wr.WriteScene(scene);
            printNodeLengths(wr, scene.Root, 0);
        }
    }

    private static void printNodeLengths(VrmlWriter wr, BaseNode node, int depth)
    {
        if (node == null)
            return;
        if (depth > 0)
        {
            if (!wr.NodeLengths.ContainsKey(node) || wr.NodeLengths[node] < 5000)
                return;
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
}
