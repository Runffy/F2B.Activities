using System;
using System.Activities;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CSharp;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    /// <summary>
    /// Compiles user Program.cs plus a generated Args type, then invokes Main.
    /// </summary>
    internal static class InvokeCSharpCodeHost
    {
        private static readonly object CompileGate = new object();
        private static readonly object RunGate = new object();
        private static readonly ConcurrentDictionary<string, CachedScript> Cache = new ConcurrentDictionary<string, CachedScript>();

        private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while"
        };

        public static void Run(string code, Dictionary<string, Argument> arguments, CodeActivityContext context)
        {
            List<ArgSpec> specs = BuildSpecs(arguments);
            CachedScript script = GetOrCompile(code, specs);
            BindProperties(script, specs);

            object argsInstance = Activator.CreateInstance(script.ArgsType);
            FillInputs(argsInstance, specs, arguments, context);

            lock (RunGate)
            {
                TextWriter oldOut = Console.Out;
                TextWriter oldError = Console.Error;
                var writer = new OutputTextWriter();
                try
                {
                    Console.SetOut(writer);
                    Console.SetError(writer);
                    InvokeMain(script.Main, argsInstance);
                    writer.Flush();
                }
                finally
                {
                    Console.SetOut(oldOut);
                    Console.SetError(oldError);
                }
            }

            WriteOutputs(argsInstance, specs, arguments, context);
        }

        public static IList<string> DescribeArgsProperties(Dictionary<string, Argument> arguments)
        {
            List<ArgSpec> specs = BuildSpecs(arguments);
            if (specs.Count == 0)
            {
                return new[] { "Args has no properties (Map Arguments to add In / Out / InOut)." };
            }

            var lines = new List<string>(specs.Count);
            foreach (ArgSpec spec in specs)
            {
                lines.Add(spec.Identifier + " (" + spec.Direction + ", " + spec.TypeName + ")");
            }

            return lines;
        }

        private static CachedScript GetOrCompile(string code, List<ArgSpec> specs)
        {
            string key = BuildCacheKey(code, specs);
            CachedScript cached;
            if (Cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            lock (CompileGate)
            {
                if (Cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                cached = Compile(code, specs);
                Cache[key] = cached;
                return cached;
            }
        }

        private static CachedScript Compile(string code, List<ArgSpec> specs)
        {
            string argsSource = BuildArgsSource(specs);
            string programSource = "#line 1 \"Program.cs\"" + Environment.NewLine + code;

            var parameters = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                IncludeDebugInformation = false,
                TreatWarningsAsErrors = false,
                CompilerOptions = "/optimize-"
            };
            foreach (string reference in GetReferenceAssemblies())
            {
                parameters.ReferencedAssemblies.Add(reference);
            }

            using (var provider = new CSharpCodeProvider())
            {
                CompilerResults result = provider.CompileAssemblyFromSource(parameters, argsSource, programSource);
                if (result.Errors != null && result.Errors.HasErrors)
                {
                    throw new InvalidOperationException(FormatCompileErrors(result));
                }

                Assembly assembly = result.CompiledAssembly;
                Type argsType = assembly.GetType("Args", false);
                if (argsType == null)
                {
                    throw new InvalidOperationException("Invoke C# Code: generated Args type was not found.");
                }

                MethodInfo main = FindEntryPoint(assembly, argsType);
                return new CachedScript
                {
                    ArgsType = argsType,
                    Main = main
                };
            }
        }

        private static MethodInfo FindEntryPoint(Assembly assembly, Type argsType)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            Type program = types.FirstOrDefault(t => t.IsClass && string.Equals(t.Name, "Program", StringComparison.Ordinal));
            MethodInfo method = FindMainOnType(program, argsType);
            if (method != null)
            {
                return method;
            }

            foreach (Type type in types)
            {
                method = FindMainOnType(type, argsType);
                if (method != null)
                {
                    return method;
                }
            }

            throw new InvalidOperationException(
                "Invoke C# Code: no entry point found. Add public static void Main(Args args) or static void Main().");
        }

        private static MethodInfo FindMainOnType(Type type, Type argsType)
        {
            if (type == null)
            {
                return null;
            }

            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo withArgs = null;
            MethodInfo withoutArgs = null;
            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(method.Name, "Main", StringComparison.Ordinal) || !IsSupportedReturn(method.ReturnType))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == argsType)
                {
                    withArgs = method;
                }
                else if (parameters.Length == 0)
                {
                    withoutArgs = method;
                }
            }

            return withArgs ?? withoutArgs;
        }

        private static bool IsSupportedReturn(Type returnType)
        {
            return returnType == typeof(void)
                || returnType == typeof(int)
                || returnType == typeof(Task)
                || returnType == typeof(Task<int>);
        }

        private static void InvokeMain(MethodInfo main, object argsInstance)
        {
            object[] parameters = main.GetParameters().Length == 0 ? null : new[] { argsInstance };
            try
            {
                object result = main.Invoke(null, parameters);
                WaitIfTask(result);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }

        private static void WaitIfTask(object result)
        {
            var task = result as Task;
            if (task == null)
            {
                return;
            }

            task.GetAwaiter().GetResult();
        }

        private static void FillInputs(
            object argsInstance,
            List<ArgSpec> specs,
            Dictionary<string, Argument> arguments,
            CodeActivityContext context)
        {
            foreach (ArgSpec spec in specs)
            {
                if (spec.Direction == ArgumentDirection.Out)
                {
                    continue;
                }

                Argument argument;
                if (!TryGetArgument(arguments, spec.Key, out argument) || argument == null)
                {
                    continue;
                }

                object value = argument.Get(context);
                spec.Property.SetValue(argsInstance, value, null);
            }
        }

        private static void WriteOutputs(
            object argsInstance,
            List<ArgSpec> specs,
            Dictionary<string, Argument> arguments,
            CodeActivityContext context)
        {
            foreach (ArgSpec spec in specs)
            {
                if (spec.Direction == ArgumentDirection.In)
                {
                    continue;
                }

                Argument argument;
                if (!TryGetArgument(arguments, spec.Key, out argument) || argument == null)
                {
                    continue;
                }

                object value = spec.Property.GetValue(argsInstance, null);
                argument.Set(context, value);
            }
        }

        private static bool TryGetArgument(Dictionary<string, Argument> arguments, string key, out Argument argument)
        {
            argument = null;
            if (arguments == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            return arguments.TryGetValue(key, out argument);
        }

        private static List<ArgSpec> BuildSpecs(Dictionary<string, Argument> arguments)
        {
            var specs = new List<ArgSpec>();
            var used = new HashSet<string>(StringComparer.Ordinal);
            if (arguments == null)
            {
                return specs;
            }

            foreach (KeyValuePair<string, Argument> pair in arguments)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                string identifier = MakeIdentifier(pair.Key);
                string unique = identifier;
                int suffix = 2;
                while (!used.Add(unique))
                {
                    unique = identifier + "_" + suffix.ToString(CultureInfo.InvariantCulture);
                    suffix++;
                }

                Type type = pair.Value.ArgumentType ?? typeof(object);
                specs.Add(new ArgSpec
                {
                    Key = pair.Key,
                    Identifier = unique,
                    Direction = pair.Value.Direction,
                    ClrType = type,
                    TypeName = GetCSharpTypeName(type)
                });
            }

            return specs;
        }

        private static void BindProperties(CachedScript script, List<ArgSpec> specs)
        {
            foreach (ArgSpec spec in specs)
            {
                spec.Property = script.ArgsType.GetProperty(spec.Identifier, BindingFlags.Public | BindingFlags.Instance);
                if (spec.Property == null)
                {
                    throw new InvalidOperationException(
                        "Invoke C# Code: Args property '" + spec.Identifier + "' was not generated.");
                }
            }
        }

        private static string BuildArgsSource(List<ArgSpec> specs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#line 1 \"Args.cs\"");
            sb.AppendLine("using System;");
            sb.AppendLine("public sealed class Args");
            sb.AppendLine("{");
            foreach (ArgSpec spec in specs)
            {
                sb.Append("    public ");
                sb.Append(spec.TypeName);
                sb.Append(" ");
                sb.Append(spec.Identifier);
                sb.AppendLine(" { get; set; }");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BuildCacheKey(string code, List<ArgSpec> specs)
        {
            var sb = new StringBuilder();
            sb.Append(code);
            sb.Append('\n');
            foreach (ArgSpec spec in specs)
            {
                sb.Append(spec.Key);
                sb.Append('|');
                sb.Append(spec.Identifier);
                sb.Append('|');
                sb.Append(spec.Direction);
                sb.Append('|');
                sb.Append(spec.ClrType != null ? spec.ClrType.FullName : "object");
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string FormatCompileErrors(CompilerResults result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Invoke C# Code compile failed:");
            foreach (CompilerError error in result.Errors)
            {
                if (error.IsWarning)
                {
                    continue;
                }

                string file = string.IsNullOrWhiteSpace(error.FileName) ? "Program.cs" : Path.GetFileName(error.FileName);
                if (string.Equals(file, "Args.cs", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("Args.cs(");
                }
                else
                {
                    sb.Append("Program.cs(");
                }

                sb.Append(error.Line);
                sb.Append(",");
                sb.Append(error.Column);
                sb.Append("): ");
                sb.Append(error.ErrorNumber);
                sb.Append(" ");
                sb.AppendLine(error.ErrorText);
            }

            return sb.ToString().TrimEnd();
        }

        private static IEnumerable<string> GetReferenceAssemblies()
        {
            var locations = new List<string>();
            AddReference(locations, typeof(object));
            AddReference(locations, typeof(Uri));
            AddReference(locations, typeof(Enumerable));
            AddReference(locations, typeof(System.Data.DataTable));
            AddReference(locations, typeof(System.Xml.XmlDocument));
            AddReference(locations, typeof(System.Xml.Linq.XDocument));
            AddReference(locations, typeof(System.Net.Http.HttpClient));
            AddReference(locations, typeof(Microsoft.CSharp.RuntimeBinder.Binder));
            AddReference(locations, typeof(Newtonsoft.Json.JsonConvert));
            AddReference(locations, typeof(Log));
            AddReference(locations, typeof(System.Drawing.Bitmap));
            AddReference(locations, typeof(System.Text.RegularExpressions.Regex));
            AddReference(locations, typeof(System.Collections.Generic.HashSet<string>));
            AddReference(locations, typeof(System.Windows.DependencyObject));
            AddReference(locations, typeof(System.Windows.Media.Visual));
            AddNamedReference(locations, "UIAutomationClient, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            AddNamedReference(locations, "UIAutomationTypes, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            AddReferencedAssemblies(locations);
            return locations;
        }

        private static void AddReference(List<string> locations, Type type)
        {
            if (type == null)
            {
                return;
            }

            AddAssembly(locations, type.Assembly);
        }

        private static void AddNamedReference(List<string> locations, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return;
            }

            try
            {
                AddAssembly(locations, Assembly.Load(assemblyName));
            }
            catch
            {
            }
        }

        private static void AddReferencedAssemblies(List<string> locations)
        {
            for (int i = 0; i < locations.Count; i++)
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly.LoadFrom(locations[i]);
                }
                catch
                {
                    continue;
                }

                AssemblyName[] names;
                try
                {
                    names = assembly.GetReferencedAssemblies();
                }
                catch
                {
                    continue;
                }

                foreach (AssemblyName name in names)
                {
                    if (name == null || string.IsNullOrWhiteSpace(name.Name))
                    {
                        continue;
                    }

                    if (string.Equals(name.Name, "System.Numerics.Vectors", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        AddAssembly(locations, Assembly.Load(name));
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void AddAssembly(List<string> locations, Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic)
            {
                return;
            }

            string location;
            try
            {
                location = assembly.Location;
            }
            catch
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(location) ||
                locations.Contains(location, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            locations.Add(location);
        }

        private static string MakeIdentifier(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name.Trim())
            {
                if (sb.Length == 0)
                {
                    if (char.IsLetter(c) || c == '_')
                    {
                        sb.Append(c);
                    }
                    else if (char.IsDigit(c))
                    {
                        sb.Append('_');
                        sb.Append(c);
                    }
                }
                else if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            if (sb.Length == 0)
            {
                return "_arg";
            }

            string identifier = sb.ToString();
            if (Keywords.Contains(identifier))
            {
                return "@" + identifier;
            }

            return identifier;
        }

        private static string GetCSharpTypeName(Type type)
        {
            if (type == null)
            {
                return "object";
            }

            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type == typeof(void))
            {
                return "void";
            }

            if (type.IsArray)
            {
                string commas = new string(',', type.GetArrayRank() - 1);
                return GetCSharpTypeName(type.GetElementType()) + "[" + commas + "]";
            }

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                string fullName = definition.FullName ?? definition.Name;
                int tick = fullName.IndexOf('`');
                if (tick >= 0)
                {
                    fullName = fullName.Substring(0, tick);
                }

                string inner = string.Join(", ", type.GetGenericArguments().Select(GetCSharpTypeName));
                return fullName.Replace('+', '.') + "<" + inner + ">";
            }

            string name = type.FullName ?? type.Name;
            return name.Replace('+', '.');
        }

        private sealed class CachedScript
        {
            public Type ArgsType;
            public MethodInfo Main;
        }

        private sealed class ArgSpec
        {
            public string Key;
            public string Identifier;
            public ArgumentDirection Direction;
            public Type ClrType;
            public string TypeName;
            public PropertyInfo Property;
        }

        private sealed class OutputTextWriter : TextWriter
        {
            private readonly StringBuilder _buffer = new StringBuilder();
            private readonly object _gate = new object();

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void Write(char value)
            {
                lock (_gate)
                {
                    if (value == '\n')
                    {
                        FlushLine();
                        return;
                    }

                    if (value != '\r')
                    {
                        _buffer.Append(value);
                    }
                }
            }

            public override void Write(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return;
                }

                foreach (char c in value)
                {
                    Write(c);
                }
            }

            public override void Flush()
            {
                lock (_gate)
                {
                    if (_buffer.Length > 0)
                    {
                        FlushLine();
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    Flush();
                }

                base.Dispose(disposing);
            }

            private void FlushLine()
            {
                string line = _buffer.ToString();
                _buffer.Clear();
                try
                {
                    Log.Output(line);
                }
                catch
                {
                }
            }
        }
    }
}
