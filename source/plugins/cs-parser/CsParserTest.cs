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

#if TEST
using CsParser;
using NUnit.Framework;
using Shared;
using System;

[TestFixture]
public sealed class CsParserTest
{	
	[TestFixtureSetUp]
	public void Init()
	{
		AssertListener.Install();
	}
	
	[Test]
	public void UsingDirectives()
	{
		string text = @"using System;
using System.IO;";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		var uses = globals.Uses;
		Assert.AreEqual(2, uses.Length);
		
		CsUsingDirective u = uses[0];
		Assert.AreEqual("System", u.Namespace);
		Assert.AreEqual(1, u.Line);
		
		u = uses[1];
		Assert.AreEqual("System.IO", u.Namespace);
		Assert.AreEqual(2, u.Line);
	}
	
	[Test]
	[ExpectedException(typeof(CsParserException))]
	public void ExtraChars()
	{
		string text = @"using System; xxx";
		
		var parser = new Parser(text);
		Unused.Value = parser.Parse();
	}
		
	[Test]
	public void ExternAlias()
	{
		string text = @"extern alias Foo;	// some comment
extern alias 	/* hmm */ Bar	;
using System.IO;";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		var externs = globals.Externs;
		Assert.AreEqual(2, externs.Length);
		
		CsExternAlias a = externs[0];
		Assert.AreEqual("Foo", a.Name);
		Assert.AreEqual(1, a.Line);
		
		a = externs[1];
		Assert.AreEqual("Bar", a.Name);
		Assert.AreEqual(2, a.Line);

		var uses = globals.Uses;
		Assert.AreEqual(1, uses.Length);
		
		CsUsingDirective u = uses[0];
		Assert.AreEqual("System.IO", u.Namespace);
		Assert.AreEqual(3, u.Line);
	}
	
	[Test]
	public void UsingAlias()
	{
		string text = @"using I = int;
using System.IO;
using F = float;
using OldSchool = System.Collections;
";
		
		var parser = new Parser(text); 
		var globals = parser.Parse();

		var aliases = globals.Aliases;
		Assert.AreEqual(3, aliases.Length);
		
		Assert.AreEqual("I", aliases[0].Alias);
		Assert.AreEqual("int", aliases[0].Value);

		Assert.AreEqual("F", aliases[1].Alias);
		Assert.AreEqual("float", aliases[1].Value);

		Assert.AreEqual("OldSchool", aliases[2].Alias);
		Assert.AreEqual("System.Collections", aliases[2].Value);
				
		var uses = globals.Uses;		
		Assert.AreEqual(1, uses.Length);
		Assert.AreEqual("System.IO", uses[0].Namespace);
	}
	
	[Test]
	public void GlobalAttributes()
	{
		string text = @"
[assembly: AssemblyTitle(""cs-refactors"")]    
[assembly: ComVisible(false)]             
[assembly: PermissionSet(SecurityAction.RequestMinimum, Unrestricted = true)]
[module: Foobar]             
[module: Bar()]             
[module: Nested(foo = min(x, y), bar = 22)]             
[module: Alpha, Beta(), Gamma(x, y)]             
";
		
		var parser = new Parser(text); 
		var globals = parser.Parse();

		var attrs = globals.Attributes;		
		Assert.AreEqual(9, attrs.Length);
		
		Assert.AreEqual("assembly", attrs[0].Target);
		Assert.AreEqual("AssemblyTitle", attrs[0].Name);
		Assert.AreEqual("\"cs-refactors\"", attrs[0].Arguments);
		
		Assert.AreEqual("assembly", attrs[1].Target);
		Assert.AreEqual("ComVisible", attrs[1].Name);
		Assert.AreEqual("false", attrs[1].Arguments);
		
		Assert.AreEqual("assembly", attrs[2].Target);
		Assert.AreEqual("PermissionSet", attrs[2].Name);
		Assert.AreEqual("SecurityAction.RequestMinimum, Unrestricted = true", attrs[2].Arguments);
		
		Assert.AreEqual("module", attrs[3].Target);
		Assert.AreEqual("Foobar", attrs[3].Name);
		Assert.AreEqual(string.Empty, attrs[3].Arguments);
		
		Assert.AreEqual("module", attrs[4].Target);
		Assert.AreEqual("Bar", attrs[4].Name);
		Assert.AreEqual(string.Empty, attrs[4].Arguments);
		
		Assert.AreEqual("Nested", attrs[5].Name);
		Assert.AreEqual("foo = min(x, y), bar = 22", attrs[5].Arguments);

		Assert.AreEqual("Alpha", attrs[6].Name);
		Assert.AreEqual(string.Empty, attrs[6].Arguments);
		
		Assert.AreEqual("Beta", attrs[7].Name);
		Assert.AreEqual(string.Empty, attrs[7].Arguments);
		
		Assert.AreEqual("Gamma", attrs[8].Name);
		Assert.AreEqual("x, y", attrs[8].Arguments);
	}
	
