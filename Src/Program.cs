using Graph3D.Vrml;
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
            wr.WriteScene(scene);
        }
    }
}
