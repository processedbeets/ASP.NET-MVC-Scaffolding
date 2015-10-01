using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Management.Automation;
using System.Reflection;
using System.Text.RegularExpressions;
using EnvDTE;
using EnvDTE80;
using Microsoft.CSharp;
using Microsoft.VisualBasic;

namespace T4Scaffolding.Core.EnvDTE
{
    public static class EnvDTEExtensions
    {
        #region Language info
        // Support C# and VB syntax
        private static readonly Regex _unwrapNullableRegex = new Regex(@"^System.Nullable(<|\(Of )(?<UnderlyingTypeName>.*)(>|\))$");

        private static readonly Dictionary<string, string> _csharpShortenedNames = new Dictionary<string, string> {
            { "System.SByte", "sbyte" },
            { "System.Byte", "byte" },
            { "System.Int16", "short" },
            { "System.UInt16", "ushort" },
            { "System.Int32", "int" },
            { "System.UInt32", "uint" },
            { "System.Int64", "long" },
            { "System.UInt64", "ulong" },
            { "System.Single", "float" },
            { "System.Double", "double" },
            { "System.Boolean", "bool" },
            { "System.Char", "char" },
            { "System.String", "string" },
            { "System.Object", "object" },
            { "System.Decimal", "decimal" },
        };
        private static readonly Dictionary<string, string> _vbShortenedNames = new Dictionary<string, string> {
            { "System.SByte", "SByte" },
            { "System.Byte", "Byte" },
            { "System.Int16", "Short" },
            { "System.UInt16", "UInt16" },
            { "System.Int32", "Integer" },
            { "System.UInt32", "UInt32" },
            { "System.Int64", "Long" },
            { "System.UInt64", "UInt64" },
            { "System.Single", "Single" },
            { "System.Double", "Double" },
            { "System.Boolean", "Boolean" },
            { "System.Char", "Char" },
            { "System.String", "String" },
            { "System.Object", "Object" },
            { "System.Decimal", "Decimal" },
            { "System.DateTime", "Date" }, // Unique to VB
        };

        private static readonly List<Type> _primitiveTypes = new List<Type> {
            typeof (bool),
            typeof (byte),
            typeof (sbyte),
            typeof (short),
            typeof (ushort),
            typeof (int),
            typeof (uint),
            typeof (long),
            typeof (ulong),
            typeof (IntPtr),
            typeof (UIntPtr),
            typeof (char),
            typeof (double),
            typeof (float),
        };

        #endregion

