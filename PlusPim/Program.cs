using PlusPim.EditorController.DebugAdapter;

namespace PlusPim;

internal class Program {
    private static void Main(string[] args) {
        Application.Application app = new();
        _ = new DebugAdapter(Console.OpenStandardInput(), Console.OpenStandardOutput(), app);
    }
}
