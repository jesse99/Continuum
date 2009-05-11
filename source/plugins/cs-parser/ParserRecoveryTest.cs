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
using System.Diagnostics;
using System.Linq;

namespace CsParser
{
	[TestFixture]
	public sealed class ParserRecoveryTest
	{
		private void DoCheck(string text)
		{
			Log.WriteLine(TraceLevel.Verbose, "CsParser", string.Empty);
			Log.WriteLine(TraceLevel.Verbose, "CsParser", "{0} {1} {2}", new string('*', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('*', 10));
			
			Parser parser = new Parser();
			
			int offset, length;
			CsGlobalNamespace globals;
			Token[] tokens, comments;
			parser.TryParse(text, out offset, out length, out globals, out tokens, out comments);
			
			string name = "Good";
			Assert.IsTrue(globals.Classes.Length > 0, "no classes");
			Assert.IsTrue(globals.Classes[0].Methods.Length > 0, "no methods");
			Assert.IsTrue(globals.Classes[0].Methods.SingleOrDefault(m => m.Name == name) != null, string.Format("didn't find {0} in {1}", name, globals.Classes[0].Methods.ToDebugString()));
		}
		
		[TestFixtureSetUp]
		public void Init()
		{
			Log.SetLevel(TraceLevel.Verbose);
		}
		
		// Note that it seems difficult to recover from an extra curly braces.
		[Test]
		public void ExtraLeftParen()
		{
			DoCheck(@"public class MyClass
{
	public void Bad(int x)
	{
		if ((x > 0)
			Console.WriteLine(x);
	}

	public void Good(int y)
	{
	}
}");
		}
		
		[Test]
		public void ExtraRightParen()
		{
			DoCheck(@"public class MyClass
{
	public void Bad(int x)
	{
		if (x > 0))
			Console.WriteLine(x);
	}

	public void Good(int y)
	{
	}
}");
		}
		
		[Test]
		public void ExtraComma()
		{
			DoCheck(@"public class MyClass
{
	public void Bad(int x,, int y)
	{
		if (x > 0)
		{
			Console.WriteLine(x);
		}
	}

	public void Good(int y)
	{
	}
}");
		}
	
		[Test]
		public void ExtraNamespaceDot()
		{
			DoCheck(@"using System;
using System.Collections.Generic.;

public class MyClass
{
	public void Good(int y)
	{
	}
}");
		}
	}
}
#endif	// TEST
