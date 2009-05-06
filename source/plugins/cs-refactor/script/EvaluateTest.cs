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
using CsRefactor;
using NUnit.Framework;
//using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CsRefactor.Script
{
	// Test declarations, statements, and expressions.
	[TestFixture]
	public sealed class EvaluateTest
	{
		private string[] DoParse(string refactor, string cs, int offset)
		{
			CsGlobalNamespace globals = new CsParser.Parser().Parse(cs);
			Script script = new Parser(refactor).Parse();
			RefactorCommand[] commands = script.Evaluate(new Context(script, globals, cs, offset, 0)); 
			return (from c in commands select c.ToString()).ToArray();
		}
		
		[Test] // --------------------------------------------------------------------
		public void Trivial1()
		{
			string script = @"
	define Run()		# this is the simplest legal refactor script
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
			
			Assert.AreEqual(0, results.Length);
		}
	
		[Test] // --------------------------------------------------------------------
		public void Run1()
		{
			string script = @"
	define Run()
		Globals.AddUsing(""System"")
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void VoidMethodCall()
		{
			string script = @"
	define Run()
		Globals.Namespace.Name
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Void does not respond to the get_Name method"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadArgCount()
		{
			string script = @"
	define Run()
		InsertBeforeSelection(""text"", true)
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Script.InsertBeforeSelection takes one argument"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void InvalidArg()
		{
			string script = @"
	define Run()
		Raise(true)
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Expected a String for the first argument to Script.Raise, not Boolean"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadMethod()
		{
			string script = @"
	define Run()
		xxx(true)
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Script does not respond to the xxx method."))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void If1()
		{
			string script = @"
	define Run()
		if Scope is Namespace then
			Scope.AddUsing(""System"")
		elif Scope is Method then
			Scope.InsertProlog(""CWL"")
		end
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void If2()
		{
			string source = @"
	internal sealed class MyClass
	{
		public int Process(int x)
		{
			return x + x;
		}
	}
	";
			
			string script = @"
	define Run()
		if Scope is Namespace then
			Scope.AddUsing(""System"")
		elif Scope is Method then
			Scope.Body.InsertFirst(""CWL"")
		end
	end";
	
			string[] results = DoParse(script, source, source.IndexOf("return"));
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("Process.Body.InsertFirst(CWL)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadPredicate()
		{
			string script = @"
	define Run()
		if Scope.Namespaces then
			Scope.AddUsing(""oops"")
		end
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Predicate should be a Boolean, but is Sequence"))
					Assert.Fail(e.ToString());
	
				if (!e.Message.Contains("Line 3"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void Return()
		{
			string script = @"
	define Run()
		if Enabled then
			Scope.AddUsing(""System"")
		end
	end
	
	define property Enabled
		return true
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadCustomArgCount()
		{
			string script = @"
	define Run()
		if IsFoo() then
			Scope.AddUsing(""oops"")
		end
	end
	
	define IsFoo(x)
		return true
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("IsFoo method takes 1 argument, not 0 arguments"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadCustomArgType()
		{
			string script = @"
	define Run()
		if IsFoo(true) then
			Scope.AddUsing(""oops"")
		end
	end
	
	define IsFoo(x)
		return x.IsEmpty
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Boolean does not respond to the get_IsEmpty method"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void CustomMethod()
		{
			string script = @"
	define Run()
		if IsFoo(""foo"") then
			Scope.AddUsing(""System"")
		end
	end
	
	define IsFoo(x)
		return x == ""foo""
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
	
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void RunValue1()
		{
			string script = @"
	define Run()
		return null			# OK for Run to return null
	end
	";
			
			string[] results = DoParse(script, string.Empty, 0);
			Assert.AreEqual(0, results.Length);
		}
	
		[Test] // --------------------------------------------------------------------
		public void BadRunValue()
		{
			string script = @"
	define Run()
		return ""oops""
	end
	";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Run should return null or an Edit, not String"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void ReservedWord()
		{
			string script = @"
	define Run()
		if while(""foo"") then
			Scope.AddUsing(""System"")
		end
	end
	
	define while(x)
		return x == ""foo""
	end";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (ScannerException e)
			{
				if (!e.Message.Contains("Line 3: while is a reserved word"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void From1()
		{
			string source = @"
	namespace Alpha
	{
	}
	
	namespace Beta
	{
	}
	";
			
			string script = @"
	define Run()
		for ns in self.Globals.Namespaces do
			ns.AddUsing(""System"")
		end
	end
	";
			
			string[] results = DoParse(script, source, 0);
	
			Assert.AreEqual(2, results.Length);
			Assert.AreEqual("Alpha.AddUsing(System)", results[0]);
			Assert.AreEqual("Beta.AddUsing(System)", results[1]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void From2()
		{
			string source = @"
	namespace Alpha
	{
	}
	
	namespace Beta
	{
	}
	
	namespace Gamma
	{
	}
	";
			
			string script = @"
	define Run()
		for ns in self.Globals.Namespaces where ns.Name.StartsWith(""B"") do
			ns.AddUsing(""System"")
		end
	end
	";
			
			string[] results = DoParse(script, source, 0);
	
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("Beta.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void NullCall()
		{
			string script = @"
	define Run()
		if null is String then
			Scope.AddUsing(""xxx"")
		elif null is Void then
			Scope.AddUsing(""System"")
		end
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(System)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void StringInterpolation1()
		{
			string script = @"
	define Run()
		InsertBeforeSelection(""Name: #{Scope.Name}"")
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual("InsertBeforeLine(0, Name: <globals>)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void StringInterpolation2()
		{
			string source = @"
	namespace Alpha
	{
	}
	
	namespace Beta
	{
	}
	
	namespace Gamma
	{
	}
	";
			
			string script = @"
	define Run()
		InsertBeforeSelection(""Names: #{Scope.Namespaces}"")
	end";
			
			string[] results = DoParse(script, source, 0);
					
			Assert.AreEqual("InsertBeforeLine(0, Names: [Alpha, Beta, Gamma])", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void StringInterpolation3()
		{
			string source = @"
	namespace Alpha
	{
	}
	";
			
			string script = string.Format(@"
	define Run()
		InsertBeforeSelection(""#{1}GetNames(Globals, {0}foo.{0})){2}"")
	end
	
	define GetNames(ns, prefix)
		return prefix + ns.Name
	end", "\"\"", "{", "}");
			
			string[] results = DoParse(script, source, 0);
					
			Assert.AreEqual("InsertBeforeLine(0, foo.<globals>)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void StringInterpolation4()
		{
			string source = @"
	namespace Alpha
	{
	}
	";
			
			string script = @"
	define Run()
		InsertBeforeSelection(""name = """"#{Globals.Name}"""" "")
	end
	
	define GetNames(ns, prefix)
		return prefix + ns.Name
	end";
			
			string[] results = DoParse(script, source, 0);
					
			Assert.AreEqual("InsertBeforeLine(0, name = \"<globals>\" )", results[0]);
		}
		
		[Test] // --------------------------------------------------------------------
		public void Let()
		{
			string source = @"
	namespace Alpha
	{
	}
	
	namespace Beta
	{
	}
	
	namespace Gamma
	{
	}
	";
			
			string script = @"
	define Run()
		let name = Scope.Name in
			InsertBeforeSelection(""Name: #{name}"")
		end
	end";
			
			string[] results = DoParse(script, source, 0);
					
			Assert.AreEqual("InsertBeforeLine(0, Name: <globals>)", results[0]);
		}
	
		[Test] // --------------------------------------------------------------------
		public void OverrideBuiltin()
		{
			string script = @"
	define Run()
		return ""oops""
	end
	
	define Write(m, n)
		WriteLine(m + n)
	end
	";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("The Write method is already defined"))
					Assert.Fail(e.ToString());
			}
		}
	
		[Test] // --------------------------------------------------------------------
		public void InfiniteRecursion()
		{
			string script = @"
	define Run()
		return Recurse(""foo"")
	end
	
	define Recurse(m)
		return Recurse(m)
	end
	";
			
			try
			{
				DoParse(script, string.Empty, 0);
				Assert.Fail("expected an exception");
			}
			catch (EvaluateException e)
			{
				if (!e.Message.Contains("Method calls have recursed more than 256 times"))
					Assert.Fail(e.ToString());
			}
		}
		
		[Test] // --------------------------------------------------------------------
		public void SequenceLiteral()
		{
			string script = @"
	define Run()
		Globals.AddUsing(""+"".Join([""alpha"", true, ""gamma""]))
	end";
			
			string[] results = DoParse(script, string.Empty, 0);
					
			Assert.AreEqual(1, results.Length);
			Assert.AreEqual("<globals>.AddUsing(alpha+true+gamma)", results[0]);
		}
	}
}
#endif	// TEST
