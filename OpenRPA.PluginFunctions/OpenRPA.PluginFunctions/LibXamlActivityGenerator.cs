using System;
using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Emits one concrete NativeActivity subclass per Lib XAML into a stable assembly
    /// (OpenRPA.PluginFunctions.LibXaml.dll) so parent workflows can save and reload.
    /// </summary>
    internal sealed class LibXamlActivityGenerator
    {
        public const string AssemblySimpleName = "OpenRPA.PluginFunctions.LibXaml";
        public const string DllFileName = "OpenRPA.PluginFunctions.LibXaml.dll";

        private readonly AssemblyBuilder _assembly;
        private readonly ModuleBuilder _module;
        private readonly string _outputDirectory;
        private readonly Dictionary<string, Type> _types = new Dictionary<string, Type>(StringComparer.Ordinal);
        private bool _saved;

        public LibXamlActivityGenerator(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("outputDirectory is required.", "outputDirectory");
            }

            Directory.CreateDirectory(outputDirectory);
            _outputDirectory = outputDirectory;

            var assemblyName = new AssemblyName(AssemblySimpleName);
            _assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.RunAndSave,
                outputDirectory);
            _module = _assembly.DefineDynamicModule(AssemblySimpleName, DllFileName);
        }

        public static string GetGeneratedDirectory()
        {
            return Path.Combine(LibXamlPaths.GetLibsRoot(), ".generated");
        }

        public static string GetDllPath()
        {
            return Path.Combine(GetGeneratedDirectory(), DllFileName);
        }

        public static Assembly TryLoadExistingAssembly()
        {
            string dllPath = GetDllPath();
            if (!File.Exists(dllPath))
            {
                return null;
            }

            try
            {
                foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (loaded == null)
                    {
                        continue;
                    }

                    string name = loaded.GetName().Name;
                    if (string.Equals(name, AssemblySimpleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return loaded;
                    }
                }

                return Assembly.LoadFrom(dllPath);
            }
            catch (Exception ex)
            {
                Log.Warning("PluginFunctions: failed loading LibXaml assembly: " + ex.Message);
                return null;
            }
        }

        public static void EnsureAssemblyResolveHook()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args == null || string.IsNullOrEmpty(args.Name))
            {
                return null;
            }

            string simple = new AssemblyName(args.Name).Name;
            if (!string.Equals(simple, AssemblySimpleName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return TryLoadExistingAssembly();
        }

        public Type GetOrCreateActivityType(LibXamlEntry entry, IList<LibXamlArgumentSpec> arguments)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.TypeKey))
            {
                return null;
            }

            Type existing;
            if (_types.TryGetValue(entry.TypeKey, out existing))
            {
                return existing;
            }

            string typeName = "LibXaml_" + entry.TypeKey;
            TypeBuilder typeBuilder = _module.DefineType(
                typeName,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
                typeof(InvokeLibXamlActivityBase));

            EmitConstructor(typeBuilder, entry.DisplayName ?? entry.TypeKey);
            EmitRelativePathOverride(typeBuilder, InvokeLibXamlActivityBase.ToRelativePath(entry.FilePath));

            if (arguments != null)
            {
                var usedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (LibXamlArgumentSpec spec in arguments)
                {
                    if (spec == null
                        || string.IsNullOrWhiteSpace(spec.Name)
                        || spec.ArgumentClrType == null)
                    {
                        continue;
                    }

                    string propName = SanitizePropertyName(spec.Name);
                    if (!usedNames.Add(propName))
                    {
                        continue;
                    }

                    if (!typeof(Argument).IsAssignableFrom(spec.ArgumentClrType))
                    {
                        continue;
                    }

                    EmitArgumentProperty(typeBuilder, propName, spec.ArgumentClrType);
                }
            }

            Type created = typeBuilder.CreateType();
            _types[entry.TypeKey] = created;
            return created;
        }

        public void Save()
        {
            if (_saved)
            {
                return;
            }

            try
            {
                _assembly.Save(DllFileName);
                _saved = true;
                Log.Information("PluginFunctions: saved LibXaml assembly to " + GetDllPath());
            }
            catch (Exception ex)
            {
                Log.Warning("PluginFunctions: could not save LibXaml assembly: " + ex.Message);
            }
        }

        private static void EmitConstructor(TypeBuilder typeBuilder, string displayName)
        {
            ConstructorInfo baseCtor = typeof(InvokeLibXamlActivityBase)
                .GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
                ?? typeof(NativeActivity).GetConstructor(Type.EmptyTypes);

            ConstructorBuilder ctor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                Type.EmptyTypes);

            ILGenerator il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, baseCtor);

            MethodInfo setDisplayName = typeof(Activity)
                .GetProperty("DisplayName")
                .GetSetMethod();
            if (setDisplayName != null && !string.IsNullOrWhiteSpace(displayName))
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldstr, displayName);
                il.Emit(OpCodes.Callvirt, setDisplayName);
            }

            il.Emit(OpCodes.Ret);
        }

        private static void EmitRelativePathOverride(TypeBuilder typeBuilder, string relativePath)
        {
            MethodInfo baseMethod = typeof(InvokeLibXamlActivityBase).GetMethod(
                "GetRelativePath",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (baseMethod == null)
            {
                throw new InvalidOperationException("GetRelativePath not found on base type.");
            }

            MethodBuilder method = typeBuilder.DefineMethod(
                "GetRelativePath",
                MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(string),
                Type.EmptyTypes);

            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldstr, relativePath ?? string.Empty);
            il.Emit(OpCodes.Ret);
            typeBuilder.DefineMethodOverride(method, baseMethod);
        }

        private static void EmitArgumentProperty(TypeBuilder typeBuilder, string propertyName, Type argumentType)
        {
            FieldBuilder field = typeBuilder.DefineField(
                "_" + propertyName,
                argumentType,
                FieldAttributes.Private);

            PropertyBuilder property = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.None,
                argumentType,
                null);

            MethodBuilder getter = typeBuilder.DefineMethod(
                "get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                argumentType,
                Type.EmptyTypes);
            ILGenerator getIl = getter.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, field);
            getIl.Emit(OpCodes.Ret);

            MethodBuilder setter = typeBuilder.DefineMethod(
                "set_" + propertyName,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                typeof(void),
                new[] { argumentType });
            ILGenerator setIl = setter.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, field);
            setIl.Emit(OpCodes.Ret);

            property.SetGetMethod(getter);
            property.SetSetMethod(setter);

            // Property grid: Argument.Input / Argument.In-Output / Argument.Output
            string category = ResolveArgumentCategory(argumentType);
            ConstructorInfo categoryCtor = typeof(CategoryAttribute).GetConstructor(new[] { typeof(string) });
            if (categoryCtor != null)
            {
                var categoryAttr = new CustomAttributeBuilder(categoryCtor, new object[] { category });
                property.SetCustomAttribute(categoryAttr);
                getter.SetCustomAttribute(categoryAttr);
            }
        }

        private static string ResolveArgumentCategory(Type argumentType)
        {
            if (argumentType != null && argumentType.IsGenericType)
            {
                Type definition = argumentType.GetGenericTypeDefinition();
                if (definition == typeof(OutArgument<>))
                {
                    return "Argument.Output";
                }

                if (definition == typeof(InOutArgument<>))
                {
                    return "Argument.In-Output";
                }

                if (definition == typeof(InArgument<>))
                {
                    return "Argument.Input";
                }
            }

            if (typeof(InOutArgument).IsAssignableFrom(argumentType))
            {
                return "Argument.In-Output";
            }

            if (typeof(OutArgument).IsAssignableFrom(argumentType))
            {
                return "Argument.Output";
            }

            return "Argument.Input";
        }

        private static string SanitizePropertyName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Arg";
            }

            bool valid = char.IsLetter(name[0]) || name[0] == '_';
            if (valid)
            {
                for (int i = 1; i < name.Length; i++)
                {
                    if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (valid)
            {
                return name;
            }

            var chars = new char[name.Length];
            int n = 0;
            for (int i = 0; i < name.Length; i++)
            {
                char ch = name[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    chars[n++] = ch;
                }
            }

            string sanitized = n == 0 ? "Arg" : new string(chars, 0, n);
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "A" + sanitized;
            }

            return sanitized;
        }
    }
}
