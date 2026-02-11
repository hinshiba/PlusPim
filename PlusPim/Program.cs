using PlusPim.Debuggers.PlusPimDbg;
using PlusPim.EditorController.DebugAdapter;

namespace PlusPim;

internal class Program {
    private static void Main(string[] args) {
        // 各種クラスを呼び出す
        PlusPimDbg plusPimDbg = new();
        Application.Application app = new(plusPimDbg);
        _ = new DebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), app);

    }
}
