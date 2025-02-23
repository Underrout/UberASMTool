namespace UberASMTool;

public enum UberContextType { None, Level, Gamemode, Overworld }

// collects all the members of a context together
public class UberContext
{
    private ContextMember all;
    private ContextMember[] singles;
    public string Name { get; init; }
    public string Directory => Name.ToLower();
    public int Size { get; init; }

    public static string TypeToName(UberContextType contextType) => contextType switch
    {
        UberContextType.Level => "Level",
        UberContextType.Gamemode => "Gamemode",
        UberContextType.Overworld => "Overworld",
        _ => throw new ArgumentException()
    };

// contextName = upper case: "Level", etc, used for labels
    public UberContext(UberContextType type, int max)
    {
        Name = TypeToName(type);
        Size = max;
        all = new ContextMember();
        singles = new ContextMember[max];
        for (int i = 0; i < max; i++)
            singles[i] = new ContextMember();
    }

    public ContextMember GetMember(int num)
    {
        if (num == -1)
            return all;
        else
            return singles[num];
    }
    
    public void GenerateExtraBytes(StringBuilder output, Resource resource)
    {
        all.GenerateExtraBytes(output, resource, $"{Name}All");
        for (int i = 0; i < singles.Length; i++)
            singles[i].GenerateExtraBytes(output, resource, $"{Name}{i:X}");
    }

    // returns the value of the general NMI define for this context, NOT success/failure
    public bool AddNMIDefines(ROM rom)
    {
        bool allNMI = all.HasNMI;

        bool normalNMI = false;
        foreach (ContextMember single in singles)
            if (single.HasNMI)
            {
                normalNMI = true;
                break;
            }

        bool overallNMI = allNMI || normalNMI;

        rom.AddDefine($"Uber{Name}NMIAll", allNMI ? "1" : "0");
        rom.AddDefine($"Uber{Name}NMINormal", normalNMI ? "1" : "0");
        rom.AddDefine($"Uber{Name}NMI", overallNMI ? "1" : "0");

        return overallNMI;
    }

// could skip if there are no NMIs, but it's just the labels (and a possibly empty macro), so it doesn't really matter
    public void GenerateCalls(StringBuilder output)
    {
        GenerateUnusedLabels(output);
        output.AppendLine("    rts").AppendLine();

        GenerateUsedLabels(output, false);
        GenerateUsedLabels(output, true);

        GenerateAllMacro(output, false);
        GenerateAllMacro(output, true);
    }

    private void GenerateUnusedLabels(StringBuilder output)
    {
        for (int i = 0; i < singles.Length; i++)
        {
            if (singles[i].Empty)
                output.AppendLine($"{Name}{i:X}JSLs:");
            if (!singles[i].HasNMI)
                output.AppendLine($"{Name}{i:X}NMIJSLs:");
        }
    }

    private void GenerateUsedLabels(StringBuilder output, bool nmi)
    {
        for (int i = 0; i < singles.Length; i++)
        {
            bool used = nmi ? singles[i].HasNMI : !singles[i].Empty;

            if (!used)
                continue;
            output.AppendLine($"{Name}{i:X}{(nmi ? "NMI" : "")}JSLs:");
            singles[i].GenerateCalls(output, nmi, Name, $"{i:X}");
            output.AppendLine("    rts").AppendLine();
        }
    }

    // Note: the resource entry point expects the label offset value at $06,S, so we need to push 2 dummy bytes
    // onto the stack here because this is called inline, rather than being JSRed to
    // PEA is 5 cycles vs. 6 for PHA : PHA
    private void GenerateAllMacro(StringBuilder output, bool nmi)
    {
        output.AppendLine($"macro {Name}All{(nmi ? "NMI" : "")}JSLs()");
        if (!all.Empty)
        {
            if (nmi)
                all.GenerateCalls(output, nmi, Name, "All");
            else
            {
                output.AppendLine("    pea $0000");
                all.GenerateCalls(output, nmi, Name, "All");
                output.AppendLine("    pla : pla");
            }
        }
        output.AppendLine("endmacro").AppendLine();
    }

}
