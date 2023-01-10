// FIX: need a way to determine if two strings point to the same resource...it does handle interspersed ".." and "."s now, but not case differences
// TODO: the variable naming in the resource statement handling is pretty gross
// TODO: passing the config statement to AddResource() just so I can pass it along to ListError() is gross

namespace UberASMTool;

public class UberConfig
{
    public string ROMFile { get; private set; } = null;
    UberContext levelContext = new UberContext(UberContextType.Level, 512);
    UberContext gamemodeContext = new UberContext(UberContextType.Gamemode, 256);
    UberContext overworldContext = new UberContext(UberContextType.Overworld, 7);

    public bool ProcessList(IEnumerable<ConfigStatement> statements, ResourceHandler handler, ROM rom)
    {
        bool globalFile = false;
        bool statusbarFile = false;
        bool macrolibFile = false;
        bool freeRAM = false;
        var currentMode = UberContextType.None;
        bool valid = true;

        foreach (ConfigStatement statement in statements)
        {
            switch (statement)
            {
                case VerboseStatement s:
                    MessageWriter.VerboseMode = s.IsOn;
                    break;

                case ModeStatement s:
                    currentMode = s.Mode;
                    break;

                case GlobalFileStatement s :
                    valid &= s.Process(ref globalFile, rom);
                    break;

                case StatusbarFileStatement s :
                    valid &= s.Process(ref statusbarFile, rom);
                    break;

                case MacrolibFileStatement s :
                    valid &= s.Process(ref macrolibFile, rom);
                    break;

                case ROMStatement s :
                    string file = ROMFile;          // because you can't pass a property as a ref param...about as equally gross as giving it an explicit backing varaible
                    valid &= s.Process(ref file);
                    ROMFile = file;
                    break;

                case FreeramStatement s:
                    valid &= s.Process(ref freeRAM, rom);
                    break;

                case ResourceStatement s:
                    if (currentMode == UberContextType.None)
                    {
                        s.Error("Unspecified resource type (level/overworld/gamemode).");
                        valid = false;
                        break;
                    }

                    UberContext context = currentMode switch
                    {
                        UberContextType.Level => levelContext,
                        UberContextType.Gamemode => gamemodeContext,
                        UberContextType.Overworld => overworldContext,
                        _ => throw new ArgumentException()
                    };

                    valid &= s.Process(context, handler, rom);
                    break;
            }
        }

        valid &= CheckCommandSpecified(globalFile, "global:");
        valid &= CheckCommandSpecified(statusbarFile, "statusbar:");
        valid &= CheckCommandSpecified(macrolibFile, "macrolib:");
        valid &= CheckCommandSpecified(freeRAM, "freeram:");

        return valid;
    }


    private bool CheckCommandSpecified(bool val, string cmd)
    {
        if (!val)
            MessageWriter.Write(true, $"Error: no \"{cmd}\" command found.");
        return val;
    }

    public void AddNMIDefines(ROM rom)
    {
        bool any = levelContext.AddNMIDefines(rom) |
                   gamemodeContext.AddNMIDefines(rom) |
                   overworldContext.AddNMIDefines(rom);
        rom.AddDefine("UberUseNMI", any ? "1" : "0");
        return;
    }

    public bool GenerateExtraBytesFile(Resource resource)
    {
        var output = new StringBuilder();

        levelContext.GenerateExtraBytes(output, resource);
        gamemodeContext.GenerateExtraBytes(output, resource);
        overworldContext.GenerateExtraBytes(output, resource);

        return Program.TryWriteFile("asm/work/extra_bytes.asm", output.ToString());
    }

    public bool GenerateCallFile()
    {
        var output = new StringBuilder();

        levelContext.GenerateCalls(output);
        gamemodeContext.GenerateCalls(output);
        overworldContext.GenerateCalls(output);

        return Program.TryWriteFile("asm/work/resource_calls.asm", output.ToString());
    }
}
