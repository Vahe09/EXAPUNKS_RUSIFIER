using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private const string ShimTypeName = "RuTranslationShim";
    private const string ShimMethodName = "Apply";
    private static readonly KeyValuePair<string, string>[] Translations =
    {
        new KeyValuePair<string, string>("AXIOM PERSONAL ORGANIZER", "\u041B\u0418\u0427\u041D\u042B\u0419 \u041E\u0420\u0413\u0410\u041D\u0410\u0419\u0417\u0415\u0420 AXIOM"),
        new KeyValuePair<string, string>("MEETING WITH NIVAS", "\u0412\u0421\u0422\u0420\u0415\u0427\u0410 \u0421 NIVAS"),
        new KeyValuePair<string, string>("MEETING WITH GHAST", "\u0412\u0421\u0422\u0420\u0415\u0427\u0410 \u0421 GHAST"),
        new KeyValuePair<string, string>("WORKHOUSE", "WORKHOUSE"),
        new KeyValuePair<string, string>("INSTALLED PROGRAM", "\u0423\u0421\u0422\u0410\u041D. \u041F\u0420\u041E\u0413\u0420\u0410\u041C\u041C\u0410"),
        new KeyValuePair<string, string>("LAUNCH WORKHOUSE", "\u0417\u0410\u041F\u0423\u0421\u041A WORKHOUSE"),
        new KeyValuePair<string, string>("Perform simple tasks to earn money at home.", "\u0412\u044B\u043F\u043E\u043B\u043D\u044F\u0439\u0442\u0435 \u043F\u0440\u043E\u0441\u0442\u044B\u0435 \u0437\u0430\u0434\u0430\u043D\u0438\u044F \u0434\u043E\u043C\u0430 \u0437\u0430 \u0434\u0435\u043D\u044C\u0433\u0438."),
        new KeyValuePair<string, string>("Find a source for bootleg medication.", "\u041D\u0430\u0439\u0434\u0438\u0442\u0435 \u0438\u0441\u0442\u043E\u0447\u043D\u0438\u043A \u043B\u0435\u0432\u044B\u0445 \u043B\u0435\u043A\u0430\u0440\u0441\u0442\u0432."),
        new KeyValuePair<string, string>("SAT 10/04/97 5:21 PM", "\u0421\u0411 04.10.97 17:21"),
        new KeyValuePair<string, string>("SAT 10/04/97 5:35 PM", "\u0421\u0411 04.10.97 17:35"),
        new KeyValuePair<string, string>("TRASH WORLD NEWS", "\u041D\u041E\u0412\u041E\u0421\u0422\u0418 TRASH"),
        new KeyValuePair<string, string>("TUTORIAL 1", "\u0423\u0420\u041E\u041A 1"),
        new KeyValuePair<string, string>("Learn to explore networks and leave no trace.", "\u0418\u0437\u0443\u0447\u0438\u0442\u0435 \u0441\u0435\u0442\u0438 \u0438 \u043D\u0435 \u043E\u0441\u0442\u0430\u0432\u043B\u044F\u0439\u0442\u0435 \u0441\u043B\u0435\u0434\u043E\u0432."),
        new KeyValuePair<string, string>("CONNECT TO NETWORK", "\u041F\u041E\u0414\u041A\u041B\u042E\u0427\u0418\u0422\u042C\u0421\u042F \u041A \u0421\u0415\u0422\u0418"),
        new KeyValuePair<string, string>("Solve this puzzle to view histograms.", "\u0420\u0435\u0448\u0438\u0442\u0435 \u044D\u0442\u0443 \u0437\u0430\u0434\u0430\u0447\u0443, \u0447\u0442\u043E\u0431\u044B \u0443\u0432\u0438\u0434\u0435\u0442\u044C \u0433\u0438\u0441\u0442\u043E\u0433\u0440\u0430\u043C\u043C\u044B."),
        new KeyValuePair<string, string>("PLAY CUTSCENE", "\u0421\u041C\u041E\u0422\u0420\u0415\u0422\u042C \u0421\u0426\u0415\u041D\u0423"),
        new KeyValuePair<string, string>("Reconnect with an old friend.", "\u0412\u043D\u043E\u0432\u044C \u0441\u0432\u044F\u0436\u0438\u0441\u044C \u0441\u043E \u0441\u0442\u0430\u0440\u044B\u043C \u0434\u0440\u0443\u0433\u043E\u043C."),
        new KeyValuePair<string, string>("605 Eddy St. #801, San Francisco, CA", "605 Eddy St. #801, \u0421\u0430\u043D-\u0424\u0440\u0430\u043D\u0446\u0438\u0441\u043A\u043E, CA"),
        new KeyValuePair<string, string>("INTRO", "\u0412\u0421\u0422\u0423\u041F\u041B\u0415\u041D\u0418\u0415"),
        new KeyValuePair<string, string>("OUTRO", "\u0424\u0418\u041D\u0410\u041B"),
        new KeyValuePair<string, string>("CYCLES", "\u0426\u0418\u041A\u041B\u042B"),
        new KeyValuePair<string, string>("SIZE", "\u0420\u0410\u0417\u041C\u0415\u0420"),
        new KeyValuePair<string, string>("ACTIVITY", "\u0410\u041A\u0422\u0418\u0412\u041D\u041E\u0421\u0422\u042C"),
        new KeyValuePair<string, string>("Emulated", "\u042D\u043C\u0443\u043B\u044F\u0446\u0438\u044F"),
        new KeyValuePair<string, string>("Internal", "\u0412\u043D\u0443\u0442\u0440\u0435\u043D\u043D\u0435\u0435"),
        new KeyValuePair<string, string>("Emotional", "\u042D\u043C\u043E\u0446\u0438\u0438"),
        new KeyValuePair<string, string>("Reasoning", "\u041B\u043E\u0433\u0438\u043A\u0430"),
        new KeyValuePair<string, string>("OPTIONS", "\u041D\u0410\u0421\u0422\u0420\u041E\u0419\u041A\u0418"),
        new KeyValuePair<string, string>("CONTROLS", "\u0423\u041F\u0420\u0410\u0412\u041B\u0415\u041D\u0418\u0415"),
        new KeyValuePair<string, string>("EXIT GAME", "\u0412\u042B\u0425\u041E\u0414 \u0418\u0417 \u0418\u0413\u0420\u042B"),
        new KeyValuePair<string, string>("BACK", "\u041D\u0410\u0417\u0410\u0414"),
        new KeyValuePair<string, string>("DISPLAY", "\u042D\u041A\u0420\u0410\u041D"),
        new KeyValuePair<string, string>("SOUND", "\u0417\u0412\u0423\u041A"),
        new KeyValuePair<string, string>("INTERFACE", "\u0418\u041D\u0422\u0415\u0420\u0424\u0415\u0419\u0421"),
        new KeyValuePair<string, string>("DISPLAY MODE", "\u0420\u0415\u0416\u0418\u041C \u042D\u041A\u0420\u0410\u041D\u0410"),
        new KeyValuePair<string, string>("FULLSCREEN", "\u041F\u041E\u041B\u041D\u042B\u0419 \u042D\u041A\u0420\u0410\u041D"),
        new KeyValuePair<string, string>("WINDOWED", "\u041E\u041A\u041E\u041D\u041D\u042B\u0419"),
        new KeyValuePair<string, string>("WINDOW SIZE", "\u0420\u0410\u0417\u041C\u0415\u0420 \u041E\u041A\u041D\u0410"),
        new KeyValuePair<string, string>("DISPLAY QUALITY", "\u041A\u0410\u0427\u0415\u0421\u0422\u0412\u041E \u042D\u041A\u0420\u0410\u041D\u0410"),
        new KeyValuePair<string, string>("HIGH (4K)", "\u0412\u042B\u0421\u041E\u041A\u041E\u0415 (4\u041A)"),
        new KeyValuePair<string, string>("LOW (2K)", "\u041D\u0418\u0417\u041A\u041E\u0415 (2\u041A)"),
        new KeyValuePair<string, string>("OTHER CONTROLS", "\u041F\u0420\u041E\u0427\u0415\u0415 \u0423\u041F\u0420\u0410\u0412\u041B\u0415\u041D\u0418\u0415"),
        new KeyValuePair<string, string>("RETURN", "\u0412\u0412\u041E\u0414"),
        new KeyValuePair<string, string>("START", "\u0421\u0422\u0410\u0420\u0422"),
        new KeyValuePair<string, string>("RESET / PAUSE / STEP / RUN / FAST", "\u0421\u0411\u0420\u041E\u0421 / \u041F\u0410\u0423\u0417\u0410 / \u0428\u0410\u0413 / \u041F\u0423\u0421\u041A / \u0411\u042B\u0421\u0422\u0420\u041E"),
        new KeyValuePair<string, string>("RUN TO INSTRUCTION (ANY EXA / THIS EXA)", "\u0414\u041E \u0418\u041D\u0421\u0422\u0420\u0423\u041A\u0426\u0418\u0418 (\u041B\u042E\u0411\u0410\u042F EXA / \u042D\u0422\u0410 EXA)"),
        new KeyValuePair<string, string>("SHOW GOAL", "\u041F\u041E\u041A\u0410\u0417\u0410\u0422\u042C \u0426\u0415\u041B\u042C"),
        new KeyValuePair<string, string>("PREVIOUS / NEXT EXA WINDOW", "\u041F\u0420\u0415\u0414\u042B\u0414\u0423\u0429\u0415\u0415 / \u0421\u041B\u0415\u0414\u0423\u042E\u0429\u0415\u0415 \u041E\u041A\u041D\u041E EXA"),
        new KeyValuePair<string, string>("CREATE NEW EXA", "\u0421\u041E\u0417\u0414\u0410\u0422\u042C EXA"),
        new KeyValuePair<string, string>("CUT / COPY / PASTE", "\u0412\u042B\u0420\u0415\u0417\u0410\u0422\u042C / \u041A\u041E\u041F\u0418\u0420\u041E\u0412\u0410\u0422\u042C / \u0412\u0421\u0422\u0410\u0412\u0418\u0422\u042C"),
        new KeyValuePair<string, string>("UNDO / REDO", "\u041E\u0422\u041C\u0415\u041D\u0418\u0422\u042C / \u041F\u041E\u0412\u0422\u041E\u0420\u0418\u0422\u042C"),
        new KeyValuePair<string, string>("HOSTNAME", "\u0418\u041C\u042F \u0425\u041E\u0421\u0422\u0410"),
        new KeyValuePair<string, string>("PROFANITY", "\u041D\u0415\u0426\u0415\u041D\u0417\u0423\u0420\u041D\u0410\u042F \u041B\u0415\u041A\u0421\u0418\u041A\u0410"),
        new KeyValuePair<string, string>("SHOW", "\u041F\u041E\u041A\u0410\u0417\u042B\u0412\u0410\u0422\u042C"),
        new KeyValuePair<string, string>("HIDE", "\u0421\u041A\u0420\u042B\u0412\u0410\u0422\u042C"),
        new KeyValuePair<string, string>("MOUSE CURSOR", "\u041A\u0423\u0420\u0421\u041E\u0420 \u041C\u042B\u0428\u0418"),
        new KeyValuePair<string, string>("HARDWARE", "\u0410\u041F\u041F\u0410\u0420\u0410\u0422\u041D\u042B\u0419"),
        new KeyValuePair<string, string>("SOFTWARE", "\u041F\u0420\u041E\u0413\u0420\u0410\u041C\u041C\u041D\u042B\u0419"),
        new KeyValuePair<string, string>("CODE FONT SIZE", "\u0420\u0410\u0417\u041C\u0415\u0420 \u0428\u0420\u0418\u0424\u0422\u0410 \u041A\u041E\u0414\u0410"),
        new KeyValuePair<string, string>("NORMAL", "\u041E\u0411\u042B\u0427\u041D\u042B\u0419"),
        new KeyValuePair<string, string>("LARGER", "\u041A\u0420\u0423\u041F\u041D\u0415\u0415"),
        new KeyValuePair<string, string>("HACK*MATCH CRT EFFECT", "\u042D\u0424\u0424\u0415\u041A\u0422 CRT \u0412 HACK*MATCH"),
        new KeyValuePair<string, string>("NO DISTORTION", "\u0411\u0415\u0417 \u0418\u0421\u041A\u0410\u0416\u0415\u041D\u0418\u0419"),
        new KeyValuePair<string, string>("REPLACE BACKUP BATTERY", "\u0417\u0410\u041C\u0415\u041D\u0418\u0422\u042C \u0420\u0415\u0417\u0415\u0420\u0412\u041D\u0423\u042E \u0411\u0410\u0422\u0410\u0420\u0415\u042E"),
        new KeyValuePair<string, string>("REFERENCE MATERIALS", "\u0421\u041F\u0420\u0410\u0412\u041E\u0427\u041D\u042B\u0415 \u041C\u0410\u0422\u0415\u0420\u0418\u0410\u041B\u042B"),
        new KeyValuePair<string, string>("ISSUE #1", "\u0412\u042B\u041F\u0423\u0421\u041A 1"),
        new KeyValuePair<string, string>("ISSUE #2", "\u0412\u042B\u041F\u0423\u0421\u041A 2"),
        new KeyValuePair<string, string>("EPILOGUE", "\u042D\u041F\u0418\u041B\u041E\u0413"),
        new KeyValuePair<string, string>("DIGITAL VERSION (PDF)", "\u0426\u0418\u0424\u0420\u041E\u0412\u0410\u042F \u0412\u0415\u0420\u0421\u0418\u042F (PDF)"),
        new KeyValuePair<string, string>("PRINTABLE VERSION (PDF)", "\u0412\u0415\u0420\u0421\u0418\u042F \u0414\u041B\u042F \u041F\u0415\u0427\u0410\u0422\u0418 (PDF)"),
        new KeyValuePair<string, string>("DIGITAL VERSION", "\u0426\u0418\u0424\u0420\u041E\u0412\u0410\u042F \u0412\u0415\u0420\u0421\u0418\u042F"),
        new KeyValuePair<string, string>("PRINTABLE VERSION", "\u0412\u0415\u0420\u0421\u0418\u042F \u0414\u041B\u042F \u041F\u0415\u0427\u0410\u0422\u0418"),
        new KeyValuePair<string, string>("LETTER-SIZE PAPER", "\u0411\u0423\u041C\u0410\u0413\u0410 LETTER"),
        new KeyValuePair<string, string>("A4-SIZE PAPER", "\u0424\u041E\u0420\u041C\u0410\u0422 A4"),
        new KeyValuePair<string, string>("Task #", "\u0417\u0430\u0434\u0430\u0447\u0430 \u2116"),
        new KeyValuePair<string, string>("Receipt", "\u0427\u0435\u043A"),
        new KeyValuePair<string, string>("Description", "\u041E\u043F\u0438\u0441\u0430\u043D\u0438\u0435"),
        new KeyValuePair<string, string>("Price", "\u0426\u0435\u043D\u0430"),
        new KeyValuePair<string, string>("Account Balance", "\u0411\u0430\u043B\u0430\u043D\u0441 \u0441\u0447\u0451\u0442\u0430"),
        new KeyValuePair<string, string>("Transcribe the items from this receipt", "\u041F\u0435\u0440\u0435\u043F\u0438\u0448\u0438\u0442\u0435 \u043F\u043E\u0437\u0438\u0446\u0438\u0438 \u0438\u0437 \u044D\u0442\u043E\u0433\u043E \u0447\u0435\u043A\u0430"),
        new KeyValuePair<string, string>("Welcome to Workhouse!", "\u0414\u043E\u0431\u0440\u043E \u043F\u043E\u0436\u0430\u043B\u043E\u0432\u0430\u0442\u044C \u0432 Workhouse!"),
        new KeyValuePair<string, string>("The only service that pays you for simple tasks you can do right from your computer!", "\u0415\u0434\u0438\u043D\u0441\u0442\u0432\u0435\u043D\u043D\u044B\u0439 \u0441\u0435\u0440\u0432\u0438\u0441, \u043A\u043E\u0442\u043E\u0440\u044B\u0439 \u043F\u043B\u0430\u0442\u0438\u0442 \u0437\u0430 \u043F\u0440\u043E\u0441\u0442\u044B\u0435 \u0437\u0430\u0434\u0430\u043D\u0438\u044F \u043F\u0440\u044F\u043C\u043E \u0441 \u0442\u0432\u043E\u0435\u0433\u043E \u043A\u043E\u043C\u043F\u044C\u044E\u0442\u0435\u0440\u0430!"),
        new KeyValuePair<string, string>("Easy work. Easy money.", "\u041F\u0440\u043E\u0441\u0442\u0430\u044F \u0440\u0430\u0431\u043E\u0442\u0430. \u041F\u0440\u043E\u0441\u0442\u044B\u0435 \u0434\u0435\u043D\u044C\u0433\u0438."),
        new KeyValuePair<string, string>("You will be shown images of receipts generated in the course of normal everyday business and will enter them digitally for bookkeeping and reimbursement purposes.", "\u0412\u0430\u043C \u0431\u0443\u0434\u0443\u0442 \u043F\u043E\u043A\u0430\u0437\u0430\u043D\u044B \u0438\u0437\u043E\u0431\u0440\u0430\u0436\u0435\u043D\u0438\u044F \u0447\u0435\u043A\u043E\u0432 \u0438\u0437 \u043E\u0431\u044B\u0447\u043D\u043E\u0439 \u043F\u043E\u0432\u0441\u0435\u0434\u043D\u0435\u0432\u043D\u043E\u0439 \u0440\u0430\u0431\u043E\u0442\u044B; \u0438\u0445 \u043D\u0443\u0436\u043D\u043E \u0431\u0443\u0434\u0435\u0442 \u0432\u043D\u043E\u0441\u0438\u0442\u044C \u0432 \u0446\u0438\u0444\u0440\u043E\u0432\u043E\u043C \u0432\u0438\u0434\u0435 \u0434\u043B\u044F \u0443\u0447\u0451\u0442\u0430 \u0438 \u0432\u043E\u0437\u043C\u0435\u0449\u0435\u043D\u0438\u0439."),
        new KeyValuePair<string, string>("Save a busy executive a minute or two!", "\u0421\u044D\u043A\u043E\u043D\u043E\u043C\u044C\u0442\u0435 \u0437\u0430\u043D\u044F\u0442\u043E\u043C\u0443 \u0431\u043E\u0441\u0441\u0443 \u043F\u0430\u0440\u0443 \u043C\u0438\u043D\u0443\u0442!"),
        new KeyValuePair<string, string>("COVER", "\u041E\u0411\u041B\u041E\u0416\u041A\u0410"),
        new KeyValuePair<string, string>("STAPLES", "\u0421\u041A\u041E\u0411\u042B"),
    };
    private static readonly Dictionary<string, string> TranslationMap =
        Translations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: PatchOriginalExe.exe <assembly-path>");
            return 1;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine("Assembly not found: " + assemblyPath);
            return 1;
        }

        string tempPath = assemblyPath + ".ru_tmp";
        AssemblyDefinition assembly = null;

        try
        {
            assembly = AssemblyDefinition.ReadAssembly(
                assemblyPath,
                new ReaderParameters
                {
                    InMemory = true,
                    ReadWrite = false,
                });

            ModuleDefinition module = assembly.MainModule;
            bool changed = false;

            TypeDefinition shimType;
            MethodDefinition applyMethod;
            changed |= EnsureShimType(module, out shimType, out applyMethod);
            changed |= ReplaceStringLiterals(module);
            changed |= PatchStringMethodReturns(module, shimType, applyMethod);

            assembly.Write(tempPath);
            assembly.Dispose();
            assembly = null;

            File.Copy(tempPath, assemblyPath, true);
            File.Delete(tempPath);

            Console.WriteLine(changed
                ? "Patched EXAPUNKS.exe string source."
                : "EXAPUNKS.exe already matched the current patch.");
            return 0;
        }
        finally
        {
            if (assembly != null)
            {
                assembly.Dispose();
            }

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static TypeDefinition CreateShimType(ModuleDefinition module)
    {
        TypeDefinition type = new TypeDefinition(
            string.Empty,
            ShimTypeName,
            TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.NotPublic | TypeAttributes.BeforeFieldInit,
            module.TypeSystem.Object);

        type.Methods.Add(CreateShimMethod(module));
        return type;
    }

    private static MethodDefinition CreateShimMethod(ModuleDefinition module)
    {
        MethodDefinition applyMethod = new MethodDefinition(
            ShimMethodName,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.String);

        applyMethod.Parameters.Add(new ParameterDefinition("input", ParameterAttributes.None, module.TypeSystem.String));
        RewriteShimMethodBody(module, applyMethod);
        return applyMethod;
    }

    private static bool EnsureShimType(
        ModuleDefinition module,
        out TypeDefinition shimType,
        out MethodDefinition applyMethod)
    {
        bool changed = false;
        shimType = module.Types.FirstOrDefault(t => t.Name == ShimTypeName);
        if (shimType == null)
        {
            shimType = CreateShimType(module);
            module.Types.Add(shimType);
            applyMethod = shimType.Methods.First(m => m.Name == ShimMethodName);
            return true;
        }

        applyMethod = shimType.Methods.FirstOrDefault(m => m.Name == ShimMethodName);
        if (applyMethod == null)
        {
            applyMethod = CreateShimMethod(module);
            shimType.Methods.Add(applyMethod);
            changed = true;
        }
        else
        {
            if (applyMethod.ReturnType.FullName != module.TypeSystem.String.FullName)
            {
                applyMethod.ReturnType = module.TypeSystem.String;
                changed = true;
            }

            if (applyMethod.Parameters.Count != 1 ||
                applyMethod.Parameters[0].ParameterType.FullName != module.TypeSystem.String.FullName)
            {
                applyMethod.Parameters.Clear();
                applyMethod.Parameters.Add(new ParameterDefinition("input", ParameterAttributes.None, module.TypeSystem.String));
                changed = true;
            }

            RewriteShimMethodBody(module, applyMethod);
            changed = true;
        }

        return changed;
    }

    private static void RewriteShimMethodBody(ModuleDefinition module, MethodDefinition applyMethod)
    {
        applyMethod.Body = new MethodBody(applyMethod);
        applyMethod.Body.InitLocals = false;

        MethodReference stringEquals = module.ImportReference(
            typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) }));

        ILProcessor il = applyMethod.Body.GetILProcessor();
        Instruction afterNullCheck = il.Create(OpCodes.Nop);

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Brtrue_S, afterNullCheck));
        il.Append(il.Create(OpCodes.Ldnull));
        il.Append(il.Create(OpCodes.Ret));
        il.Append(afterNullCheck);

        foreach (KeyValuePair<string, string> pair in Translations)
        {
            Instruction nextCheck = il.Create(OpCodes.Nop);
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldstr, pair.Key));
            il.Append(il.Create(OpCodes.Call, stringEquals));
            il.Append(il.Create(OpCodes.Brfalse_S, nextCheck));
            il.Append(il.Create(OpCodes.Ldstr, pair.Value));
            il.Append(il.Create(OpCodes.Ret));
            il.Append(nextCheck);
        }

        il.Append(il.Create(OpCodes.Ldarg_0));
        il.Append(il.Create(OpCodes.Ret));
    }

    private static bool ReplaceStringLiterals(ModuleDefinition module)
    {
        bool changed = false;
        foreach (TypeDefinition type in GetAllTypes(module.Types))
        {
            if (type.Name == ShimTypeName)
            {
                continue;
            }

            foreach (MethodDefinition method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (Instruction instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode.Code != Code.Ldstr)
                    {
                        continue;
                    }

                    string literal = instruction.Operand as string;
                    if (string.IsNullOrEmpty(literal))
                    {
                        continue;
                    }

                    string replacement;
                    if (TryTranslateLiteral(literal, out replacement) &&
                        !string.Equals(literal, replacement, StringComparison.Ordinal))
                    {
                        instruction.Operand = replacement;
                        changed = true;
                    }
                }
            }
        }

        return changed;
    }

    private static bool TryTranslateLiteral(string literal, out string replacement)
    {
        if (TranslationMap.TryGetValue(literal, out replacement))
        {
            return true;
        }

        switch (literal)
        {
            case "WorkHouse":
                replacement = "WORKHOUSE";
                return true;
            default:
                replacement = null;
                return false;
        }
    }

    private static bool PatchStringMethodReturns(
        ModuleDefinition module,
        TypeDefinition shimType,
        MethodDefinition applyMethod)
    {
        MethodReference applyRef = module.ImportReference(applyMethod);
        bool changed = false;
        foreach (TypeDefinition type in GetAllTypes(module.Types))
        {
            if (type.Name == ShimTypeName)
            {
                continue;
            }

            foreach (MethodDefinition method in type.Methods)
            {
                if (!method.HasBody || method.ReturnType.FullName != "System.String")
                {
                    continue;
                }

                ILProcessor il = method.Body.GetILProcessor();
                List<Instruction> returns = method.Body.Instructions
                    .Where(i => i.OpCode.Code == Code.Ret)
                    .ToList();

                foreach (Instruction ret in returns)
                {
                    Instruction previous = GetPreviousNonNop(method.Body.Instructions, ret);
                    if (IsApplyCall(previous, shimType))
                    {
                        continue;
                    }

                    Instruction callApply = il.Create(OpCodes.Call, applyRef);
                    RedirectInstructionReferences(method.Body, ret, callApply);
                    il.InsertBefore(ret, callApply);
                    changed = true;
                }
            }
        }

        return changed;
    }

    private static bool PatchStringMethodParameters(
        ModuleDefinition module,
        TypeDefinition shimType,
        MethodDefinition applyMethod)
    {
        MethodReference applyRef = module.ImportReference(applyMethod);
        bool changed = false;

        foreach (TypeDefinition type in GetAllTypes(module.Types))
        {
            if (type.Name == ShimTypeName)
            {
                continue;
            }

            foreach (MethodDefinition method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                List<ParameterDefinition> stringParameters = method.Parameters
                    .Where(p => p.ParameterType.FullName == "System.String")
                    .ToList();
                if (stringParameters.Count == 0 || HasParameterShimPreamble(method, stringParameters, shimType))
                {
                    continue;
                }

                ILProcessor il = method.Body.GetILProcessor();
                Instruction originalFirst = method.Body.Instructions.First();
                Instruction firstInserted = originalFirst;

                for (int index = stringParameters.Count - 1; index >= 0; index--)
                {
                    ParameterDefinition parameter = stringParameters[index];
                    Instruction starg = il.Create(OpCodes.Starg, parameter);
                    Instruction callApply = il.Create(OpCodes.Call, applyRef);
                    Instruction ldarg = il.Create(OpCodes.Ldarg, parameter);

                    il.InsertBefore(firstInserted, starg);
                    il.InsertBefore(starg, callApply);
                    il.InsertBefore(callApply, ldarg);
                    firstInserted = ldarg;
                }

                RedirectInstructionReferences(method.Body, originalFirst, firstInserted);
                changed = true;
            }
        }

        return changed;
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(Mono.Collections.Generic.Collection<TypeDefinition> types)
    {
        foreach (TypeDefinition type in types)
        {
            yield return type;
            foreach (TypeDefinition nested in GetAllTypes(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }

    private static bool IsApplyCall(Instruction instruction, TypeDefinition shimType)
    {
        if (instruction == null)
        {
            return false;
        }

        if (instruction.OpCode.Code != Code.Call && instruction.OpCode.Code != Code.Callvirt)
        {
            return false;
        }

        MethodReference calledMethod = instruction.Operand as MethodReference;
        return calledMethod != null &&
               calledMethod.DeclaringType.Name == shimType.Name &&
               calledMethod.Name == ShimMethodName;
    }

    private static bool HasParameterShimPreamble(
        MethodDefinition method,
        IList<ParameterDefinition> stringParameters,
        TypeDefinition shimType)
    {
        if (!method.HasBody || stringParameters.Count == 0)
        {
            return false;
        }

        int cursor = 0;
        Mono.Collections.Generic.Collection<Instruction> instructions = method.Body.Instructions;

        for (int parameterIndex = 0; parameterIndex < stringParameters.Count; parameterIndex++)
        {
            ParameterDefinition parameter = stringParameters[parameterIndex];

            cursor = SkipNops(instructions, cursor);
            if (cursor >= instructions.Count || !IsParameterLoad(instructions[cursor], parameter))
            {
                return false;
            }

            cursor = SkipNops(instructions, cursor + 1);
            if (cursor >= instructions.Count || !IsApplyCall(instructions[cursor], shimType))
            {
                return false;
            }

            cursor = SkipNops(instructions, cursor + 1);
            if (cursor >= instructions.Count || !IsParameterStore(instructions[cursor], parameter))
            {
                return false;
            }

            cursor++;
        }

        return true;
    }

    private static int SkipNops(Mono.Collections.Generic.Collection<Instruction> instructions, int start)
    {
        int cursor = start;
        while (cursor < instructions.Count && instructions[cursor].OpCode.Code == Code.Nop)
        {
            cursor++;
        }

        return cursor;
    }

    private static bool IsParameterLoad(Instruction instruction, ParameterDefinition parameter)
    {
        if (instruction == null)
        {
            return false;
        }

        if (instruction.OpCode.Code == Code.Ldarg)
        {
            ParameterDefinition operand = instruction.Operand as ParameterDefinition;
            return operand != null && operand.Index == parameter.Index;
        }

        if (instruction.OpCode.Code == Code.Ldarg_S)
        {
            ParameterDefinition operand = instruction.Operand as ParameterDefinition;
            return operand != null && operand.Index == parameter.Index;
        }

        return false;
    }

    private static bool IsParameterStore(Instruction instruction, ParameterDefinition parameter)
    {
        if (instruction == null)
        {
            return false;
        }

        if (instruction.OpCode.Code == Code.Starg || instruction.OpCode.Code == Code.Starg_S)
        {
            ParameterDefinition operand = instruction.Operand as ParameterDefinition;
            return operand != null && operand.Index == parameter.Index;
        }

        return false;
    }

    private static void RedirectInstructionReferences(MethodBody body, Instruction from, Instruction to)
    {
        foreach (Instruction instruction in body.Instructions)
        {
            if (ReferenceEquals(instruction.Operand, from))
            {
                instruction.Operand = to;
                continue;
            }

            Instruction[] targets = instruction.Operand as Instruction[];
            if (targets == null)
            {
                continue;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                if (ReferenceEquals(targets[index], from))
                {
                    targets[index] = to;
                }
            }
        }

        foreach (ExceptionHandler handler in body.ExceptionHandlers)
        {
            if (ReferenceEquals(handler.TryStart, from))
            {
                handler.TryStart = to;
            }

            if (ReferenceEquals(handler.TryEnd, from))
            {
                handler.TryEnd = to;
            }

            if (ReferenceEquals(handler.HandlerStart, from))
            {
                handler.HandlerStart = to;
            }

            if (ReferenceEquals(handler.HandlerEnd, from))
            {
                handler.HandlerEnd = to;
            }

            if (ReferenceEquals(handler.FilterStart, from))
            {
                handler.FilterStart = to;
            }
        }
    }

    private static Instruction GetPreviousNonNop(Mono.Collections.Generic.Collection<Instruction> instructions, Instruction instruction)
    {
        if (instructions == null || instruction == null)
        {
            return null;
        }

        for (int index = instructions.Count - 1; index >= 0; index--)
        {
            if (!ReferenceEquals(instructions[index], instruction))
            {
                continue;
            }

            for (int inner = index - 1; inner >= 0; inner--)
            {
                if (instructions[inner].OpCode.Code != Code.Nop)
                {
                    return instructions[inner];
                }
            }

            return null;
        }

        return null;
    }

}
