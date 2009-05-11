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
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CsRefactor.Script
{
	// Test all of the built-in methods.
	[TestFixture]
	public sealed class EvaluateMethodsTest
	{
		private string DoParse(string refactor, string cs, int offset)
		{
			StringWriter writer = new StringWriter();
			ScriptType.Instance.SetWriter(writer);
			
			CsGlobalNamespace globals = new CsParser.Parser().Parse(cs);
			Script script = new Parser(refactor).Parse();
			script.Evaluate(new Context(script, globals, cs, offset, 0));
			
			return writer.ToString();
		}
			
		[Test]
		public void AttributeMethods()
		{
			string source = @"
[assembly: foo]
internal sealed class MyClass
{
	[bar(22, false)]
	public int Process(int x)
	{
		return x + x;
	}
}
	";
			
			string script = @"
define property EnableTracing
	return false
end

define Run()
	for attr in Globals.Attributes do
		WriteLine(""Name = "" + attr.Name)
		WriteLine(""Args = "" + attr.Arguments)
		WriteLine(""Target = "" + attr.Target)
	end
	
	let method = Globals.Classes.Head.Methods.Head in
		for attr in method.Attributes do
			WriteLine(""Name = "" + attr.Name)
			WriteLine(""Args = "" + attr.Arguments)
			WriteLine(""Target = "" + attr.Target)
		end
	end
end
	";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"Name = foo
Args = 
Target = assembly
Name = bar
Args = 22, false
Target = null
", result);
		}
		
		[Test]
		public void BooleanMethods()
		{
			string source = @"
[assembly: foo]
internal sealed class MyClass : IFoo, IBar
{
	public int Process(int x)
	{
		return x + x;
	}
}
	";
			
			string script = @"
define Run()
	let method = Globals.Classes.Head.Methods.Head in
		WriteLine(""and = "" + (Globals.Attributes.IsEmpty and method.Attributes.IsEmpty))
		WriteLine(""or = "" + (Globals.Attributes.IsEmpty or method.Attributes.IsEmpty))
		WriteLine(""not = "" + (not (Globals.Attributes is String)))

		WriteLine(""is1 = "" + (Globals.Attributes is Sequence))
		WriteLine(""is2 = "" + (Globals.Attributes is Object))
		WriteLine(""is3 = "" + (Globals.Attributes is String))

		WriteLine(""contains = "" + Globals.Classes.Head.Bases.Contains(""IBar""))
	end
end
	";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"and = false
or = true
not = true
is1 = true
is2 = true
is3 = false
contains = true
", result);
		}
		
		[Test]
		public void StringMethods()
		{
			string source = @"
[assembly: foo]
internal sealed class MyClass : IFoo, IBar
{
	public int Process(int x)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	let klass = Globals.Classes.Head, method = klass.Methods.Head in
		WriteLine(""Contains = "" + klass.Bases.Head.Contains(""Foo""))
		WriteLine(""EndsWith = "" + klass.Bases.Head.EndsWith(""Foo""))
		WriteLine(""StartsWith = "" + klass.Bases.Head.StartsWith(""Foo""))
		WriteLine(""Replace = "" + klass.Bases.Head.Replace(""o"", ""xxx""))
		WriteLine(""IsEmpty = "" + klass.Bases.Head.IsEmpty)
		WriteLine(""eq = "" + (klass.Bases.Head == ""IFoo""))
		WriteLine(""ne = "" + (klass.Bases.Head != ""IFoo""))
	end
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"Contains = true
EndsWith = true
StartsWith = false
Replace = IFxxxxxx
IsEmpty = false
eq = true
ne = false
", result);
		}
		
		[Test]
		public void MemberMethods()
		{
			string source = @"
[assembly: foo]
internal sealed class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	let klass = Globals.Classes.Head, method = klass.Methods.Head in
		WriteLine(""DeclaringType = "" + method.DeclaringType.Name)
		WriteLine(""Access = "" + method.Access)
		WriteLine(""Attributes = "" + method.Attributes)
		WriteLine(""Modifiers = "" + method.Modifiers)
		WriteLine(""Name = "" + method.Name)
	end
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"DeclaringType = MyClass
Access = public
Attributes = []
Modifiers = public, static
Name = Process
", result);
		}
		
		[Test]
		public void TypeScopeMethods()
		{
			string source = @"
[assembly: foo]
internal sealed class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	let klass = Globals.Classes.Head in
		WriteLine("""")
		WriteLine(""Declarations = "" + klass.Declarations)
		WriteLine(""Delegates = "" + klass.Delegates)
		WriteLine(""Enums = "" + klass.Enums)
		WriteLine(""Interfaces = "" + klass.Interfaces)
		WriteLine(""Namespace = "" + klass.Namespace)
		WriteLine(""Structs = "" + klass.Structs)
		WriteLine(""Types = "" + Globals.Types)
	end
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"
Declarations = [Process]
Delegates = []
Enums = []
Interfaces = []
Namespace = <globals>
Structs = []
Types = [MyClass]
", result);
		}
		
		[Test]
		public void NamespaceMethods1()
		{
			string source = @"
using System;
using System.IO;

[assembly: foo]
internal sealed class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	WriteLine("""")
	WriteLine(""Aliases = "" + Globals.Aliases)
	WriteLine(""Externs = "" + Globals.Externs)
	WriteLine(""Name = "" + Globals.Name)
	WriteLine(""Namespaces = "" + Globals.Namespaces)
	WriteLine(""Uses = "" + Globals.Uses)
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"
Aliases = []
Externs = []
Name = <globals>
Namespaces = []
Uses = [System, System.IO]
", result);
		}
	
		[Test]
		public void NamespaceMethods2()
		{
			string source = @"
using System;
using SS = System;
using Ptr1 = System.IntPtr;
using Ptr2 = IntPtr;

internal sealed class MyClass
{
	private IntPtr m_1;
	private System.IntPtr m_2;
	private SS.IntPtr m_3;
	private Ptr1 m_4;
	private Ptr2 m_5;
}
";
			
			string script = @"
define Run()
	WriteLine("""")
	let fields = Globals.Classes.Head.Fields in
		WriteLine(""is1 = "" + Globals.TypeMatches(fields.First.Type, ""System.IntPtr""))
		WriteLine(""is1b = "" + Globals.TypeMatches(fields.First.Type, ""System.UIntPtr""))
		WriteLine(""is2 = "" + Globals.TypeMatches(fields.Second.Type, ""System.IntPtr""))
		WriteLine(""is3 = "" + Globals.TypeMatches(fields.Third.Type, ""System.IntPtr""))
		WriteLine(""is4 = "" + Globals.TypeMatches(fields.Fourth.Type, ""System.IntPtr""))
		WriteLine(""is5 = "" + Globals.TypeMatches(fields.Fifth.Type, ""System.IntPtr""))
		WriteLine(""is5b = "" + Globals.TypeMatches(fields.Fifth.Type, ""Ptr""))
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
is1 = true
is1b = false
is2 = true
is3 = true
is4 = true
is5 = true
is5b = false
", result);
		}
	
		[Test]
		public void TypeDeclarationMethods()
		{
			string source = @"
[assembly: foo]
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
	
	private string m_name;
}
";
			
			string script = @"
define Run()
	let klass = Globals.Classes.Head in
		WriteLine("""")
		WriteLine(""Access = "" + klass.Access)
		WriteLine(""Attributes = "" + klass.Attributes)
		WriteLine(""Bases = "" + klass.Bases)
		WriteLine(""Constraints = "" + klass.Constraints)
		WriteLine(""DeclaringType = "" + klass.DeclaringType)
		WriteLine(""Events = "" + klass.Events)
		WriteLine(""Fields = "" + klass.Fields)
		WriteLine(""GenericArguments = "" + klass.GenericArguments)
		WriteLine(""Indexers = "" + klass.Indexers)
		WriteLine(""IsPartial = "" + klass.IsPartial)
		WriteLine(""Methods = "" + klass.Methods)
		WriteLine(""Modifiers = "" + klass.Modifiers)
		WriteLine(""Name = "" + klass.Name)
		WriteLine(""Operators = "" + klass.Operators)
		WriteLine(""Properties = "" + klass.Properties)
	end
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"
Access = public
Attributes = []
Bases = [IFoo, IBar]
Constraints = null
DeclaringType = null
Events = []
Fields = [m_name]
GenericArguments = null
Indexers = []
IsPartial = true
Methods = [Process]
Modifiers = public, abstract, partial
Name = MyClass
Operators = []
Properties = []
", result);
	}
	
		[Test]
		public void DelegateMethods()
		{
			string source = @"
public delegate int Process<T, U>(T x, U y) where T : new();
";
		
		string script = @"
define Run()
	let delegate = Globals.Delegates.Head in
		WriteLine("""")
		WriteLine(""Constraints = "" + delegate.Constraints)
		WriteLine(""GenericArguments = "" + delegate.GenericArguments)
		WriteLine(""Parameters = "" + delegate.Parameters)
		WriteLine(""ReturnType = "" + delegate.ReturnType)
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Constraints = where T : new()
GenericArguments = T,U
Parameters = [x, y]
ReturnType = int
", result);
		}
		
		[Test]
		public void EnumMethods()
		{
			string source = @"
public enum Colors {red, green, blue}
";
			
			string script = @"
define Run()
	let enum = Globals.Enums.Head in
		WriteLine("""")
		WriteLine(""BaseType = "" + enum.BaseType)
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
BaseType = int
", result);
		}
	
		[Test]
		public void FieldMethods()
		{
			string source = @"
[assembly: foo]
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
	
	private string m_name;
}
";
			
			string script = @"
define Run()
	let klass = Globals.Classes.Head in
		WriteLine("""")
		WriteLine(""Field Type = "" + klass.Fields.Head.Type)
		WriteLine(""Field Value = "" + klass.Fields.Head.Value)
	end
end
";
			string result = DoParse(script, source, source.IndexOf("return"));
			Assert.AreEqual(@"
Field Type = string
Field Value = null
", result);
		}
	
		[Test]
		public void GlobalNamespaceMethods()
		{
			string source = @"
[assembly: foo]
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
	
	private string m_name;
}
";
			
			string script = @"
define Run()
	WriteLine("""")
	WriteLine(""Attributes = "" + Globals.Attributes)
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Attributes = [foo]
", result);
		}
	
		[Test]
		public void IndexerMethods()
		{
			string source = @"
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}

	public int this[short i]
	{
		get {return i;}
	}
}
";
		
			string script = @"
define Run()
	let index = Globals.Classes.Head.Indexers.Head in
		WriteLine("""")
		WriteLine(""GetterAccess = "" + index.GetterAccess)
		WriteLine(""GetterAttributes = "" + index.GetterAttributes)
		WriteLine(""HasGetter = "" + index.HasGetter)
		WriteLine(""HasSetter = "" + index.HasSetter)
		WriteLine(""Parameters = "" + index.Parameters)
		WriteLine(""ReturnType = "" + index.ReturnType)
		WriteLine(""SetterAccess = "" + index.SetterAccess)
		WriteLine(""SetterAttributes = "" + index.SetterAttributes)
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
GetterAccess = null
GetterAttributes = []
HasGetter = true
HasSetter = false
Parameters = [i]
ReturnType = int
SetterAccess = null
SetterAttributes = null
", result);
		}
	
		[Test]
		public void MethodMethods()
		{
			string source = @"
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	let method = Globals.Classes.Head.Methods.Head in
		WriteLine("""")
		WriteLine(""Constraints = "" + method.Constraints)
		WriteLine(""GenericArguments = "" + method.GenericArguments)
		WriteLine(""IsConstructor = "" + method.IsConstructor)
		WriteLine(""IsFinalizer = "" + method.IsFinalizer)
		WriteLine(""ReturnType = "" + method.ReturnType)
		WriteLine(""Parameters = "" + method.Parameters)
		WriteLine(""Is = "" + (method is Member))
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Constraints = null
GenericArguments = null
IsConstructor = false
IsFinalizer = false
ReturnType = int
Parameters = [x]
Is = true
", result);
		}
		
		[Test]
		public void OperatorMethods()
		{
			string source = @"
public abstract class MyClass : IFoo, IBar
{
	public static int operator+(int lhs, int rhs)
	{
		return lhs + rhs;
	}
}
";
			
			string script = @"
define Run()
	let op = Globals.Classes.Head.Operators.Head in
		WriteLine("""")
		WriteLine(""IsConversion = "" + op.IsConversion)
		WriteLine(""IsExplicit = "" + op.IsExplicit)
		WriteLine(""IsImplicit = "" + op.IsImplicit)
		WriteLine(""Parameters = "" + op.Parameters)
		WriteLine(""ReturnType = "" + op.ReturnType)
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
IsConversion = false
IsExplicit = false
IsImplicit = false
Parameters = [lhs, rhs]
ReturnType = int
", result);
	}
		
		[Test]
		public void ParameterMethods()
		{
			string source = @"
public abstract partial class MyClass : IFoo, IBar
{
	public static int Process(int x, out string s, params object[] args)
	{
		return x + x;
	}
}
";
			
			string script = @"
define Run()
	let params = Globals.Classes.Head.Methods.Head.Parameters, p1 = params.First, 
	p2 = params.Second, p3 = params.Last in
		WriteLine("""")
		WriteLine(""Attributes1 = "" + p1.Attributes)
		WriteLine(""Attributes2 = "" + p2.Attributes)
		WriteLine(""Attributes3 = "" + p3.Attributes)

		WriteLine(""IsParams1 = "" + p1.IsParams)
		WriteLine(""IsParams2 = "" + p2.IsParams)
		WriteLine(""IsParams3 = "" + p3.IsParams)

		WriteLine(""Modifier1 = "" + p1.Modifier)
		WriteLine(""Modifier2 = "" + p2.Modifier)
		WriteLine(""Modifier3 = "" + p3.Modifier)

		WriteLine(""Name1 = "" + p1.Name)
		WriteLine(""Name2 = "" + p2.Name)
		WriteLine(""Name3 = "" + p3.Name)

		WriteLine(""Type1 = "" + p1.Type)
		WriteLine(""Type2 = "" + p2.Type)
		WriteLine(""Type3 = "" + p3.Type)
	end
end
";
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Attributes1 = []
Attributes2 = []
Attributes3 = []
IsParams1 = false
IsParams2 = false
IsParams3 = true
Modifier1 = none
Modifier2 = out
Modifier3 = none
Name1 = x
Name2 = s
Name3 = args
Type1 = int
Type2 = string
Type3 = object[]
", result);
		}
	
		[Test]
		public void PropertyMethods()
		{
			string source = @"
public class MyClass : IFoo, IBar
{
	public int Weight {get; private set;}

	public int Height
	{
		get {return 100;}
	}
}
";
			
			string script = @"
define Run()
	let props = Globals.Classes.Head.Properties, p1 = props.First, p2 = props.Second in
		WriteLine("""")
		WriteLine(""GetterAccess1 = "" + p1.GetterAccess)
		WriteLine(""GetterAccess2 = "" + p2.GetterAccess)

		WriteLine(""GetterAttributes1 = "" + p1.GetterAttributes)
		WriteLine(""GetterAttributes2 = "" + p2.GetterAttributes)

		WriteLine(""HasGetter1 = "" + p1.HasGetter)
		WriteLine(""HasGetter2 = "" + p2.HasGetter)

		WriteLine(""HasSetter1 = "" + p1.HasSetter)
		WriteLine(""HasSetter2 = "" + p2.HasSetter)

		WriteLine(""ReturnType1 = "" + p1.ReturnType)
		WriteLine(""ReturnType2 = "" + p2.ReturnType)

		WriteLine(""SetterAccess1 = "" + p1.SetterAccess)
		WriteLine(""SetterAccess2 = "" + p2.SetterAccess)

		WriteLine(""SetterAttributes1 = "" + p1.SetterAttributes)
		WriteLine(""SetterAttributes2 = "" + p2.SetterAttributes)
	end
end
";
	
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
GetterAccess1 = null
GetterAccess2 = null
GetterAttributes1 = []
GetterAttributes2 = []
HasGetter1 = true
HasGetter2 = true
HasSetter1 = true
HasSetter2 = false
ReturnType1 = int
ReturnType2 = int
SetterAccess1 = private
SetterAccess2 = null
SetterAttributes1 = []
SetterAttributes2 = null
", result);
		}
		
		[Test]
		public void UsingAliasMethods()
		{
			string source = @"
using Cookie = IntPtr;
using OldCollections = System.Collections;
";
		
			string script = @"
define Run()
	let a1 = Globals.Aliases.First, a2 = Globals.Aliases.Second in
		WriteLine("""")
		WriteLine(""Alias1 = "" + a1.Alias)
		WriteLine(""Alias2 = "" + a2.Alias)

		WriteLine(""Value1 = "" + a1.Value)
		WriteLine(""Value2 = "" + a2.Value)
	end
end
";
	
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Alias1 = Cookie
Alias2 = OldCollections
Value1 = IntPtr
Value2 = System.Collections
", result);
		}
	
		[Test]
		public void UsingDirectiveMethods()
		{
			string source = @"
using Mono.Posix;
using System.Collections;
";
			
			string script = @"
define Run()
	let a1 = Globals.Uses.First, a2 = Globals.Uses.Second in
		WriteLine("""")
		WriteLine(""Name1 = "" + a1.Namespace)
		WriteLine(""Name2 = "" + a2.Namespace)
	end
end
";
	
			string result = DoParse(script, source, 0);
			Assert.AreEqual(@"
Name1 = Mono.Posix
Name2 = System.Collections
", result);
		}
	}
}
#endif	// TEST