	[Test]
	public void Namespace1()
	{
		string text = @"
namespace Foo.Bar
{
}            
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		var ns = globals.Namespaces;
		Assert.AreEqual(1, ns.Length);
		
		Assert.AreEqual("Foo.Bar", ns[0].Name);
	}
	
	[Test]
	public void Namespace2()
	{
		string text = @"
namespace Foo.Bar
{
	extern alias Foo;
	using F = float;
	using System.Collections;
}            
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		var ns = globals.Namespaces;
		Assert.AreEqual(1, ns.Length);
		Assert.AreEqual("Foo.Bar", ns[0].Name);

		Assert.AreEqual(1, ns[0].Externs.Length);
		Assert.AreEqual("Foo", ns[0].Externs[0].Name);

		Assert.AreEqual(1, ns[0].Aliases.Length);
		Assert.AreEqual("F", ns[0].Aliases[0].Alias);
		Assert.AreEqual("float", ns[0].Aliases[0].Value);

		Assert.AreEqual(1, ns[0].Uses.Length);
		Assert.AreEqual("System.Collections", ns[0].Uses[0].Namespace);
	}
	
	[Test]
	public void Namespace3()
	{
		string text = @"
namespace Foo.Bar
{
	using System.Collections;
	namespace Internal
	{
		using Mono.Posix;
	}
}            
";
		
		var parser = new Parser(text); 
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Namespaces.Length);
		CsNamespace top = globals.Namespaces[0];
		Assert.AreEqual("Foo.Bar", top.Name);

		Assert.AreEqual(1, top.Uses.Length);
		Assert.AreEqual("System.Collections", top.Uses[0].Namespace);


		Assert.AreEqual(1, top.Namespaces.Length);
		CsNamespace nested = top.Namespaces[0];
		Assert.AreEqual("Internal", nested.Name);

