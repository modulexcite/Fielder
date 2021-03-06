﻿using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class ModuleWeaver
{
    public Action<string, SequencePoint> LogErrorPoint;
    public Action<string> LogInfo { get; set; }
    public Action<string> LogWarning { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }

    public ModuleWeaver()
    {
        LogInfo = s => { };
        LogErrorPoint = (s,p) => { };
        LogWarning = s => { };
    }

    public void Execute()
    {
        var msCoreReferenceFinder = new MsCoreReferenceFinder(this, ModuleDefinition.AssemblyResolver);
        msCoreReferenceFinder.Execute();
        var allTypes = ModuleDefinition.GetTypes().ToList();

        var fieldToPropertyFinder = new MethodFinder(allTypes);
        fieldToPropertyFinder.Execute();
        var fieldToPropertyConverter = new FieldToPropertyConverter(this, msCoreReferenceFinder, ModuleDefinition.TypeSystem, allTypes);
        fieldToPropertyConverter.Execute();
        var fieldToPropertyForwarder = new FieldToPropertyForwarder(this, fieldToPropertyConverter, msCoreReferenceFinder, fieldToPropertyFinder);
        fieldToPropertyForwarder.Execute();

    }
}