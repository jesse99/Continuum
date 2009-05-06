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
using CsRefactor.Script;
using NUnit.Framework;
//using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

[TestFixture]
public sealed class ParserTest
{
	[Test]
	public void Trivial1()
	{
		string text = @"
define Run()		# this is the simplest legal refactor script
end";
		
		var parser = new Parser(text);
		Script script = parser.Parse();
		string result = script.ToString();
		
		Assert.AreEqual(@"define Run()
", result);
	}

	[Test]
	public void Run1()
	{
		string text = @"
define Run()
	Globals.AddUsing(""System"")
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.get_Globals().AddUsing(""System"")
", result);
	}

	[Test]
	public void MisspelledEnd()
	{
		string text = @"
define Run()
	Globals.AddUsing(""System"")
emd";
		
		var parser = new Parser(text);
		try			// TODO: can do this better with nunit 2.4
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("to end the Run method"))	// emd is treated as a property call (which would be caught later when we execute the script because it a) doesn't exist and b) would not be of type Void)
				Assert.Fail(e.ToString());
		}
	}

	[Test]
	public void ExtraStuff()
	{
		string text = @"
define Run()
	Globals.AddUsing(""System"")
end

xxx";
		
		var parser = new Parser(text); 
		try
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("xxx"))
				Assert.Fail(e.ToString());
		}
	}

	[Test]
	public void NoRunMethod()
	{
		string text = @"
define Runxx()
	Globals.AddUsing(""System"")
end";
		
		var parser = new Parser(text); 
		try
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("Script has no Run method"))
				Assert.Fail(e.ToString());
		}
	}

	[Test]
	public void EmptyScript()
	{
		string text = @"

";
		
		var parser = new Parser(text); 
		try
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("Script has no Run method"))
				Assert.Fail(e.ToString());
		}
	}

	[Test]
	public void Operator1()
	{
		string text = @"
define Run()
	Foo(x or y)
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.Foo(self.get_x().op_LogicalOr(self.get_y()))
", result);
	}
	
	[Test]
	public void Literal1()
	{
		string text = @"
define Run()
	Foo(false == self)
	self.Bar(null)
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.Foo(false.op_Equals(self))
	self.Bar(null)
", result);
	}

	[Test]
	public void Parens()
	{
		string text = @"
define Run()
	Foo(false or true == true)
	Bar((false or true) == true)
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.Foo(false.op_LogicalOr(true.op_Equals(true)))
	self.Bar(false.op_LogicalOr(true).op_Equals(true))
", result);
	}
	
	[Test]
	public void ChainedCalls()
	{
		string text = @"
define Run()
	x.y.Z(true, false)
	X(null).Y(false, true).Z(self)
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.get_x().get_y().Z(true, false)
	self.X(null).Y(false, true).Z(self)
", result);
	}
	
	[Test]
	public void If1()
	{
		string text = @"
define Run()
	if a then
		m
	end

	if b then
		n
	elif c then
		o
	end

	if d then
		p
	else
		q
	end

	if e then
		r
	elif f then
		s
	else
		t
	end
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	Conditional
		if self.get_a()
			self.get_m()
	Conditional
		if self.get_b()
			self.get_n()
		if self.get_c()
			self.get_o()
	Conditional
		if self.get_d()
			self.get_p()
		if true
			self.get_q()
	Conditional
		if self.get_e()
			self.get_r()
		if self.get_f()
			self.get_s()
		if true
			self.get_t()
", result);
	}
	
	[Test]
	public void For1()
	{
		string text = @"
define Run()
	for method in Globals.Methods do
		method.InsertProlog(text)
	end
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	For method in self.get_Globals().get_Methods()
		method.InsertProlog(self.get_text())
", result);
	}
	
	[Test]
	public void For2()
	{
		string text = @"
define Run()
	for method in Globals.Methods where method.HasFoo do
		method.InsertProlog(text)
	end
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	For method in self.get_Globals().get_Methods() where method.get_HasFoo()
		method.InsertProlog(self.get_text())
", result);
	}
	
	[Test]
	public void NestedFor()
	{
		string text = @"
define Run()
	for method1 in Globals.Methods do
		for method2 in Globals.Methods do
			if method1 != method2 then
				method1.InsertProlog(method2.Name)
			end
		end
	end
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	For method1 in self.get_Globals().get_Methods()
		For method2 in self.get_Globals().get_Methods()
			Conditional
				if method1.op_NotEquals(method2)
					method1.InsertProlog(method2.get_Name())
", result);

		text = @"
define Run()
	for method1 in Globals.Methods do
		for method1 in Globals.Methods do
			if method1 != method1 then
				method1.InsertProlog(method1.Name)
			end
		end
	end
end";
		
		parser = new Parser(text); 
		try
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("There is already a definition for"))
				Assert.Fail(e.ToString());
		}
	}
	
	[Test]
	public void Property1()
	{
		string text = @"
define Run()
	for method in Methods do
		method.InsertProlog(text)
	end
end

define property Methods
	return Globals.Methods
end
";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	For method in self.get_Methods()
		method.InsertProlog(self.get_text())

define get_Methods()
	return self.get_Globals().get_Methods()
", result);
	}
	
	[Test]
	public void DuplicateMethod()
	{
		string text = @"
define Run()
	for method in Methods do
		method.InsertProlog(text)
	end
end

define property Methods
	return Globals.Methods
end

define Run()
	return Globals.Methods
end
";
		
		var parser = new Parser(text); 
		try
		{
			parser.Parse();
			Assert.Fail("expected an exception");
		}
		catch (ParserException e)
		{
			if (!e.Message.Contains("method was defined more than once"))
				Assert.Fail(e.ToString());
		}
	}

	[Test]
	public void StringInterpolate()
	{
		string text = @"
define Run()
	Foo(""value: #{x == y}"")
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
				
		Assert.AreEqual(@"define Run()
	self.Foo(""value: "".op_Add(self.get_x().op_Equals(self.get_y())).op_Add(""""))
", result);
	}
	
	[Test]
	public void From1()
	{
		string text = @"
define Run()
	let ctors = (from method in Globals.Methods where method.IsConstructor) in
		WriteLine(ctors)
	end
end";
		
		var parser = new Parser(text); 
		Script script = parser.Parse();
		string result = script.ToString();
						
		Assert.AreEqual(@"define Run()
	Let ctors = from method in self.get_Globals().get_Methods() where method.get_IsConstructor()
		self.WriteLine(ctors)
", result);
	}
}
#endif	// TEST