		Assert.AreEqual(1, nested.Uses.Length);
		Assert.AreEqual("Mono.Posix", nested.Uses[0].Namespace);
	}

	[Test]
	public void Enum1()
	{
		string text = @"
public enum Greek {alpha, beta, gamma}          
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Enums.Length);
		CsEnum type = globals.Enums[0];
		Assert.AreEqual("Greek", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);
	}

	[Test]
	public void Enum2()
	{
		string text = @"
[Foobar]
[Barbar]
public enum Greek {alpha, beta, gamma}          
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Enums.Length);
		CsEnum type = globals.Enums[0];
		Assert.AreEqual("Greek", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);

		Assert.AreEqual(2, globals.Enums[0].Attributes.Length);
		Assert.AreEqual("Foobar", globals.Enums[0].Attributes[0].Name);
		Assert.AreEqual("Barbar", globals.Enums[0].Attributes[1].Name);
	}

	[Test]
	public void Enum3()
	{
		string text = @"
namespace Mine
{
	[Foobar]
	[Barbar]
	public enum Greek {alpha, beta, gamma}          
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();
		var ns = globals.Namespaces[0];

		Assert.AreEqual(1, ns.Enums.Length);
		CsEnum type = ns.Enums[0];
		Assert.AreEqual("Greek", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);

		Assert.AreEqual(2, ns.Enums[0].Attributes.Length);
		Assert.AreEqual("Foobar", ns.Enums[0].Attributes[0].Name);
		Assert.AreEqual("Barbar", ns.Enums[0].Attributes[1].Name);
	}

	[Test]
	public void Delegate1()
	{
		string text = @"
public delegate void Foo();
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Delegates.Length);
		CsDelegate type = globals.Delegates[0];
		Assert.AreEqual("Foo", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);
		Assert.AreEqual("void", type.ReturnType);
	}

	[Test]
	public void Delegate2()
	{
		string text = @"
public delegate Dictionary<int, string> Foo();
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Delegates.Length);
		CsDelegate type = globals.Delegates[0];
		Assert.AreEqual("Foo", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);
		Assert.AreEqual("Dictionary<int,string>", type.ReturnType);
	}

	[Test]
	public void Delegate3()
	{
		string text = @"
public delegate void Foo<int, string>();
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Delegates.Length);
		CsDelegate type = globals.Delegates[0];
		Assert.AreEqual("Foo", type.Name);
		Assert.AreEqual(MemberModifiers.Public, type.Modifiers);
		Assert.AreEqual("void", type.ReturnType);
		Assert.AreEqual("int,string", type.GenericArguments);
		Assert.AreEqual(0, type.Parameters.Length);
	}

	[Test]
	public void Delegate4()
	{
		string text = @"
delegate void Foo(int alpha, Dictionary < int, string > beta);
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Delegates.Length);
		CsDelegate type = globals.Delegates[0];
		Assert.AreEqual("Foo", type.Name);
		Assert.AreEqual(MemberModifiers.Internal, type.Modifiers);
		Assert.AreEqual("void", type.ReturnType);

		Assert.AreEqual(2, type.Parameters.Length);
		Assert.AreEqual("alpha", type.Parameters[0].Name);
		Assert.AreEqual("int", type.Parameters[0].Type);
		Assert.AreEqual("beta", type.Parameters[1].Name);
		Assert.AreEqual("Dictionary<int,string>", type.Parameters[1].Type);
	}

	[Test]
	public void Delegate5()
	{
		string text = @"
delegate void Foo([Hmm ] int alpha, ref float beta, params int [ ] args);
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Delegates.Length);
		CsDelegate type = globals.Delegates[0];
		Assert.IsNull(type.Constraints);

		Assert.AreEqual(3, type.Parameters.Length);
		Assert.AreEqual("alpha", type.Parameters[0].Name);
		Assert.AreEqual("beta", type.Parameters[1].Name);
		Assert.AreEqual("args", type.Parameters[2].Name);

		Assert.AreEqual("int", type.Parameters[0].Type);
		Assert.AreEqual("float", type.Parameters[1].Type);
		Assert.AreEqual("int[]", type.Parameters[2].Type);

		Assert.AreEqual("Hmm", type.Parameters[0].Attributes[0].Name);
		Assert.AreEqual(ParameterModifier.None, type.Parameters[0].Modifier);
		Assert.AreEqual(ParameterModifier.Ref, type.Parameters[1].Modifier);
		Assert.IsTrue(type.Parameters[2].IsParams);
	}

	[Test]
	public void Delegate6()
	{
		string text = @"
delegate void Foo<KEY>(KEY value) where KEY : class;
delegate void Bar<KEY>(KEY value) where KEY : new() where KEY : class;
delegate void Bar<KEY>(KEY value) where KEY : new(), class;
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(3, globals.Delegates.Length);
		Assert.AreEqual("KEY", globals.Delegates[0].GenericArguments);
		Assert.AreEqual("KEY", globals.Delegates[1].GenericArguments);
		Assert.AreEqual("KEY", globals.Delegates[2].GenericArguments);

		Assert.AreEqual("where KEY : class", globals.Delegates[0].Constraints);
		Assert.AreEqual("where KEY : new() where KEY : class", globals.Delegates[1].Constraints);
		Assert.AreEqual("where KEY : new(), class", globals.Delegates[2].Constraints);
	}

	[Test]
	public void Interface1()
	{
		string text = @"
public interface IFoo
{
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);
		Assert.AreEqual("IFoo", globals.Interfaces[0].Name);
		Assert.AreEqual(MemberModifiers.Public, globals.Interfaces[0].Modifiers);
	}

	[Test]
	public void Interface2()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);
		Assert.AreEqual("IFoo", globals.Interfaces[0].Name);
		Assert.AreEqual(MemberModifiers.Public, globals.Interfaces[0].Modifiers);

		Assert.AreEqual(2, globals.Interfaces[0].Bases.Names.Length);
		Assert.AreEqual("IBar", globals.Interfaces[0].Bases.Names[0]);
		Assert.AreEqual("IBaz", globals.Interfaces[0].Bases.Names[1]);
	}

	[Test]
	public void Indexer1()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
	int this[int x] {get; set;}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);

		Assert.AreEqual(1, globals.Interfaces[0].Indexers.Length);
		CsIndexer indexer = globals.Interfaces[0].Indexers[0];
		
		Assert.AreEqual("int", indexer.ReturnType);
		Assert.IsTrue(indexer.HasGetter);
		Assert.IsTrue(indexer.HasSetter);

		Assert.AreEqual(1, indexer.Parameters.Length);
		Assert.AreEqual("x", indexer.Parameters[0].Name);
		Assert.AreEqual("int", indexer.Parameters[0].Type);
	}

	[Test]
	public void Indexer2()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
	int this[int x] {[Foo] set;}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);

		Assert.AreEqual(1, globals.Interfaces[0].Indexers.Length);
		CsIndexer indexer = globals.Interfaces[0].Indexers[0];
		
		Assert.AreEqual("int", indexer.ReturnType);
		Assert.IsFalse(indexer.HasGetter);
		Assert.IsTrue(indexer.HasSetter);

		Assert.AreEqual("Foo", indexer.SetterAttributes[0].Name);
		Assert.IsNull(indexer.GetterAttributes);
	}

	[Test]
	public void Event1()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
	event bool Foo;
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);

		Assert.AreEqual(1, globals.Interfaces[0].Events.Length);
		CsEvent e = globals.Interfaces[0].Events[0];
		
		Assert.AreEqual("bool", e.Type);
		Assert.AreEqual("Foo", e.Name);
	}

	[Test]
	public void Property1()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
	bool Enabled {get;}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);

		Assert.AreEqual(1, globals.Interfaces[0].Properties.Length);
		CsProperty p = globals.Interfaces[0].Properties[0];
		
		Assert.AreEqual("bool", p.ReturnType);
		Assert.AreEqual("Enabled", p.Name);
		Assert.IsTrue(p.HasGetter);
		Assert.IsFalse(p.HasSetter);
	}

	[Test]
	public void Method1()
	{
		string text = @"
public interface IFoo : IBar, IBaz
{
	bool Enabled(int x, float y);
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Interfaces.Length);

		Assert.AreEqual(1, globals.Interfaces[0].Methods.Length);
		CsMethod m = globals.Interfaces[0].Methods[0];
		Assert.IsFalse(m.IsConstructor);
		
		Assert.AreEqual("bool", m.ReturnType);
		Assert.AreEqual("Enabled", m.Name);

		Assert.AreEqual(2, m.Parameters.Length);
		Assert.AreEqual("x", m.Parameters[0].Name);
		Assert.AreEqual("y", m.Parameters[1].Name);

		Assert.AreEqual("int", m.Parameters[0].Type);
		Assert.AreEqual("float", m.Parameters[1].Type);
	}

	[Test]
	public void Fields()
	{
		string text = @"
public struct Foo
{
	public const float Pi = 3.14;
	public const int Age = 20, Weight = 150;
	private Dictionary<string, int> m_names = new Dictionary<string, int>();
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(3, s.Fields.Length);
		Assert.AreEqual("Pi", s.Fields[0].Name);
		Assert.AreEqual("Age", s.Fields[1].Name);
		Assert.AreEqual("m_names", s.Fields[2].Name);

		Assert.AreEqual(MemberModifiers.Const | MemberModifiers.Public, s.Fields[0].Modifiers);
		Assert.AreEqual(MemberModifiers.Const | MemberModifiers.Public, s.Fields[1].Modifiers);
		Assert.AreEqual(MemberModifiers.Private, s.Fields[2].Modifiers);

		Assert.AreEqual("float", s.Fields[0].Type);
		Assert.AreEqual("int", s.Fields[1].Type);
		Assert.AreEqual("Dictionary<string,int>", s.Fields[2].Type);

		Assert.AreEqual("3.14", s.Fields[0].Value);
		Assert.AreEqual("20, Weight = 150", s.Fields[1].Value);	// multiple declerators aren't handle correctly
		Assert.AreEqual("new Dictionary<string, int>()", s.Fields[2].Value);
	}

	[Test]
	public void Event2()
	{
		string text = @"
public struct Foo
{
	public  event bool OnHide;
	protected new event int OnShow, OnOpen;
	public  event bool OnCustom {add {foo bar} remove {some stuff}}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(4, s.Events.Length);
		Assert.AreEqual("OnHide", s.Events[0].Name);
		Assert.AreEqual("OnShow", s.Events[1].Name);
		Assert.AreEqual("OnOpen", s.Events[2].Name);
		Assert.AreEqual("OnCustom", s.Events[3].Name);

		Assert.AreEqual(MemberModifiers.Public, s.Events[0].Modifiers);
		Assert.AreEqual(MemberModifiers.Protected | MemberModifiers.New, s.Events[1].Modifiers);

		Assert.AreEqual("bool", s.Events[0].Type);
		Assert.AreEqual("int", s.Events[1].Type);
		Assert.AreEqual("int", s.Events[2].Type);
		Assert.AreEqual("bool", s.Events[3].Type);
	}

	[Test]
	public void Indexer3()
	{
		string text = @"
public struct Foo
{
	public int this[int x] {get {blah blah} set {aa {bb} cc}}
	public bool IFoo.this[int x] {set {dddd}}
	protected int this[int x] {get {blah blah} private set {aa {bb} cc}}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(3, s.Indexers.Length);
		Assert.AreEqual("int", s.Indexers[0].ReturnType);
		Assert.AreEqual("bool", s.Indexers[1].ReturnType);
		Assert.AreEqual("int", s.Indexers[2].ReturnType);

		Assert.IsTrue(s.Indexers[0].HasGetter);
		Assert.IsFalse(s.Indexers[1].HasGetter);
		Assert.IsTrue(s.Indexers[2].HasGetter);

		Assert.IsTrue(s.Indexers[0].HasSetter);
		Assert.IsTrue(s.Indexers[1].HasSetter);
		Assert.IsTrue(s.Indexers[2].HasSetter);

		Assert.AreEqual(MemberModifiers.Public, s.Indexers[0].Modifiers);
		Assert.AreEqual(MemberModifiers.Public, s.Indexers[1].Modifiers);
		Assert.AreEqual(MemberModifiers.Protected, s.Indexers[2].Modifiers);

		Assert.AreEqual(MemberModifiers.None, s.Indexers[0].GetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Indexers[1].GetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Indexers[2].GetterAccess);

		Assert.AreEqual(MemberModifiers.None, s.Indexers[0].SetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Indexers[1].SetterAccess);
		Assert.AreEqual(MemberModifiers.Private, s.Indexers[2].SetterAccess);
	}

	[Test]
	public void UnaryOperators()
	{
		string text = @"
public struct Foo
{
	public static bool operator!(Foo rhs) {return !m_value;}
	public extern int operator++(Foo rhs);
	public extern bool operator true(Foo rhs);
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);
		CsStruct s = globals.Structs[0];		

		Assert.AreEqual(3, s.Operators.Length);
		Assert.AreEqual("bool", s.Operators[0].ReturnType);
		Assert.AreEqual("int", s.Operators[1].ReturnType);
		Assert.AreEqual("bool", s.Operators[2].ReturnType);

		Assert.AreEqual("!", s.Operators[0].Name);
		Assert.AreEqual("++", s.Operators[1].Name);
		Assert.AreEqual("true", s.Operators[2].Name);

		Assert.AreEqual(1, s.Operators[0].Parameters.Length);
		Assert.AreEqual("rhs", s.Operators[0].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[0].Parameters[0].Type);

		Assert.AreEqual(1, s.Operators[1].Parameters.Length);
		Assert.AreEqual("rhs", s.Operators[1].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[1].Parameters[0].Type);

		Assert.AreEqual(1, s.Operators[2].Parameters.Length);
		Assert.AreEqual("rhs", s.Operators[2].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[2].Parameters[0].Type);
	}

	[Test]
	public void BinaryOperators()
	{
		string text = @"
public struct Foo
{
	public static bool operator+(Foo lhs, int rhs) {return lhs.m_value + rhs;}
	public extern int operator<<(Foo lhs, Foo rhs);
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);
		CsStruct s = globals.Structs[0];		

		Assert.AreEqual(2, s.Operators.Length);
		Assert.AreEqual("bool", s.Operators[0].ReturnType);
		Assert.AreEqual("int", s.Operators[1].ReturnType);

		Assert.AreEqual("+", s.Operators[0].Name);
		Assert.AreEqual("<<", s.Operators[1].Name);

		Assert.AreEqual(2, s.Operators[0].Parameters.Length);
		Assert.AreEqual("lhs", s.Operators[0].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[0].Parameters[0].Type);
		Assert.AreEqual("rhs", s.Operators[0].Parameters[1].Name);
		Assert.AreEqual("int", s.Operators[0].Parameters[1].Type);

		Assert.AreEqual(2, s.Operators[1].Parameters.Length);
		Assert.AreEqual("lhs", s.Operators[1].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[1].Parameters[0].Type);
		Assert.AreEqual("rhs", s.Operators[1].Parameters[1].Name);
		Assert.AreEqual("Foo", s.Operators[1].Parameters[1].Type);
	}

	[Test]
	public void ConversionOperators()
	{
		string text = @"
public struct Foo
{
	public implicit operator bool(Foo lhs) {return m_value;}
	internal explicit operator int(Foo lhs) {return m_value;}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);
		CsStruct s = globals.Structs[0];		

		Assert.AreEqual(2, s.Operators.Length);
		Assert.AreEqual("bool", s.Operators[0].ReturnType);
		Assert.AreEqual("int", s.Operators[1].ReturnType);
		
		Assert.IsTrue(s.Operators[0].IsImplicit);
		Assert.IsFalse(s.Operators[0].IsExplicit);

		Assert.IsFalse(s.Operators[1].IsImplicit);
		Assert.IsTrue(s.Operators[1].IsExplicit);

		Assert.AreEqual("<conversion>", s.Operators[0].Name);
		Assert.AreEqual("<conversion>", s.Operators[1].Name);

		Assert.AreEqual(1, s.Operators[0].Parameters.Length);
		Assert.AreEqual("lhs", s.Operators[0].Parameters[0].Name);
		Assert.AreEqual("Foo", s.Operators[0].Parameters[0].Type);
	}

	[Test]
	public void Property2()
	{
		string text = @"
public struct Foo
{
	public float Pi {get {return 3.14;}}
	protected static int Age {get; private set;}
	public int Weight {get {return m_value;} set {m_weight = value;}}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(3, s.Properties.Length);
		Assert.AreEqual("Pi", s.Properties[0].Name);
		Assert.AreEqual("Age", s.Properties[1].Name);
		Assert.AreEqual("Weight", s.Properties[2].Name);

		Assert.AreEqual(MemberModifiers.Public, s.Properties[0].Modifiers);
		Assert.AreEqual(MemberModifiers.Protected | MemberModifiers.Static, s.Properties[1].Modifiers);

		Assert.AreEqual("float", s.Properties[0].ReturnType);
		Assert.AreEqual("int", s.Properties[1].ReturnType);
		Assert.AreEqual("int", s.Properties[2].ReturnType);
		
		Assert.IsTrue(s.Properties[0].HasGetter);
		Assert.IsTrue(s.Properties[1].HasGetter);
		Assert.IsTrue(s.Properties[2].HasGetter);
		
		Assert.IsFalse(s.Properties[0].HasSetter);
		Assert.IsTrue(s.Properties[1].HasSetter);
		Assert.IsTrue(s.Properties[2].HasSetter);
		
		Assert.AreEqual(MemberModifiers.None, s.Properties[0].GetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Properties[1].GetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Properties[2].GetterAccess);
		
		Assert.AreEqual(MemberModifiers.None, s.Properties[0].SetterAccess);
		Assert.AreEqual(MemberModifiers.Private, s.Properties[1].SetterAccess);
		Assert.AreEqual(MemberModifiers.None, s.Properties[2].SetterAccess);
	}

	[Test]
	public void NestedTypes()
	{
		string text = @"
public struct Foo
{
	private struct Bar
	{
	}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(1, s.Structs.Length);
		Assert.AreEqual("Bar", s.Structs[0].Name);
	}

	[Test]
	public void Ctors()
	{
		string text = @"
public struct Foo
{
	private Foo() : this(20)
	{
	}

	public static Foo()
	{
	}

	public Foo(int x)
	{
	}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(3, s.Methods.Length);
		Assert.AreEqual("Foo", s.Methods[0].Name);
		Assert.AreEqual("Foo", s.Methods[1].Name);
		Assert.AreEqual("Foo", s.Methods[2].Name);

		Assert.IsTrue(s.Methods[0].IsConstructor);
		Assert.IsTrue(s.Methods[1].IsConstructor);
		Assert.IsTrue(s.Methods[2].IsConstructor);

		Assert.AreEqual(0, s.Methods[0].Parameters.Length);
		Assert.AreEqual(0, s.Methods[1].Parameters.Length);
		Assert.AreEqual(1, s.Methods[2].Parameters.Length);
	}

	[Test]
	public void Methods1()
	{
		string text = @"
public struct Foo
{
	private void Foo(int x, float y)
	{
		Console.WriteLine(x + y);
	}
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Structs.Length);

		CsStruct s = globals.Structs[0];		
		Assert.AreEqual("Foo", s.Name);

		Assert.AreEqual(1, s.Methods.Length);
		Assert.AreEqual("Foo", s.Methods[0].Name);
		Assert.AreEqual("void", s.Methods[0].ReturnType);

		Assert.IsFalse(s.Methods[0].IsConstructor);

		Assert.AreEqual(2, s.Methods[0].Parameters.Length);
		Assert.AreEqual("x", s.Methods[0].Parameters[0].Name);
		Assert.AreEqual("y", s.Methods[0].Parameters[1].Name);
	}

	[Test]
	public void Class1()
	{
		string text = @"
public sealed class Database	: IDisposable
{
	~Database()
	{
		DoDispose(false);
	}
	
	// Opens a connection to an arbitrary database.
	public Database(string path)
	{
		Log.WriteLine(""Database"", ""connecting to database at {0}"", path);
	}
	
	// Used for SQL commands which do not return a table.
	public void Update(string command)
	{
		Trace.Assert(!string.IsNullOrEmpty(command), ""command is null or empty"");
	}
	
	public delegate bool RowCallback(string[] row);

	public string[][] QueryRows(string command)
	{
		return rows.ToArray();
	}
	
	#region P/Invokes
	private delegate Error SelectCallback(
		IntPtr param, 
		int numCols, 
		[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] values, 
		[MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] names);
	
	[Flags]
	private enum OpenFlags : int
	{
		READONLY = 0x00000001,
		READWRITE = 0x00000002,
	}
			
   	[DllImport(""/usr/local/lib/libsqlite3.dylib"")]
    private static extern Error sqlite3_open_v2(string fileName, out IntPtr db, OpenFlags flags, IntPtr module);
    #endregion
    
    #region Fields
    private IntPtr m_database;
    private bool m_disposed;
    #endregion
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Classes.Length);

		// class
		CsClass klass = globals.Classes[0];		
		Assert.AreEqual("Database", klass.Name);
		Assert.AreEqual(1, klass.Bases.Names.Length);
		Assert.AreEqual("IDisposable", klass.Bases.Names[0]);

		// methods
		Assert.AreEqual(5, klass.Methods.Length);
		Assert.AreEqual("~Database", klass.Methods[0].Name);
		Assert.AreEqual("Database", klass.Methods[1].Name);
		Assert.AreEqual("Update", klass.Methods[2].Name);
		Assert.AreEqual("QueryRows", klass.Methods[3].Name);
		Assert.AreEqual("sqlite3_open_v2", klass.Methods[4].Name);

		Assert.AreEqual("void", klass.Methods[0].ReturnType);
		Assert.AreEqual("void", klass.Methods[1].ReturnType);
		Assert.AreEqual("void", klass.Methods[2].ReturnType);
		Assert.AreEqual("string[][]", klass.Methods[3].ReturnType);
		Assert.AreEqual("Error", klass.Methods[4].ReturnType);

		Assert.AreEqual(0, klass.Methods[0].Parameters.Length);
		Assert.AreEqual(1, klass.Methods[1].Parameters.Length);
		Assert.AreEqual(1, klass.Methods[2].Parameters.Length);
		Assert.AreEqual(1, klass.Methods[3].Parameters.Length);
		Assert.AreEqual(4, klass.Methods[4].Parameters.Length);

		Assert.IsFalse(klass.Methods[0].IsConstructor);
		Assert.IsTrue(klass.Methods[1].IsConstructor);
		Assert.IsFalse(klass.Methods[2].IsConstructor);
		Assert.IsFalse(klass.Methods[3].IsConstructor);
		Assert.IsFalse(klass.Methods[4].IsConstructor);

		Assert.IsTrue(klass.Methods[0].IsFinalizer);
		Assert.IsFalse(klass.Methods[1].IsFinalizer);
		Assert.IsFalse(klass.Methods[2].IsFinalizer);
		Assert.IsFalse(klass.Methods[3].IsFinalizer);
		Assert.IsFalse(klass.Methods[4].IsFinalizer);

		// delegates
		Assert.AreEqual(2, klass.Delegates.Length);
		Assert.AreEqual("RowCallback", klass.Delegates[0].Name);
		Assert.AreEqual("SelectCallback", klass.Delegates[1].Name);

		Assert.AreEqual("bool", klass.Delegates[0].ReturnType);
		Assert.AreEqual("Error", klass.Delegates[1].ReturnType);

		Assert.AreEqual(1, klass.Delegates[0].Parameters.Length);
		Assert.AreEqual(4, klass.Delegates[1].Parameters.Length);
		
		// enums
		Assert.AreEqual(1, klass.Enums.Length);
		Assert.AreEqual("OpenFlags", klass.Enums[0].Name);
		Assert.AreEqual("int", klass.Enums[0].BaseType);
		
		// fields
		Assert.AreEqual(2, klass.Fields.Length);
		Assert.AreEqual("m_database", klass.Fields[0].Name);
		Assert.AreEqual("m_disposed", klass.Fields[1].Name);

		Assert.AreEqual("IntPtr", klass.Fields[0].Type);
		Assert.AreEqual("bool", klass.Fields[1].Type);

		Assert.IsNull(klass.Fields[0].Value);
		Assert.IsNull(klass.Fields[1].Value);
	}

	[Test]
	public void NullableType()
	{
		string text = @"
public sealed class Foo
{
    private int? m_weight;
    private int[] m_ages;
    private int?[] m_hmm1;
    private int? [] ?[] m_hmm2;
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();

		Assert.AreEqual(1, globals.Classes.Length);

		CsClass klass = globals.Classes[0];

		Assert.AreEqual(4, klass.Fields.Length);
		Assert.AreEqual("m_weight", klass.Fields[0].Name);
		Assert.AreEqual("m_ages", klass.Fields[1].Name);
		Assert.AreEqual("m_hmm1", klass.Fields[2].Name);
		Assert.AreEqual("m_hmm2", klass.Fields[3].Name);

		Assert.AreEqual("int?", klass.Fields[0].Type);
		Assert.AreEqual("int[]", klass.Fields[1].Type);
		Assert.AreEqual("int?[]", klass.Fields[2].Type);
		Assert.AreEqual("int?[]?[]", klass.Fields[3].Type);
	}

	[Test]
	public void ReadOnlyField()
	{
		string text = @"
public sealed class Foo
{
	internal static readonly Selector Alloc = new Selector(""alloc"");
}
";
		
		var parser = new Parser(text);
		var globals = parser.Parse();
		Assert.AreEqual(1, globals.Classes.Length);

		CsClass klass = globals.Classes[0];

		Assert.AreEqual(1, klass.Fields.Length);
		Assert.AreEqual("Alloc", klass.Fields[0].Name);
		Assert.AreEqual("Selector", klass.Fields[0].Type);
	}
}
#endif	// TEST
