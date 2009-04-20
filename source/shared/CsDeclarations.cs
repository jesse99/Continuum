// Copyright (C) 2009 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Shared
{
	// [assembly: CLSCompliant(false)]
	public sealed class CsAttribute : CsDeclaration
	{
		public CsAttribute(string target, string name, string args, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(target == null || target.Length > 0, "target is empty");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(args != null, "args is null");
			
			Target = target;
			Name = name;
			Arguments = args;
		}
		
		// The name of the object the attribute applies to, e.g. "assembly". 
		// May be null.
		public string Target {get; private set;}
		
		// The attribute type name, e.g. "CLSCompliant".
		public string Name {get; private set;}
		
		// The arguments supplied to the attribute, e.g. "false". These are 
		// not normalized and may be empty but will not be null.
		public string Arguments {get; private set;}
		
		public override string ToString()
		{
			return Name;
		}
	}
	
	// Command, IComparable <MyCommand>, IDisposable
	public sealed class CsBases : CsDeclaration
	{
		public CsBases(int offset, int line) : base(offset, 0, line)
		{
			Names = new string[0];
		}
		
		public CsBases(string[] names, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(names != null, "names is null");
			
			Names = names;
		}
		
		// Returns something like ["Command", "IComparable<MyCommand>", "IDisposable"]. 
		// The names will not contain whitespace. May be empty but will not be null. If there
		// are names Offset will be at the start of the first one and Offset+Length will point to
		// just after the last one, otherwise Offset will point to just before the opening
		// brace and Length will be zero.
		public string[] Names {get; private set;}
		
		public bool HasBaseClass
		{
			get {return Names.Length > 0 && !CsHelpers.IsInterface(Names[0]);}
		}
	}
	
	// { void Foo(); }
	public sealed class CsBody : CsDeclaration
	{
		public CsBody(string name, int length) : base(0, length, 1)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(length >= 0, "length is negative");
			
			Name = name;
			First = 0;
			First = 0;
			Last = length > 0 ? length - 1 : 0;	// handle edge case of empty <globals>
		}
		
		public CsBody(string name, int start, int first, int length, int line) : base(start, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(start < first, "start and first are not ordered properly");
			Contract.Requires(length >= 2, "length is too small");
			
			Name = name;
			Start = start;
			First = first;
			Last = start + length - 1;
		}
		
		// Name of the declaration the body is part of.
		public string Name {get; private set;}
		
		// Offset of the opening brace.
		public int Start {get; private set;}
		
		// First token after the opening brace.
		public int First {get; private set;}
		
		// Offset of the closing brace.
		public int Last {get; private set;}
	}
	
	// public class Foo : Bar {}
	public sealed class CsClass : CsType
	{
		public CsClass(int nameOffset, CsBody body, CsMember[] members, CsType[] types, CsBases bases, string constraints, string gargs, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, body, bases, members, types, attrs, modifiers, constraints, gargs, name, offset, length, line)
		{
		}
	}
	
	// public delegate void Foo<KEY, VALUE>(int x, Dictionary<KEY, VALUE> y)
	public sealed class CsDelegate : CsType
	{
		public CsDelegate(int nameOffset, string constraints, CsParameter[] parms, string gargs, string rtype, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line)
			: base(nameOffset, null, new CsBases(offset, line), new CsMember[0], new CsType[0], attrs, modifiers, constraints, gargs, name, offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			Contract.Requires(parms != null, "parms is null");
			
			ReturnType = rtype.TrimAll();
			Parameters = parms;
		}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string ReturnType {get; private set;}
		
		public CsParameter[] Parameters {get; private set;}
	}
	
	// public enum Greek {alpha, beta, gamma}
	public sealed class CsEnum : CsType
	{
		public CsEnum(string[] names, int nameOffset, string baseType, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line)
			: base(nameOffset, null, new CsBases(offset, line), new CsMember[0], new CsType[0], attrs, modifiers, null, null, name, offset, length, line)
		{
			Contract.Requires(names != null, "names is null");
			Contract.Requires(!string.IsNullOrEmpty(baseType), "baseType is null or empty");
			
			Names = names;
			BaseType = baseType;
		}
		
		// Will be an integral type name, e.g. "int".
		public string BaseType {get; private set;}

		public string[] Names {get; private set;}
	}
	
	// event bool Signaled;
	public sealed class CsEvent : CsMember
	{
		public CsEvent(int nameOffset, string type, string name, CsAttribute[] attrs, MemberModifiers modifiers, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			
			Type = type.TrimAll();
		}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string Type {get; private set;}
		
		public override string FullName
		{
			get
			{
				if (DeclaringType != null)
					return DeclaringType.FullName + "/" + Name;				
				else
					return Name;
			}
		}
	}
	
	// extern alias Foo;
	public sealed class CsExternAlias : CsDeclaration
	{
		public CsExternAlias(string name, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Name = name;
		}
		
		// Will be an identifier, e.g. "Foo".
		public string Name {get; private set;}
		
		public override string ToString()
		{
			return Name;
		}
	}
	
	// public float Pi = 3.14;
	public sealed class CsField : CsMember
	{
		public CsField(int nameOffset, string type, string value, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			Contract.Requires(value == null || value.Length > 0, "value is empty");
			
			Type = type.TrimAll();
			Value = value;
		}
		
		// Will be something like "int" and will not have any whitespace.
		public string Type {get; private set;}
		
		// Will be something like "3.14" and may have white space. May be null, but will not be empty.
		public string Value {get; private set;}
		
		public override string FullName
		{
			get
			{
				return Type + " " + DeclaringType.FullName + "::" + Name;
			}
		}
	}
	
	public sealed class CsGlobalNamespace : CsNamespace
	{
		public CsGlobalNamespace(CsPreprocess[] preprocess, CsBody body, CsAttribute[] attrs, CsExternAlias[] externs, CsUsingAlias[] aliases, CsUsingDirective[] uses, CsNamespace[] namespaces, CsType[] types, int length) : base(body, "<globals>", externs, aliases, uses, namespaces, types, 0, length, 1)
		{
			Contract.Requires(attrs != null, "attrs is null");
			Contract.Requires(preprocess != null, "preprocess is null");
			
			Attributes = attrs;
			Preprocess = preprocess;
		}
		
		// Target will be "assembly" or "module".
		public CsAttribute[] Attributes {get; private set;}
		
		public CsPreprocess[] Preprocess {get; private set;}

		public bool Malformed {get; set;}
	}
	
	// int this[int x] {get; set;}
	public sealed class CsIndexer : CsMember
	{
		public CsIndexer(int nameOffset, CsBody getterBody, CsBody setterBody, MemberModifiers getAccess, MemberModifiers setAccess, string name, CsAttribute[] getAttrs, CsAttribute[] setAttrs, bool hasGet, bool hasSet, CsParameter[] parms, string rtype, CsAttribute[] attrs, MemberModifiers modifiers, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!hasGet || getAttrs != null, "getAttrs is null");
			Contract.Requires(!hasSet || setAttrs != null, "setAttrs is null");
			Contract.Requires(hasGet || hasSet, "not a getter and not a setter");
			Contract.Requires(parms != null, "parms is null");
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			Contract.Requires(((int) getAccess & ~CsMember.AccessMask) == 0, "getAccess has more than just acccess set");
			Contract.Requires(((int) setAccess & ~CsMember.AccessMask) == 0, "setAccess has more than just acccess set");
			
			HasGetter = hasGet;
			HasSetter = hasSet;
			ReturnType = rtype.TrimAll();
			Parameters = parms;
			GetterAttributes = getAttrs;
			SetterAttributes = setAttrs;
			GetterAccess = getAccess;
			SetterAccess = setAccess;
			GetterBody = getterBody;
			SetterBody = setterBody;
		}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string ReturnType {get; private set;}
		
		public CsParameter[] Parameters {get; private set;}
		
		public bool HasGetter {get; private set;}
		
		public bool HasSetter {get; private set;}
		
		// Will be null if the indexer does not have a getter (or has an abstract one).
		public CsAttribute[] GetterAttributes {get; private set;}
		
		// Will be null if the indexer does not have a getter (or has an abstract one).
		public CsBody GetterBody {get; private set;}
		
		// Will be null if the indexer does not have a setter (or has an abstract one).
		public CsBody SetterBody {get; private set;}
		
		// Will be null if the indexer does not have a setter (or has an abstract one).
		public CsAttribute[] SetterAttributes {get; private set;}
		
		// If non-zero then these override the indexer access.
		public MemberModifiers GetterAccess {get; private set;}
		
		public MemberModifiers SetterAccess {get; private set;}
		
		public override string FullName
		{
			get
			{
				var builder = new StringBuilder();
				
				builder.Append(ReturnType);
				builder.Append(' ');
				builder.Append(DeclaringType.FullName);
				builder.Append("::this[");
				AppendParameters(builder, Parameters);
				builder.Append(']');
				
				return builder.ToString();
			}
		}
	}
	
	// public interface IFoo : IBar, IBaz {}
	public sealed class CsInterface : CsType
	{
		public CsInterface(int nameOffset, CsBody body, CsMember[] members, CsType[] types, CsBases bases, string constraints, string gargs, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, body, bases, members, types, attrs, modifiers, constraints, gargs, name, offset, length, line)
		{
		}
	}
	
	[Flags]
	[Serializable]
	public enum MemberModifiers
	{
		None				= 0x0000,
		
		Public			= 0x0001,
		Protected		= 0x0002,
		Internal			= 0x0004,
		Private			= 0x0008,
		
		New				= 0x0010,
		Static			= 0x0020,
		Abstract			= 0x0040,
		Virtual			= 0x0080,
		Override		= 0x0100,
		Sealed			= 0x0200,
		Extern			= 0x0400,
		Partial			= 0x0800,
		
		Readonly		= 0x1000,			// field modifiers
		Volatile			= 0x2000,
		Const			= 0x4000,
		Unsafe			= 0x8000,
	}
	
	// Base class for things which may appear in a class, e.g. CsMethod, CsProperty etc.
	public abstract class CsMember : CsDeclaration
	{
		public static readonly int AccessMask	= 0x000F;			// can't define this in MemberModifiers or it messes up ToString
		
		protected CsMember(int nameOffset, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(attrs != null, "attrs is null");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(nameOffset > offset, "nameOffset is too small");
			Contract.Requires(nameOffset < offset + length, "nameOffset is too large");
			
			Attributes = attrs;
			Modifiers = modifiers;
			Name = name;
			NameOffset = nameOffset;
			
			Contract.Ensures(Access != MemberModifiers.None, "access was not set");
		}
		
		// The type the member is declared within. Will be null for delegates and enums
		// declared in a namespace scope.
		public CsType DeclaringType {get; internal set;}
		
		public CsAttribute[] Attributes {get; private set;}
		
		// Note that this will not return None.
		public MemberModifiers Access
		{
			get {return (MemberModifiers) ((int) Modifiers & CsMember.AccessMask);}
		}
		
		// Note that this will not return None.
		public MemberModifiers Modifiers {get; private set;}
		
		public string Name {get; private set;}
		
		public int NameOffset {get; private set;}
		
		public abstract string FullName {get;}
		
		public override string ToString()
		{
			return Name;
		}
		
		protected void AppendParameters(StringBuilder builder, CsParameter[] parms)
		{
			Contract.Requires(builder != null, "builder is null");
			Contract.Requires(parms != null, "parms is null");
			
			for (int i = 0; i < parms.Length; ++i)
			{
				builder.Append(parms[i].Type);
				builder.Append(' ');
				builder.Append(parms[i].Name);
				
				if (i + 1 < parms.Length)
					builder.Append(", ");
			}
		}
	}
	
	// public bool IsEnabled() {return m_enabled;}
	public sealed class CsMethod : CsMember
	{
		public CsMethod(int nameOffset, CsBody body, bool isCtor, bool isDtor, string constraints, CsParameter[] parms, string gargs, string rtype, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!(isCtor && isDtor), "can't be both a ctor and a dtor");
			Contract.Requires(parms != null, "parms is null");
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			
			Body = body;
			IsConstructor = isCtor;
			IsFinalizer = isDtor;
			ReturnType = rtype.TrimAll();
			Parameters = parms;
			Constraints = constraints;
			
			if (gargs != null)
				GenericArguments = gargs.TrimAll();
		}
		
		public bool IsConstructor {get; private set;}
		
		public bool IsFinalizer {get; private set;}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string ReturnType {get; private set;}
		
		// Will be something like "KEY,VALUE". Note that this will not have any whitespace
		// and will be null if the delegate is not generic.
		public string GenericArguments {get; private set;}
		
		public CsParameter[] Parameters {get; private set;}
		
		// Will be something like "where KEY : new(), where KEY : class". May be null.
		public string Constraints {get; private set;}
		
		// May be null if the method is extern, abstract, declared within an interface, etc.
		public CsBody Body {get; private set;}
		
		public override string FullName
		{
			get
			{
				var builder = new StringBuilder();
				
				builder.Append(ReturnType);
				builder.Append(' ');
				builder.Append(DeclaringType.FullName);
				builder.Append("::");
				builder.Append(Name);
				builder.Append('(');
				AppendParameters(builder, Parameters);
				builder.Append(')');
				
				return builder.ToString();
			}
		}
	}
	
	// namespace CoolLib.Internals {}
	public class CsNamespace : CsTypeScope
	{
		public CsNamespace(CsBody body, string name, CsExternAlias[] externs, CsUsingAlias[] aliases, CsUsingDirective[] uses, CsNamespace[] namespaces, CsType[] types, int offset, int length, int line) : base(body, types, offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(externs != null, "externs is null");
			Contract.Requires(aliases != null, "aliases is null");
			Contract.Requires(uses != null, "uses is null");
			Contract.Requires(namespaces != null, "namespaces is null");
			
			Name = name;
			Externs = externs;
			Aliases = aliases;
			Uses = uses;
			Namespaces = namespaces;
			
			var decs = new List<CsDeclaration>();
			decs.AddRange(namespaces);
			SetDeclarations(decs);
			
			foreach (CsNamespace n in Namespaces)
				n.Namespace = this;
			foreach (CsType t in Types)
				t.Namespace = this;
		}
		
		// Will be a qualified identifier, e.g. "CoolLib.Internals" or "<globals>" for the global namespace.
		// But will not include the outer namespace name (if any).
		public string Name {get; private set;}
				
		// These properties will have the declarations at the top of the namespace, but not any
		// declared in a nested namespace.
		public CsExternAlias[] Externs {get; private set;}
		
		public CsUsingAlias[] Aliases {get; private set;}
		
		public CsUsingDirective[] Uses {get; private set;}
		
		public CsNamespace[] Namespaces {get; private set;}
		
		protected override void SetDeclarations(List<CsDeclaration> decs)
		{
			base.SetDeclarations(decs);
			
			Contract.Assert(Declarations.Length == Namespaces.Length + Classes.Length + Delegates.Length + Enums.Length + Interfaces.Length + Structs.Length, "bad declarations length");
		}
		
		public override string ToString()
		{
			return Name;
		}
	}
	
	// public static bool operator!(Foo rhs) {return !m_value;}
	public sealed class CsOperator : CsMember
	{
		public CsOperator(int nameOffset, CsBody body, bool isImplicit, bool isExplicit, CsParameter[] parms, string rtype, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			
			ReturnType = rtype.TrimAll();
			Parameters = parms;
			IsImplicit = isImplicit;
			IsExplicit = isExplicit;
			Body = body;
		}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string ReturnType {get; private set;}
		
		public CsParameter[] Parameters {get; private set;}
		
		// Returns true if the operator is a conversion operator.
		public bool IsConversion {get {return IsImplicit || IsExplicit;}}
		
		// Returns true if the operator is an implicit conversion operator.
		public bool IsImplicit {get; private set;}
		
		// Returns true if the operator is an explicit conversion operator.
		public bool IsExplicit {get; private set;}
		
		// May be null.
		public CsBody Body {get; private set;}
		
		public override string FullName
		{
			get
			{
				var builder = new StringBuilder();
				
				builder.Append(ReturnType);
				builder.Append(' ');
				builder.Append(DeclaringType.FullName);
				builder.Append("::operator");
				builder.Append(Name);
				builder.Append('(');
				AppendParameters(builder, Parameters);
				builder.Append(')');
				
				return builder.ToString();
			}
		}
	}
	
	[Serializable]
	public enum ParameterModifier {None, Ref, Out, This}
	
	// int width
	public sealed class CsParameter
	{
		public CsParameter(CsAttribute[] attrs, ParameterModifier modifier, bool isParams, string type, string name)
		{
			Contract.Requires(attrs != null, "attrs is null");
			Contract.Requires(!string.IsNullOrEmpty(type), "type is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Attributes = attrs;
			Type = type.TrimAll();
			Modifier = modifier;
			IsParams = isParams;
			Name = name;
		}
		
		public CsAttribute[] Attributes {get; private set;}
		
		// Will be something like "int"  Note that the type will not have any whitespace.
		public string Type {get; private set;}
		
		public ParameterModifier Modifier {get; private set;}
		
		// Returns true if the parameter was decorated with the params keyword.
		public bool IsParams {get; private set;}
		
		// Will be something like "width".
		public string Name {get; private set;}
		
		public override string ToString()
		{
			return Name;
		}
	}
	
	// #region Private methods
	public sealed class CsPreprocess : CsDeclaration
	{
		public CsPreprocess(string name, string text, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(text != null, "text is null");
			
			Name = name;
			Text = text.Trim();
		}
		
		// Will be define, undef, if, elif, else, endif, line, error, warning, region, 
		// endregion, or pragma.
		public string Name {get; private set;}
		
		// Will not be null, but may be empty for directives like #else.  Will not 
		// have leading or trailing spaces but some directives (like #if) may have
		// interior spaces.
		public string Text {get; private set;}
		
		public override string ToString()
		{
			return Name + " " + Text;
		}
	}
	
	// int Age {get; set;}
	public sealed class CsProperty : CsMember
	{
		public CsProperty(int nameOffset, CsBody getterBody, CsBody setterBody, MemberModifiers getAccess, MemberModifiers setAccess, string name, CsAttribute[] getAttrs, CsAttribute[] setAttrs, bool hasGet, bool hasSet, string rtype, CsAttribute[] attrs, MemberModifiers modifiers, int offset, int length, int line) : base(nameOffset, attrs, modifiers, name, offset, length, line)
		{
			Contract.Requires(!hasGet || getAttrs != null, "getAttrs is null");
			Contract.Requires(!hasSet || setAttrs != null, "setAttrs is null");
			Contract.Requires(hasGet || hasSet, "not a getter and not a setter");
			Contract.Requires(!string.IsNullOrEmpty(rtype), "rtype is null or empty");
			Contract.Requires(((int) getAccess & ~CsMember.AccessMask) == 0, "getAccess has more than just acccess set");
			Contract.Requires(((int) setAccess & ~CsMember.AccessMask) == 0, "setAccess has more than just acccess set");
			
			HasGetter = hasGet;
			HasSetter = hasSet;
			ReturnType = rtype.TrimAll();
			GetterAttributes = getAttrs;
			SetterAttributes = setAttrs;
			GetterAccess = getAccess;
			SetterAccess = setAccess;
			GetterBody = getterBody;
			SetterBody = setterBody;
		}
		
		// Will be something like "int" or "Dictionary<KEY,VALUE>". Note that the type will 
		// not have any whitespace.
		public string ReturnType {get; private set;}
		
		public bool HasGetter {get; private set;}
		
		public bool HasSetter {get; private set;}
		
		// Will be null if the property does not have a getter (or has an abstract one).
		public CsAttribute[] GetterAttributes {get; private set;}
		
		// Will be null if the indexer does not have a getter (or has an abstract one).
		public CsBody GetterBody {get; private set;}
		
		// Will be null if the indexer does not have a setter (or has an abstract one).
		public CsBody SetterBody {get; private set;}
		
		// Will be null if the property does not have a setter (or has an abstract one).
		public CsAttribute[] SetterAttributes {get; private set;}
		
		// If non-zero then these override the property access.
		public MemberModifiers GetterAccess {get; private set;}
		
		public MemberModifiers SetterAccess {get; private set;}
		
		public override string FullName
		{
			get
			{
				var builder = new StringBuilder();
				
				builder.Append(ReturnType);
				builder.Append(' ');
				builder.Append(DeclaringType.FullName);
				builder.Append("::");
				builder.Append(Name);
				
				return builder.ToString();
			}
		}
	}
	
	// public struct Foo {}
	public sealed class CsStruct : CsType
	{
		public CsStruct(int nameOffset, CsBody body, CsMember[] members, CsType[] types, CsBases bases, string constraints, string gargs, CsAttribute[] attrs, MemberModifiers modifiers, string name, int offset, int length, int line) : base(nameOffset, body, bases, members, types, attrs, modifiers, constraints, gargs, name, offset, length, line)
		{
		}
	}
	
	// Base class for CsInterface, CsClass, and CsStruct.
	public abstract class CsType : CsTypeScope
	{
		protected CsType(int nameOffset, CsBody body, CsBases bases, CsMember[] members, CsType[] types, CsAttribute[] attrs, MemberModifiers modifiers, string constraints, string gargs, string name, int offset, int length, int line) : base(body, types, offset, length, line)
		{
			Contract.Requires(members != null, "members is null");
			Contract.Requires(types != null, "types is null");
			Contract.Requires(attrs != null, "attrs is null");
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			Contract.Requires(bases != null, "bases is null");
			Contract.Requires(nameOffset > offset, "nameOffsetis too small");
			Contract.Requires(nameOffset < offset + length, "nameOffsetis too large");

			Attributes = attrs;
			Modifiers = modifiers;
			Name = name;
			NameOffset = nameOffset;
			Constraints = constraints;
			Bases = bases;
			
			if (gargs != null)
				GenericArguments = gargs.TrimAll();
			
			Events = (from m in members let e = m as CsEvent where e != null select e).ToArray();
			Fields = (from m in members let e = m as CsField where e != null select e).ToArray();
			Indexers = (from m in members let e = m as CsIndexer where e != null select e).ToArray();
			Methods = (from m in members let e = m as CsMethod where e != null select e).ToArray();
			Operators = (from m in members let e = m as CsOperator where e != null select e).ToArray();
			Properties = (from m in members let e = m as CsProperty where e != null select e).ToArray();
			Contract.Assert(members.Length == Events.Length + Fields.Length + Indexers.Length + Methods.Length + Operators.Length + Properties.Length, "bad members length");
			
			Members = members;
			
			var decs = new List<CsDeclaration>();
			decs.AddRange(Events);
			decs.AddRange(Fields);
			decs.AddRange(Indexers);
			decs.AddRange(Methods);
			decs.AddRange(Operators);
			decs.AddRange(Properties);
			SetDeclarations(decs);
			
			foreach (CsMember m in members)
				m.DeclaringType = this;
			foreach (CsType t in types)
				t.DeclaringType = this;
		}
		
		// The type this type is declared within. Will be null if the type is not a nested type.
		public CsType DeclaringType {get; private set;}
		
		public CsAttribute[] Attributes {get; private set;}
		
		// Note that this will not return None.
		public MemberModifiers Access
		{
			get {return (MemberModifiers) ((int) Modifiers & CsMember.AccessMask);}
		}
		
		// Note that this will not return None.
		public MemberModifiers Modifiers {get; private set;}
		
		public string Name {get; private set;}
		
		public string FullName
		{
			get
			{
				if (DeclaringType != null)
					return DeclaringType.FullName + "/" + Name;
				
				else if (Namespace != null && Namespace.Name != "<globals>")
					return Namespace.Name + "." + Name;
				
				else
					return Name;
			}
		}
		
		public int NameOffset {get; private set;}
		
		// Returns something like ["IBar", "IBaz"]. The names will not contain whitespace.
		// Will not be null.
		public CsBases Bases {get; private set;}
		
		// Will be something like "KEY,VALUE". Note that this will not have any whitespace
		// and will be null if the type is not generic.
		public string GenericArguments {get; private set;}
		
		// Will be something like "where KEY : new(), where KEY : class". May be null.
		public string Constraints {get; private set;}
		
		public CsEvent[] Events {get; private set;}
		
		public CsField[] Fields {get; private set;}
		
		public CsIndexer[] Indexers {get; private set;}
		
		public CsMember[] Members {get; private set;}
		
		// Note that this is only the members declared using the method syntax: not
		// events, or properties, or whatever.
		public CsMethod[] Methods {get; private set;}
		
		public CsOperator[] Operators {get; private set;}
		
		public CsProperty[] Properties {get; private set;}
		
		public override string ToString()
		{
			return Name;
		}
		
		protected override void SetDeclarations(List<CsDeclaration> decs)
		{
			base.SetDeclarations(decs);
			
			Contract.Assert(Declarations.Length == Classes.Length + Delegates.Length + Enums.Length + Events.Length + Fields.Length + Indexers.Length + Interfaces.Length + Methods.Length + Operators.Length + Properties.Length + Structs.Length, "bad declarations length");
		}
	}
	
	// Derived classes are CsNamespace, CsClass, etc.
	public abstract class CsTypeScope : CsDeclaration
	{
		protected CsTypeScope(CsBody body, CsType[] types, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(types != null, "types is null");
			
			Body = body;
			
			Delegates = (from m in types let e = m as CsDelegate where e != null select e).ToArray();
			Enums = (from m in types let e = m as CsEnum where e != null select e).ToArray();
			
			Classes = (from t in types let e = t as CsClass where e != null select e).ToArray();
			Interfaces = (from t in types let e = t as CsInterface where e != null select e).ToArray();
			Structs = (from t in types let e = t as CsStruct where e != null select e).ToArray();
			
			Types = types;
			Contract.Assert(Types.Length == Classes.Length + Interfaces.Length + Structs.Length + Delegates.Length + Enums.Length, "bad types length");
		}
		
		// The namespace this container is declared within. Will be null if it is
		// declared in the global namespace.
		public CsNamespace Namespace {get; internal set;}
		
		// Will be null for the global namespace and delegates.
		public CsBody Body {get; private set;}
		
		public CsClass[] Classes {get; protected set;}
		
		public CsDelegate[] Delegates {get; private set;}
		
		public CsEnum[] Enums {get; private set;}
		
		public CsInterface[] Interfaces {get; private set;}
		
		public CsStruct[] Structs {get; protected set;}
		
		public CsType[] Types {get; private set;}
		
		// These will be the top level declarations: not those declared within nested declarations.
		// Note that these will appear in the order in which they are declared.
		public CsDeclaration[] Declarations {get; protected set;}
		
		protected virtual void SetDeclarations(List<CsDeclaration> decs)
		{
			Contract.Requires(decs != null, "decs is null");
			
			decs.AddRange(Classes);
			decs.AddRange(Delegates);
			decs.AddRange(Enums);
			decs.AddRange(Interfaces);
			decs.AddRange(Structs);
			decs.Sort((lhs, rhs) => lhs.Offset.CompareTo(rhs.Offset));
			
			Declarations = decs.ToArray();
		}
	}
	
	// using Cookie = IntPtr;
	// using OldCollections = System.Collections;
	public sealed class CsUsingAlias : CsDeclaration
	{
		public CsUsingAlias(string alias, string value, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(alias), "alias is null or empty");
			Contract.Requires(!string.IsNullOrEmpty(value), "value is null or empty");
			
			Alias = alias;
			Value = value.TrimAll();
		}
		
		// Will be an identifier, e.g. "Cookie".
		public string Alias {get; private set;}
		
		// Will be a namespace or type, e.g. "IntPtr" or Dictionary<int,string>. Will not have whitespace.
		public string Value {get; private set;}
		
		public override string ToString()
		{
			return Alias;
		}
	} 

	// using namespace System.IO;
	public sealed class CsUsingDirective : CsDeclaration
	{
		public CsUsingDirective(string name, int offset, int length, int line) : base(offset, length, line)
		{
			Contract.Requires(!string.IsNullOrEmpty(name), "name is null or empty");
			
			Namespace = name;
		}
		
		// Will be something like "System" or "System.IO".
		public string Namespace {get; private set;}
		
		public override string ToString()
		{
			return Namespace;
		}
	} 
}