        /// <summary>
        /// Scans all partial classes and all base types to get a list of all the
        /// members exposed by a type
        /// </summary>
        public static CodeElements VisibleMembers(this CodeType element)
        {
            if (element == null) throw new ArgumentNullException("element");

            // If it's a local code element, we may be able to enumerate all its partial classes
            if (element.InfoLocation == vsCMInfoLocation.vsCMInfoLocationProject)
            {
                var codeClass2 = element as CodeClass2;
                if (codeClass2 != null)
                {
                    var partialsCodeElements = new List<CodeElement>();
                    var isFirstPartialClass = true;
                    foreach (var partialClass in codeClass2.PartialClasses.OfType<CodeType>()) {
                        partialsCodeElements.AddRange(partialClass.VisibleMembersIgnorePartials(includeBaseMembers: isFirstPartialClass).OfType<CodeElement>());
                        isFirstPartialClass = false; // Only scan base types once per partial class set, otherwise we'll get duplicates of the bases
                    }
                    return new ConcreteCodeElements(element, partialsCodeElements);
                }
            }

            return element.VisibleMembersIgnorePartials(true);
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents the specified .NET type.
        /// This requires that the CodeTypeRef be attached to a parent code element so we can detect
        /// what language it is written in; otherwise we'll throw an InvalidOperationException.
        /// </summary>
        public static bool IsType<T>(this CodeTypeRef codeTypeRef)
        {
            return codeTypeRef.IsType(typeof (T));
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents the specified .NET type.
        /// This requires that the CodeTypeRef be attached to a parent code element so we can detect
        /// what language it is written in; otherwise we'll throw an InvalidOperationException.
        /// </summary>
        public static bool IsType(this CodeTypeRef codeTypeRef, Type type)
        {
            if (codeTypeRef == null) throw new ArgumentNullException("codeTypeRef");

            return IsType(codeTypeRef.AsFullName, type, GetCodeLanguageFromCodeTypeRef(codeTypeRef));
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents the specified .NET type.
        /// If the CodeTypeRef represents a nullable type, we match the *underlying type* (e.g., 
        /// from int? we match int).
        /// This requires that the CodeTypeRef be attached to a parent code element so we can detect
        /// what language it is written in; otherwise we'll throw an InvalidOperationException.
        /// </summary>
        public static bool UnderlyingTypeIs<T>(this CodeTypeRef codeTypeRef)
        {
            return codeTypeRef.UnderlyingTypeIs(typeof (T));
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents the specified .NET type.
        /// If the CodeTypeRef represents a nullable type, we match the *underlying type* (e.g., 
        /// from int? we match int). Otherwise, we match the CodeTypeRef itself.
        /// This requires that the CodeTypeRef be attached to a parent code element so we can detect
        /// what language it is written in; otherwise we'll throw an InvalidOperationException.
        /// </summary>
        public static bool UnderlyingTypeIs(this CodeTypeRef codeTypeRef, Type type)
        {
            var typeUnderlyingNullable = ExtractNullableTypeParameterValue(codeTypeRef);
            if (typeUnderlyingNullable != null) {
                return IsType(typeUnderlyingNullable, type, GetCodeLanguageFromCodeTypeRef(codeTypeRef));
            }
            return IsType(codeTypeRef, type);
        }

        /// <summary>
        /// Determines whether the supplied CodeType represents a .NET type that inherits
        /// from, or is, T. Note that this does not account for language-specific type 
        /// representations, so will not be accurate for types with language-specific
        /// short names, such as List(Of Integer)
        /// </summary>
        public static bool IsAssignableTo<T>(this CodeType codeType)
        {
            return codeType.IsAssignableTo(typeof (T));
        }

        /// <summary>
        /// Determines whether the supplied CodeType represents a .NET type that inherits
        /// from, or is, type. Note that this does not account for language-specific type
        /// representations, so will not be accurate for types with language-specific
        /// short names, such as List(Of Integer)
        /// </summary>
        public static bool IsAssignableTo(this CodeType codeType, Type type)
        {
            return codeType.IsAssignableTo(type.FullName);
        }

        /// <summary>
        /// Determines whether the supplied CodeType represents a .NET type that inherits
        /// from, or is, the specified type. Note that this does not account for language-specific  
        /// type representations, so will not be accurate for types with language-specific
        /// short names, such as List(Of Integer)
        /// </summary>
        public static bool IsAssignableTo(this CodeType codeType, string fullTypeName)
        {
            if (codeType == null)
                return false;

            if (codeType.FullName == fullTypeName)
                return true;

            // Recurse for every immediate base type, even though in practice there will only ever
            // be at most one immediate base type, as neither C# nor VB supports multiple inheritance
            return codeType.Bases.OfType<CodeType>().Any(baseType => baseType.IsAssignableTo(fullTypeName));
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents a primitive .NET type, e.g.,
        /// byte, bool, float, etc.
        /// </summary>
        public static bool IsPrimitive(this CodeTypeRef codeTypeRef)
        {
            // A possible optimization would be checking codeTypeRef.TypeKind for known primitive
            // types, e.g., vsCMTypeRef.vsCMTypeRefDouble. Consider adding this logic in future.
            return _primitiveTypes.Any(primitiveType => codeTypeRef.IsType(primitiveType));
        }

        /// <summary>
        /// Determines whether the supplied CodeTypeRef represents a primitive .NET type, e.g.,
        /// byte, bool, float, etc. 
        /// If the CodeTypeRef represents a nullable type, we consider the *underlying type* (e.g., 
        /// from int? we consider int. Otherwise, we consider the CodeTypeRef itself.
        /// </summary>
        public static bool UnderlyingIsPrimitive(this CodeTypeRef codeTypeRef)
        {
            var typeUnderlyingNullable = ExtractNullableTypeParameterValue(codeTypeRef);
            if (typeUnderlyingNullable != null) {
                var language = GetCodeLanguageFromCodeTypeRef(codeTypeRef);
                return _primitiveTypes.Any(primitiveType => IsType(typeUnderlyingNullable, primitiveType, language));
            }
            return IsPrimitive(codeTypeRef);
        }

        /// <summary>
        /// Determines whether the supplied codeProperty is publicly readable
        /// </summary>
        public static bool IsReadable(this CodeProperty codeProperty)
        {
            if (codeProperty == null) throw new ArgumentNullException("codeProperty");
            var codeProperty2 = codeProperty as CodeProperty2;
            if (codeProperty2 != null) {
                return (codeProperty2.ReadWrite != vsCMPropertyKind.vsCMPropertyKindWriteOnly)
                       && ((codeProperty2.Getter == null /* auto property */) || ((CodeElement)codeProperty2.Getter).IsPublic());
            }
            return false;
        }

        /// <summary>
        /// Determines whether the supplied codeProperty is publicly writeable
        /// </summary>
        public static bool IsWriteable(this CodeProperty codeProperty)
        {
            if (codeProperty == null) throw new ArgumentNullException("codeProperty");
            var codeProperty2 = codeProperty as CodeProperty2;
            if (codeProperty2 != null) {
                return (codeProperty2.ReadWrite != vsCMPropertyKind.vsCMPropertyKindReadOnly)
                       && ((codeProperty2.Setter == null /* auto property */) || ((CodeElement)codeProperty2.Setter).IsPublic());
            }
            return false;
        }

        /// <summary>
        /// Determines whether the supplied codeProperty requires you to pass one or more index
        /// arguments to read or write its value.
        /// </summary>
        public static bool HasIndexParameters(this CodeProperty codeProperty)
        {
            var codeProperty2 = codeProperty as CodeProperty2;
            if (codeProperty2 != null) {
                return (codeProperty2.Parameters != null) && (codeProperty2.Parameters.Count > 0);
            }
            return false;
        }

        /// <summary>
        /// Determines which property, if any, is nominated as the "display column" for
        /// the supplied type by looking for a [DisplayColumn] attribute
        /// </summary>
        public static CodeProperty DisplayColumnProperty(this CodeType codeType)
        {
            try {
                var attributes = codeType.Attributes.OfType<CodeAttribute2>();
                var displayColumnAttribute = attributes.FirstOrDefault(x => x.FullName == typeof (DisplayColumnAttribute).FullName);
                if ((displayColumnAttribute == null) || (displayColumnAttribute.Value == null))
                    return null;
                var firstArgValue = displayColumnAttribute.Value.Split(',').FirstOrDefault();
                if (string.IsNullOrEmpty(firstArgValue))
                    return null;
                var matchingProperty = codeType.VisibleMembers().OfType<CodeProperty>().FirstOrDefault(x => "\"" + x.Name + "\"" == firstArgValue);
                return matchingProperty;
            } catch(NotImplementedException) {
                // DTE may throw this when you query interface types for attributes
                return null;
            }
        }

        /// <summary>
        /// Finds a visible property matching any one of a set of possible names. The names are considered in
        /// priority order, with earlier entries in the array taking higher priority.
        /// </summary>
        public static CodeProperty FindProperty(this CodeType codeType, params string[] namesToMatch)
        {
            var candidateProperties = codeType.VisibleMembers().OfType<CodeProperty>().ToList();
            var comparison = VsConstants.VbCodeModelLanguageGuid.Equals(codeType.Language, StringComparison.OrdinalIgnoreCase)
                                 ? StringComparison.OrdinalIgnoreCase // VB
                                 : StringComparison.Ordinal;          // C#
            foreach (var name in namesToMatch) {
                var matchingProperty = candidateProperties.FirstOrDefault(x => string.Equals(x.Name, name, comparison));
                if (matchingProperty != null)
                    return matchingProperty;
            }
            return null;
        }

        /// <summary>
        /// Adds a member to a type using the supplied source code, which should be valid code in the target project's language.
        /// </summary>
        /// <param name="codeType">The class or interface to which the member will be added</param>
        /// <param name="sourceCode">The source code of the member to be added</param>
        /// <param name="position">The position in the class where the source code should be added. See documentation for EnvDTE.CodeModel.AddVariable for allowed values. 
        /// If the position is not specified, the new member will be added to the end of the class.</param>
        /// <param name="replaceTextOptions">Text formatting options. See documentation for EnvDTE.CodeModel.AddVariable for allowed values.</param>
        public static void AddMemberFromSourceCode(this CodeType codeType, string sourceCode, object position = null, vsEPReplaceTextOptions replaceTextOptions = vsEPReplaceTextOptions.vsEPReplaceTextAutoformat)
        {
            if (codeType == null) throw new ArgumentNullException("codeClass");

            CodeElement temporaryElement;
            if (codeType is CodeClass) {
                temporaryElement = (CodeElement)((CodeClass)codeType).AddVariable("temporaryVariable", "System.Object", Position: position ?? -1 /* Add to end of class by default */);
            } else if (codeType is CodeInterface) {
                temporaryElement = (CodeElement)((CodeInterface)codeType).AddFunction("temporaryFunction", vsCMFunction.vsCMFunctionFunction, "System.Object", Position: position ?? -1 /* Add to end of class by default */);
            } else {
                throw new ArgumentException("Parameter value must be an instance of EnvDTE.CodeClass or EnvDTE.CodeInterface", "codeType");
            }
            
            temporaryElement.ReplaceWithSourceCode(sourceCode, replaceTextOptions);
        }

        /// <summary>
        /// Replaces a code element with the supplied source code, which should be valid code in the target project's language.
        /// </summary>
        public static void ReplaceWithSourceCode(this CodeElement codeElement, string sourceCode, vsEPReplaceTextOptions replaceTextOptions = vsEPReplaceTextOptions.vsEPReplaceTextAutoformat)
        {
            if (codeElement == null) throw new ArgumentNullException("codeElement");
            var startPoint = codeElement.GetStartPoint();
            startPoint.CreateEditPoint().ReplaceText(codeElement.GetEndPoint(), IndentAllButFirstLine(sourceCode ?? string.Empty, startPoint.LineCharOffset - 1), (int)replaceTextOptions);
        }

        /// <summary>
        /// Provides a way to invoke an arbitrary MethodInfo from PowerShell code, automatically
        /// converting any supplied parameters to the type required by the target method. This is
        /// needed when passing COM objects from PowerShell code, because if you attempt to call
        /// the method directly, the runtime will be unable to convert the PowerShell COM object
        /// wrapper into the .NET code.
        /// </summary>
        public static object PowerShellInvoke(MethodInfo methodInfo, object target, object[] args)
        {
            if (args != null) {
                for(var i = 0; i < args.Length; i++) {
                    if (args[i] is PSObject)
                        args[i] = ((PSObject)args[i]).BaseObject;
                }
            }
            return methodInfo.Invoke(target, args);
        }

        private static string IndentAllButFirstLine(string value, int charsToIndent)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (charsToIndent == 0)
                return value;

            var lineSeparator = Environment.NewLine + new string(' ', charsToIndent);
            var lines = value.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            return string.Join(lineSeparator, lines);
        }

        private static string GetCodeLanguageFromCodeTypeRef(CodeTypeRef codeTypeRef)
        {
            var parentElement = codeTypeRef.Parent as CodeElement;
            if (parentElement == null)
                throw new InvalidOperationException("Cannot determine code language for supplied codeTypeRef because it is not associated with any parent of type CodeElement");
            return parentElement.Language;
        }

        /// <summary>
        /// Detemines whether the specific type name represents a given .NET type within the context of a given
        /// language (only C# and VB are supported). For example, List(Of System.Int32) and List(Of Integer)
        /// within VB both represent the .NET type List``1[System.Int32]
        /// </summary>
        private static bool IsType(string languageSpecificLongTypeName, Type type, string languageGuid)
        {
            // Normalise both type names into language-specific ones with short type names (e.g, System.Collections.Generic.List<int>)
            // since that's the only direction we can go without manually parsing language-specific type names into CLR-style ones
            CodeDomProvider codeDomProvider = string.Equals(languageGuid, VsConstants.VbCodeModelLanguageGuid, StringComparison.OrdinalIgnoreCase)
                                                  ? (CodeDomProvider)new VBCodeProvider()
                                                  : new CSharpCodeProvider();
            var languageSpecificType = codeDomProvider.GetTypeOutput(new CodeTypeReference(type));
            
            var codeTypeRefLanguageSpecificType = ShortenLanguageSpecificTypeName(languageSpecificLongTypeName, languageGuid);

            return string.Equals(codeTypeRefLanguageSpecificType, languageSpecificType, StringComparison.Ordinal);
        }

        /// <summary>
        /// Converts type names containing long primitive names, e.g., List(Of System.Int32), to language-specific short ones, e.g., List(Of Integer)
        /// </summary>
        private static string ShortenLanguageSpecificTypeName(string typeName, string languageGuid)
        {
            var shortenedNames = string.Equals(languageGuid, VsConstants.VbCodeModelLanguageGuid, StringComparison.OrdinalIgnoreCase)
                                     ? _vbShortenedNames
                                     : _csharpShortenedNames;

            return Regex.Replace(typeName, @"[a-z_][a-z0-9_]*(\.[a-z_][a-z0-9_]*)*", match => {
                string shortName;
                return shortenedNames.TryGetValue(match.Value, out shortName) ? shortName : match.Value;
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// If your supplied codeTypeRef represents a nullable type in either C# or VB, this method returns a string representation
        /// of the underlying type - a language-specific representation with unshortened primitive names (e.g., MyStruct(Of System.Int32))
        /// If your supplied codeTypeRef does not represent a nullable type in these languages, this method returns null.
        /// 
        /// It would be preferable to return an actual CodeTypeRef representing the underlying type, but the VB CodeModel API doesn't
        /// allow us to construct arbitrary CodeTypeRefs: CodeModel.CreateCodeTypeRef("MyStruct(Of System.Int32)") returns a codeTypeRef
        /// with AsFullName = "MyStruct(Of T)" - i.e., it loses generic type information. To avoid the information loss we are forced
        /// to return a string represenation only.
        /// </summary>
        private static string ExtractNullableTypeParameterValue(CodeTypeRef codeTypeRef)
        {
            var codeTypeRef2 = codeTypeRef as CodeTypeRef2;
            if ((codeTypeRef2 != null) && (codeTypeRef2.IsGeneric))
            {
                // CodeTypeRef does not expose generic type args directly, hence falling back on Regex match
                var match = _unwrapNullableRegex.Match(codeTypeRef.AsFullName);
                if (match.Success)
                    return match.Groups["UnderlyingTypeName"].Value;
            }

            return null;
        }

        private static CodeElements VisibleMembersIgnorePartials(this CodeType element, bool includeBaseMembers)
        {
            var baseMembers = includeBaseMembers
                                  ? element.Bases.OfType<CodeType>().SelectMany(x => x.VisibleMembers().OfType<CodeElement>())
                                  : Enumerable.Empty<CodeElement>();

            var nameComparer = string.Equals(element.Language, VsConstants.VbCodeModelLanguageGuid, StringComparison.OrdinalIgnoreCase)
                                   ? StringComparer.OrdinalIgnoreCase // VB has case-insensitive identifiers
                                   : StringComparer.Ordinal;          // Supported other languages (C#) don't

            // Ensure we only have one entry per name from all the bases
            var memberDict = new Dictionary<string, CodeElement>(nameComparer);
            foreach (var baseMember in baseMembers) {
                memberDict[baseMember.Name] = baseMember;
            }

            // Now overlay members at this level
            foreach (var codeElement in element.Members.OfType<CodeElement>()) {
                if (codeElement.IsPublic())
                    memberDict[codeElement.Name] = codeElement;
            }

            return new ConcreteCodeElements(element, memberDict.Values);
        }

        public static bool IsPublic(this CodeElement codeElement)
        {
            if (codeElement is CodeType)
                return ((CodeType)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            if (codeElement is CodeProperty)
                return ((CodeProperty)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            if (codeElement is CodeFunction)
                return ((CodeFunction)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            if (codeElement is CodeVariable)
                return ((CodeVariable)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            if (codeElement is CodeStruct)
                return ((CodeStruct)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            if (codeElement is CodeDelegate)
                return ((CodeDelegate)codeElement).Access == vsCMAccess.vsCMAccessPublic;
            return false;
        }

        private class ConcreteCodeElements : MarshalByRefObject, CodeElements
        {
            private readonly CodeType _primaryCodeType;
            private readonly IList<CodeElement> _codeElements;

            public ConcreteCodeElements(CodeType primaryCodeType, IEnumerable<CodeElement> codeElements)
            {
                _primaryCodeType = primaryCodeType;
                _codeElements = codeElements.ToList();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _codeElements.GetEnumerator();
            }

            public CodeElement Item(object index)
            {
                if (index is int)
                {
                    return _codeElements[(int)index];
                }
                throw new ArgumentException();
            }

            public void Reserved1(object element)
            {
                throw new NotImplementedException();
            }

            public bool CreateUniqueID(string prefix, ref string newName)
            {
                throw new NotImplementedException();
            }

            public DTE DTE
            {
                get { return _primaryCodeType.DTE; }
            }

            public object Parent
            {
                get { return _primaryCodeType; }
            }

            public int Count
            {
                get { return _codeElements.Count; }
            }

            IEnumerator CodeElements.GetEnumerator()
            {
                return _codeElements.GetEnumerator();
            }
        }
    }
}
