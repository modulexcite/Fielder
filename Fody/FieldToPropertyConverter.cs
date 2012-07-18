using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;


public class FieldToPropertyConverter
{

    MsCoreReferenceFinder msCoreReferenceFinder;
    TypeSystem typeSystem;
    public Dictionary<FieldDefinition, PropertyDefinition> ForwardedFields;
    ModuleWeaver moduleWeaver;

    public FieldToPropertyConverter(ModuleWeaver moduleWeaver, MsCoreReferenceFinder msCoreReferenceFinder, TypeSystem typeSystem)
    {
        ForwardedFields = new Dictionary<FieldDefinition, PropertyDefinition>();
        this.moduleWeaver = moduleWeaver;
        this.msCoreReferenceFinder = msCoreReferenceFinder;
        this.typeSystem = typeSystem;
    }

    void Process(TypeDefinition typeDefinition)
    {
        foreach (var field in typeDefinition.Fields)
        {
            ProcessField(typeDefinition, field);
        }
    }

    void ProcessField(TypeDefinition typeDefinition, FieldDefinition field)
    {
        var name = field.Name;
        if (!field.IsPublic || field.IsStatic || !char.IsUpper(name, 0))
        {
            return;
        }
        if (typeDefinition.HasGenericParameters)
        {
            var message = string.Format("Skipped public field '{0}.{1}' because generic types are not currently supported. You should make this a public property instead.", typeDefinition.Name, field.Name);
            moduleWeaver.LogWarning(message);
            return;
        }

        field.Name = string.Format("<{0}>k__BackingField", name);
        field.IsPublic = false;
        field.IsPrivate = true;
        var get = GetGet(field, name);
        typeDefinition.Methods.Add(get);

        var set = GetSet(field, name);
        typeDefinition.Methods.Add(set);

        var propertyDefinition = new PropertyDefinition(name, PropertyAttributes.None, field.FieldType)
                                     {
                                         GetMethod = get,
                                         SetMethod = set
                                     };
        foreach (var customAttribute in field.CustomAttributes)
        {
            propertyDefinition.CustomAttributes.Add(customAttribute);
        }
        typeDefinition.Properties.Add(propertyDefinition);
        ForwardedFields.Add(field, propertyDefinition);
    }

    MethodDefinition GetGet(FieldDefinition field, string name)
    {
        var get = new MethodDefinition("get_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, field.FieldType);
        var instructions = get.Body.Instructions;
        instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        instructions.Add(Instruction.Create(OpCodes.Ldfld, field));
        instructions.Add(Instruction.Create(OpCodes.Stloc_0));
        var inst = Instruction.Create(OpCodes.Ldloc_0);
        instructions.Add(Instruction.Create(OpCodes.Br_S, inst));
        instructions.Add(inst);
        instructions.Add(Instruction.Create(OpCodes.Ret));
        get.Body.Variables.Add(new VariableDefinition(field.FieldType));
        get.Body.InitLocals = true;
        get.SemanticsAttributes = MethodSemanticsAttributes.Getter;
        get.CustomAttributes.Add(new CustomAttribute(msCoreReferenceFinder.CompilerGeneratedReference));
        return get;
    }

    MethodDefinition GetSet(FieldDefinition field, string name)
    {
        var set = new MethodDefinition("set_" + name, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeSystem.Void);
        var instructions = set.Body.Instructions;
        instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
        instructions.Add(Instruction.Create(OpCodes.Stfld, field));
        instructions.Add(Instruction.Create(OpCodes.Ret));
        set.Parameters.Add(new ParameterDefinition(field.FieldType));
        set.SemanticsAttributes = MethodSemanticsAttributes.Setter;
        set.CustomAttributes.Add(new CustomAttribute(msCoreReferenceFinder.CompilerGeneratedReference));
        return set;
    }

    public void Execute()
    {
        foreach (var type in moduleWeaver.ModuleDefinition.GetAllTypeDefinitions())
        {
            if (type.IsInterface)
            {
                continue;
            }
            if (type.IsValueType)
            {
                continue;
            }
            if (type.IsEnum)
            {
                continue;
            }
            Process(type);
        }
    }
}